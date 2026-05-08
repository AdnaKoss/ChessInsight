using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Heuristička evaluacija šahovske pozicije.
    /// Pozitivan skor = prednost bijelog, negativan = prednost crnog.
    /// </summary>
    public class Evaluator
    {
        // ── Materijalne vrijednosti ──────────────────────────────
        internal static readonly Dictionary<PieceType, int> PieceValues = new()
        {
            { PieceType.Pawn,   100   },
            { PieceType.Knight, 320   },
            { PieceType.Bishop, 330   },
            { PieceType.Rook,   500   },
            { PieceType.Queen,  900   },
            { PieceType.King,   20000 }
        };

        // ── PST tablice ──────────────────────────────────────────
        // Indeksiranje: row 0 = rank 8 (crna strana), row 7 = rank 1 (bijela strana).
        // Za bijele figure: tableRow = 7 - piece.Position.Row
        // Za crne figure:   tableRow = piece.Position.Row

        private static readonly int[,] PawnTable = {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            { 50, 50, 50, 50, 50, 50, 50, 50 },
            { 10, 10, 20, 30, 30, 20, 10, 10 },
            {  5,  5, 10, 25, 25, 10,  5,  5 },
            {  0,  0,  0, 20, 20,  0,  0,  0 },
            {  5, -5,-10,  0,  0,-10, -5,  5 },
            {  5, 10, 10,-20,-20, 10, 10,  5 },
            {  0,  0,  0,  0,  0,  0,  0,  0 }
        };

        private static readonly int[,] KnightTable = {
            { -50,-40,-30,-30,-30,-30,-40,-50 },
            { -40,-20,  0,  0,  0,  0,-20,-40 },
            { -30,  0, 10, 15, 15, 10,  0,-30 },
            { -30,  5, 15, 20, 20, 15,  5,-30 },
            { -30,  0, 15, 20, 20, 15,  0,-30 },
            { -30,  5, 10, 15, 15, 10,  5,-30 },
            { -40,-20,  0,  5,  5,  0,-20,-40 },
            { -50,-40,-30,-30,-30,-30,-40,-50 }
        };

        private static readonly int[,] BishopTable = {
            { -20,-10,-10,-10,-10,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5, 10, 10,  5,  0,-10 },
            { -10,  5,  5, 10, 10,  5,  5,-10 },
            { -10,  0, 10, 10, 10, 10,  0,-10 },
            { -10, 10, 10, 10, 10, 10, 10,-10 },
            { -10,  5,  0,  0,  0,  0,  5,-10 },
            { -20,-10,-10,-10,-10,-10,-10,-20 }
        };

        private static readonly int[,] RookTable = {
            {  0,  0,  0,  0,  0,  0,  0,  0 },
            {  5, 10, 10, 10, 10, 10, 10,  5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            { -5,  0,  0,  0,  0,  0,  0, -5 },
            {  0,  0,  0,  5,  5,  0,  0,  0 }
        };

        // Popravljena tablica — bila asimetrična u redovima 4-6
        private static readonly int[,] QueenTable = {
            { -20,-10,-10, -5, -5,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -20,-10,-10, -5, -5,-10,-10,-20 }
        };

        private static readonly int[,] KingTable = {
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -20,-30,-30,-40,-40,-30,-30,-20 },
            { -10,-20,-20,-20,-20,-20,-20,-10 },
            {  20, 20,  0,  0,  0,  0, 20, 20 },
            {  20, 30, 10,  0,  0, 10, 30, 20 }
        };

        // Bonus za prolaznog pješaka po ranku (0 = vlastita strana, 7 = promocija)
        private static readonly int[] PassedPawnBonus = { 0, 10, 20, 35, 55, 80, 120, 0 };

        // ── Glavna evaluacijska funkcija ─────────────────────────

        public int Evaluate(GameState state)
        {
            int score = 0;
            var whitePawns = new List<Piece>(8);
            var blackPawns = new List<Piece>(8);

            foreach (var piece in state.Board.GetAllPieces())
            {
                int val = PieceValues[piece.Type] + GetPositionBonus(piece);

                if (piece.Color == PieceColor.White)
                {
                    score += val;
                    if (piece.Type == PieceType.Pawn) whitePawns.Add(piece);
                }
                else
                {
                    score -= val;
                    if (piece.Type == PieceType.Pawn) blackPawns.Add(piece);
                }
            }

            score += EvaluatePawnStructure(whitePawns, blackPawns);

            return score;
        }

        // ── Pozicijski bonus (PST) ────────────────────────────────

        private int GetPositionBonus(Piece piece)
        {
            // Ispravno indeksiranje:
            // Row 0 = rank 1 (bijela strana) u našem koordinatnom sistemu
            // PST row 0 = rank 8 (crna strana) — zato invertujemo za bijelog
            int row = piece.Color == PieceColor.White
                ? 7 - piece.Position.Row
                : piece.Position.Row;
            int col = piece.Position.Column;

            return piece.Type switch
            {
                PieceType.Pawn   => PawnTable[row, col],
                PieceType.Knight => KnightTable[row, col],
                PieceType.Bishop => BishopTable[row, col],
                PieceType.Rook   => RookTable[row, col],
                PieceType.Queen  => QueenTable[row, col],
                PieceType.King   => KingTable[row, col],
                _                => 0
            };
        }

        // ── Pawn structure ────────────────────────────────────────

        private int EvaluatePawnStructure(List<Piece> whitePawns, List<Piece> blackPawns)
        {
            int score = 0;
            score += ScorePawns(whitePawns, blackPawns, PieceColor.White);
            score -= ScorePawns(blackPawns, whitePawns, PieceColor.Black);
            return score;
        }

        private int ScorePawns(List<Piece> own, List<Piece> enemy, PieceColor color)
        {
            if (own.Count == 0) return 0;

            int score = 0;
            var perFile = new int[8];
            foreach (var p in own) perFile[p.Position.Column]++;

            foreach (var pawn in own)
            {
                int col = pawn.Position.Column;

                // Dvostruki pješak
                if (perFile[col] > 1) score -= 20;

                // Izolirani pješak
                bool hasNeighbour = (col > 0 && perFile[col - 1] > 0) ||
                                    (col < 7 && perFile[col + 1] > 0);
                if (!hasNeighbour) score -= 15;

                // Prolazni pješak — veći bonus što je dalje odmakao
                if (IsPassedPawn(pawn, enemy, color))
                {
                    int rank = color == PieceColor.White
                        ? pawn.Position.Row
                        : 7 - pawn.Position.Row;
                    score += PassedPawnBonus[rank];
                }
            }

            return score;
        }

        private static bool IsPassedPawn(Piece pawn, List<Piece> enemyPawns, PieceColor color)
        {
            int col = pawn.Position.Column;
            int row = pawn.Position.Row;

            foreach (var ep in enemyPawns)
            {
                if (ep.Position.Column < col - 1 || ep.Position.Column > col + 1) continue;

                // Neprijateljski pješak ispred blokira prolaz
                if (color == PieceColor.White && ep.Position.Row > row) return false;
                if (color == PieceColor.Black && ep.Position.Row < row) return false;
            }

            return true;
        }

    }
}
