using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Heuristička evaluacija šahovske pozicije.
    /// Pozitivan skor = prednost bijelog.
    /// Negativan skor = prednost crnog.
    /// </summary>
    public class Evaluator
    {
        // ── Materijalne vrijednosti figura ───────────────────────
        private static readonly Dictionary<PieceType, int> PieceValues = new()
        {
            { PieceType.Pawn,   100  },
            { PieceType.Knight, 320  },
            { PieceType.Bishop, 330  },
            { PieceType.Rook,   500  },
            { PieceType.Queen,  900  },
            { PieceType.King,   20000}
        };

        // ── Piece-square tablice ─────────────────────────────────
        // Bonus/malus za poziciju figure na određenom polju.
        // Indeksirano [red, kolona] iz perspektive bijelog (red 0 = bijela strana).

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

        private static readonly int[,] QueenTable = {
            { -20,-10,-10, -5, -5,-10,-10,-20 },
            { -10,  0,  0,  0,  0,  0,  0,-10 },
            { -10,  0,  5,  5,  5,  5,  0,-10 },
            {  -5,  0,  5,  5,  5,  5,  0, -5 },
            {   0,  0,  5,  5,  5,  5,  0, -5 },
            { -10,  5,  5,  5,  5,  5,  0,-10 },
            { -10,  0,  5,  0,  0,  0,  0,-10 },
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

        // ── Glavna evaluacijska funkcija ─────────────────────────

        /// <summary>
        /// Evaluira poziciju i vraća skor.
        /// Pozitivan = bijeli bolje, negativan = crni bolje.
        /// </summary>
        public int Evaluate(GameState state)
        {
            int score = 0;

            foreach (var piece in state.Board.GetAllPieces())
            {
                int value = GetMaterialValue(piece);
                int position = GetPositionBonus(piece);
                int total = value + position;

                score += piece.Color == PieceColor.White ? +total : -total;
            }

            return score;
        }

        // ── Materijalna vrijednost ────────────────────────────────

        private int GetMaterialValue(Piece piece) =>
            PieceValues.TryGetValue(piece.Type, out int val) ? val : 0;

        // ── Pozicijski bonus ─────────────────────────────────────

        private int GetPositionBonus(Piece piece)
        {
            // Za crnog invertujemo red — tablica je iz perspektive bijelog
            int row = piece.Color == PieceColor.White
                ? piece.Position.Row
                : 7 - piece.Position.Row;

            int col = piece.Position.Column;

            return piece.Type switch
            {
                PieceType.Pawn => PawnTable[row, col],
                PieceType.Knight => KnightTable[row, col],
                PieceType.Bishop => BishopTable[row, col],
                PieceType.Rook => RookTable[row, col],
                PieceType.Queen => QueenTable[row, col],
                PieceType.King => KingTable[row, col],
                _ => 0
            };
        }
    }
}