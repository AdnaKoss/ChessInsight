using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Čuva kompletno stanje šahovske igre u jednom trenutku.
    /// Odgovorna za: ko je na potezu, prava na rokadu,
    /// detekciju šaha / mata / pata i primjenu poteza.
    /// </summary>
    public class GameState
    {
        public Board Board { get; private set; }
        public PieceColor CurrentPlayer { get; private set; }
        public GameStatus Status { get; private set; }

        // Prava na rokadu — gube se čim kralj ili top pomakne
        public bool WhiteCanCastleKingside { get; private set; } = true;
        public bool WhiteCanCastleQueenside { get; private set; } = true;
        public bool BlackCanCastleKingside { get; private set; } = true;
        public bool BlackCanCastleQueenside { get; private set; } = true;

        // Broj polupoteza bez hvatanja ili pomaka pješaka (za pravilo 50 poteza)
        public int HalfMoveClock { get; private set; } = 0;

        // Ukupan broj poteza u igri
        public int FullMoveNumber { get; private set; } = 1;

        /// <summary>
        /// Zobristov hash trenutne pozicije — keširan i inkrementalno ažuriran
        /// u ApplyMove kako bi Search() radio u O(1) umjesto O(64) po čvoru.
        /// </summary>
        public ulong ZobristHash { get; private set; }

        public GameState()
        {
            Board = new Board();
            CurrentPlayer = PieceColor.White;
            Status = GameStatus.Active;
            Board.SetupStartingPosition();
            ZobristHash = Zobrist.Compute(this);
        }

        // Konstruktor za kloniranje i ApplyMove — prima preračunati hash
        private GameState(Board board, PieceColor currentPlayer, GameStatus status,
            bool wCK, bool wCQ, bool bCK, bool bCQ, int halfMove, int fullMove,
            ulong? hash = null)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
            Status = status;
            WhiteCanCastleKingside = wCK;
            WhiteCanCastleQueenside = wCQ;
            BlackCanCastleKingside = bCK;
            BlackCanCastleQueenside = bCQ;
            HalfMoveClock = halfMove;
            FullMoveNumber = fullMove;
            // Ako hash nije proslijeđen (npr. FromFen), računaj od nule
            ZobristHash = hash ?? Zobrist.Compute(this);
        }

        // ── Detekcija šaha ───────────────────────────────────────

        /// <summary>
        /// Provjerava da li je kralj zadane boje trenutno u šahu.
        /// </summary>
        public bool IsInCheck(PieceColor color)
        {
            var king = Board.GetKing(color);
            if (king == null) return false;

            // Provjeri da li bilo koja protivnička figura napada kralja
            var opponent = Opponent(color);
            return Board.GetPieces(opponent)
                        .Any(p => p.CanAttackSquare(king.Position, Board));
        }

        /// <summary>Provjerava da li je trenutni igrač u šah-matu.</summary>
        public bool IsCheckmate(List<Move> legalMoves) =>
            legalMoves.Count == 0 && IsInCheck(CurrentPlayer);

        /// <summary>Provjerava da li je trenutni igrač u patu.</summary>
        public bool IsStalemate(List<Move> legalMoves) =>
            legalMoves.Count == 0 && !IsInCheck(CurrentPlayer);

        // ── Primjena poteza ──────────────────────────────────────

        /// <summary>
        /// Primjenjuje potez i vraća novo stanje igre.
        /// Originalno stanje ostaje nepromijenjeno — važno za Minimax.
        /// </summary>
        public GameState ApplyMove(Move move)
        {
            var newBoard = Board.Clone();
            var piece    = newBoard.GetPiece(move.From)!;

            bool isCapture  = move.Type == MoveType.Capture ||
                              move.Type == MoveType.EnPassant;
            bool isPawnMove = piece.Type == PieceType.Pawn;

            Square? newEnPassant = null;

            // ── Inkrementalni Zobrist hash ───────────────────────
            // Svi XOR-ovi se rade na originalnoj tabli PRIJE modifikacije newBoard.
            ulong newHash = ZobristHash;

            // 1. Promjena strane na potezu
            newHash ^= Zobrist.GetBlackToMoveKey();

            // 2. Ukloni staro en passant polje iz hasha
            if (Board.EnPassantSquare != null)
                newHash ^= Zobrist.GetEnPassantKey(Board.EnPassantSquare.Column);

            // 3. Ukloni figure s polaznog i (eventualno) ciljnog polja
            newHash ^= Zobrist.PieceKey(piece.Color, piece.Type, move.From.Row, move.From.Column);

            // Hvatanje na ciljnom polju (osim en passant — tamo je drugačija logika)
            if (move.Type != MoveType.EnPassant)
            {
                var capturedAtTo = Board.GetPiece(move.To);
                if (capturedAtTo != null)
                    newHash ^= Zobrist.PieceKey(capturedAtTo.Color, capturedAtTo.Type,
                                                move.To.Row, move.To.Column);
            }

            // ── Normalan potez ili hvatanje ──────────────────────
            newBoard.RemovePiece(move.From);
            newBoard.SetPiece(move.To, piece);

            // ── Specijalni potezi ────────────────────────────────
            switch (move.Type)
            {
                case MoveType.EnPassant:
                {
                    int epRow    = move.From.Row;
                    var epSquare = new Square(epRow, move.To.Column);
                    var epPawn   = Board.GetPiece(epSquare)!;
                    newHash ^= Zobrist.PieceKey(epPawn.Color, epPawn.Type, epRow, move.To.Column);
                    newBoard.RemovePiece(epSquare);
                    break;
                }
                case MoveType.CastleKingside:
                {
                    int row = move.From.Row;
                    // Top prelazi s h→f
                    newHash ^= Zobrist.PieceKey(piece.Color, PieceType.Rook, row, 7);
                    newHash ^= Zobrist.PieceKey(piece.Color, PieceType.Rook, row, 5);
                    var ckRook = newBoard.RemovePiece(new Square(row, 7));
                    newBoard.SetPiece(new Square(row, 5), ckRook);
                    break;
                }
                case MoveType.CastleQueenside:
                {
                    int row = move.From.Row;
                    // Top prelazi s a→d
                    newHash ^= Zobrist.PieceKey(piece.Color, PieceType.Rook, row, 0);
                    newHash ^= Zobrist.PieceKey(piece.Color, PieceType.Rook, row, 3);
                    var cqRook = newBoard.RemovePiece(new Square(row, 0));
                    newBoard.SetPiece(new Square(row, 3), cqRook);
                    break;
                }
                case MoveType.PawnPromotion:
                {
                    PieceType promType = move.PromotionPiece ?? PieceType.Queen;
                    newBoard.RemovePiece(move.To);
                    Piece promoted = promType switch
                    {
                        PieceType.Queen  => new Queen (piece.Color, move.To),
                        PieceType.Rook   => new Rook  (piece.Color, move.To),
                        PieceType.Bishop => new Bishop(piece.Color, move.To),
                        PieceType.Knight => new Knight(piece.Color, move.To),
                        _                => new Queen (piece.Color, move.To)
                    };
                    newBoard.SetPiece(move.To, promoted);
                    // Hash: pješak je već uklonjen s From, sada dodaj promovanu figuru
                    newHash ^= Zobrist.PieceKey(piece.Color, promType, move.To.Row, move.To.Column);
                    break;
                }
                case MoveType.Normal when isPawnMove &&
                     Math.Abs(move.To.Row - move.From.Row) == 2:
                {
                    int epDirection = piece.Color == PieceColor.White ? -1 : +1;
                    newEnPassant = new Square(move.To.Row + epDirection, move.To.Column);
                    break;
                }
            }

            // Za sve poteze osim promocije — dodaj figuru na ciljno polje
            if (move.Type != MoveType.PawnPromotion)
                newHash ^= Zobrist.PieceKey(piece.Color, piece.Type, move.To.Row, move.To.Column);

            // 4. Novo en passant polje
            if (newEnPassant != null)
                newHash ^= Zobrist.GetEnPassantKey(newEnPassant.Column);

            newBoard.EnPassantSquare = newEnPassant;

            // ── Ažuriranje prava na rokadu ───────────────────────
            bool newWCK = WhiteCanCastleKingside;
            bool newWCQ = WhiteCanCastleQueenside;
            bool newBCK = BlackCanCastleKingside;
            bool newBCQ = BlackCanCastleQueenside;

            if (piece.Type == PieceType.King)
            {
                if (piece.Color == PieceColor.White) { newWCK = false; newWCQ = false; }
                else { newBCK = false; newBCQ = false; }
            }
            if (piece.Type == PieceType.Rook)
            {
                if (move.From.Equals(new Square(0, 0))) newWCQ = false;
                if (move.From.Equals(new Square(0, 7))) newWCK = false;
                if (move.From.Equals(new Square(7, 0))) newBCQ = false;
                if (move.From.Equals(new Square(7, 7))) newBCK = false;
            }

            // 5. Ažuriranje rokadnih prava u hashu
            // XOR-aj stara prava i nova prava — razlika je automatski uključena
            if (WhiteCanCastleKingside)  newHash ^= Zobrist.GetCastlingKey(0);
            if (WhiteCanCastleQueenside) newHash ^= Zobrist.GetCastlingKey(1);
            if (BlackCanCastleKingside)  newHash ^= Zobrist.GetCastlingKey(2);
            if (BlackCanCastleQueenside) newHash ^= Zobrist.GetCastlingKey(3);
            if (newWCK) newHash ^= Zobrist.GetCastlingKey(0);
            if (newWCQ) newHash ^= Zobrist.GetCastlingKey(1);
            if (newBCK) newHash ^= Zobrist.GetCastlingKey(2);
            if (newBCQ) newHash ^= Zobrist.GetCastlingKey(3);

            // ── Sat polupoteza i broj poteza ─────────────────────
            int newHalfMove = (isCapture || isPawnMove) ? 0 : HalfMoveClock + 1;
            int newFullMove = CurrentPlayer == PieceColor.Black
                ? FullMoveNumber + 1
                : FullMoveNumber;

            return new GameState(
                newBoard, Opponent(CurrentPlayer), GameStatus.Active,
                newWCK, newWCQ, newBCK, newBCQ, newHalfMove, newFullMove, newHash
            );
        }

        // ── Ažuriranje statusa ───────────────────────────────────

        /// <summary>
        /// Ažurira status igre na osnovu legalnih poteza.
        /// Poziva se nakon što MoveGenerator vrati legalne poteze.
        /// </summary>
        public void UpdateStatus(List<Move> legalMoves)
        {
            if (IsCheckmate(legalMoves))
                Status = GameStatus.Checkmate;
            else if (IsStalemate(legalMoves))
                Status = GameStatus.Stalemate;
            else if (IsInCheck(CurrentPlayer))
                Status = GameStatus.Check;
            else if (HalfMoveClock >= 100)
                Status = GameStatus.Draw;
            else
                Status = GameStatus.Active;
        }

        // ── Kloniranje ───────────────────────────────────────────

        public GameState Clone() => new GameState(
            Board.Clone(), CurrentPlayer, Status,
            WhiteCanCastleKingside, WhiteCanCastleQueenside,
            BlackCanCastleKingside, BlackCanCastleQueenside,
            HalfMoveClock, FullMoveNumber, ZobristHash
        );
        /// <summary>
        /// Konstruktor za custom poziciju — koristi se u testovima.
        /// </summary>
        public GameState(Board board, PieceColor currentPlayer)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
            Status = GameStatus.Active;
            ZobristHash = Zobrist.Compute(this);
        }
        // ── Factory za FEN ───────────────────────────────────────

        public static GameState FromFen(Board board, PieceColor currentPlayer,
            bool wCK, bool wCQ, bool bCK, bool bCQ, int halfMove, int fullMove)
        {
            return new GameState(board, currentPlayer, GameStatus.Active,
                wCK, wCQ, bCK, bCQ, halfMove, fullMove);
        }

        // ── Null potez (za Null Move Pruning) ───────────────────

        /// <summary>
        /// Vraća stanje u kojem je protivnik na potezu, a tabla je ista.
        /// En passant polje se poništava jer se "preskočio" polu-potez.
        /// Koristi se isključivo u Null Move Pruning heuristici.
        /// </summary>
        public GameState ApplyNullMove()
        {
            var board = Board.Clone();
            board.EnPassantSquare = null;

            ulong newHash = ZobristHash;
            newHash ^= Zobrist.GetBlackToMoveKey();
            if (Board.EnPassantSquare != null)
                newHash ^= Zobrist.GetEnPassantKey(Board.EnPassantSquare.Column);

            return new GameState(board, Opponent(CurrentPlayer), GameStatus.Active,
                WhiteCanCastleKingside, WhiteCanCastleQueenside,
                BlackCanCastleKingside, BlackCanCastleQueenside,
                HalfMoveClock, FullMoveNumber, newHash);
        }

        // ── FEN export ───────────────────────────────────────────

        public string ToFen()
        {
            var sb = new System.Text.StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    var piece = Board.GetPiece(new Square(rank, file));
                    if (piece == null) { empty++; }
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        char c = piece.Type switch
                        {
                            PieceType.King   => 'K', PieceType.Queen  => 'Q',
                            PieceType.Rook   => 'R', PieceType.Bishop => 'B',
                            PieceType.Knight => 'N', PieceType.Pawn   => 'P',
                            _ => '?'
                        };
                        sb.Append(piece.Color == PieceColor.White ? c : char.ToLower(c));
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }

            sb.Append(CurrentPlayer == PieceColor.White ? " w " : " b ");

            string castling = "";
            if (WhiteCanCastleKingside)  castling += "K";
            if (WhiteCanCastleQueenside) castling += "Q";
            if (BlackCanCastleKingside)  castling += "k";
            if (BlackCanCastleQueenside) castling += "q";
            sb.Append(string.IsNullOrEmpty(castling) ? "-" : castling);

            var ep = Board.EnPassantSquare;
            sb.Append(ep != null ? $" {(char)('a' + ep.Column)}{ep.Row + 1}" : " -");
            sb.Append($" {HalfMoveClock} {FullMoveNumber}");
            return sb.ToString();
        }

        // ── Helper ───────────────────────────────────────────────

        public static PieceColor Opponent(PieceColor color) =>
            color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}