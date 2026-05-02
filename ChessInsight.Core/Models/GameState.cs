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

        public GameState()
        {
            Board = new Board();
            CurrentPlayer = PieceColor.White;
            Status = GameStatus.Active;
            Board.SetupStartingPosition();
        }

        // Konstruktor za kloniranje
        private GameState(Board board, PieceColor currentPlayer, GameStatus status,
            bool wCK, bool wCQ, bool bCK, bool bCQ, int halfMove, int fullMove)
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
            var piece = newBoard.GetPiece(move.From)!;

            bool isCapture = move.Type == MoveType.Capture ||
                             move.Type == MoveType.EnPassant;
            bool isPawnMove = piece.Type == PieceType.Pawn;

            Square? newEnPassant = null;

            // ── Normalan potez ili hvatanje ──────────────────────
            newBoard.RemovePiece(move.From);
            newBoard.SetPiece(move.To, piece);

            // ── Specijalni potezi ────────────────────────────────
            switch (move.Type)
            {
                case MoveType.EnPassant:
                    // Ukloni uhvaćenog pješaka koji je pored, ne na To polju
                    int epRow = move.From.Row;
                    newBoard.RemovePiece(new Square(epRow, move.To.Column));
                    break;

                case MoveType.CastleKingside:
                    // Pomjeri top s h1/h8 na f1/f8
                    int ckRow = move.From.Row;
                    var ckRook = newBoard.RemovePiece(new Square(ckRow, 7));
                    newBoard.SetPiece(new Square(ckRow, 5), ckRook);
                    break;

                case MoveType.CastleQueenside:
                    // Pomjeri top s a1/a8 na d1/d8
                    int cqRow = move.From.Row;
                    var cqRook = newBoard.RemovePiece(new Square(cqRow, 0));
                    newBoard.SetPiece(new Square(cqRow, 3), cqRook);
                    break;

                case MoveType.PawnPromotion:
                    // Zamijeni pješaka s izabranom figurom
                    newBoard.RemovePiece(move.To);
                    Piece promoted = move.PromotionPiece switch
                    {
                        PieceType.Queen => new Queen(piece.Color, move.To),
                        PieceType.Rook => new Rook(piece.Color, move.To),
                        PieceType.Bishop => new Bishop(piece.Color, move.To),
                        PieceType.Knight => new Knight(piece.Color, move.To),
                        _ => new Queen(piece.Color, move.To)
                    };
                    newBoard.SetPiece(move.To, promoted);
                    break;

                case MoveType.Normal when isPawnMove &&
                     Math.Abs(move.To.Row - move.From.Row) == 2:
                    // Postavi en passant polje iza pješaka
                    int epDirection = piece.Color == PieceColor.White ? -1 : +1;
                    newEnPassant = new Square(move.To.Row + epDirection, move.To.Column);
                    break;
            }

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

            // ── Sat polupoteza i broj poteza ─────────────────────
            int newHalfMove = (isCapture || isPawnMove) ? 0 : HalfMoveClock + 1;
            int newFullMove = CurrentPlayer == PieceColor.Black
                ? FullMoveNumber + 1
                : FullMoveNumber;

            var newState = new GameState(
                newBoard, Opponent(CurrentPlayer), GameStatus.Active,
                newWCK, newWCQ, newBCK, newBCQ, newHalfMove, newFullMove
            );

            return newState;
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
            HalfMoveClock, FullMoveNumber
        );
        /// <summary>
        /// Konstruktor za custom poziciju — koristi se u testovima.
        /// </summary>
        public GameState(Board board, PieceColor currentPlayer)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
            Status = GameStatus.Active;
        }
        // ── Helper ───────────────────────────────────────────────

        public static PieceColor Opponent(PieceColor color) =>
            color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}