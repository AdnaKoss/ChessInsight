using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;

namespace ChessInsight.Core.Models
{
    /// <summary>
    /// Pješak — kreće se naprijed, hvata dijagonalno.
    /// Specijalna pravila: početni korak za 2, en passant, promocija.
    /// </summary>
    public class Pawn : Piece
    {
        public Pawn(PieceColor color, Square position)
            : base(color, PieceType.Pawn, position) { }

        public override List<Move> GetPseudoLegalMoves(Board board)
        {
            var moves = new List<Move>();

            // Bijeli ide gore (+1), crni ide dolje (-1)
            int direction = Color == PieceColor.White ? +1 : -1;
            int startRow = Color == PieceColor.White ? 1 : 6;
            int promoteRow = Color == PieceColor.White ? 7 : 0;

            // ── 1. Korak naprijed za jedno polje ─────────────────
            var oneStep = new Square(Position.Row + direction, Position.Column);

            if (oneStep.IsValid() && board.IsEmpty(oneStep))
            {
                // Promocija ako stižemo na zadnji red
                if (oneStep.Row == promoteRow)
                    AddPromotionMoves(moves, oneStep);
                else
                    moves.Add(new Move(Position, oneStep, MoveType.Normal));

                // ── 2. Početni korak za dva polja ────────────────
                if (Position.Row == startRow)
                {
                    var twoStep = new Square(Position.Row + 2 * direction, Position.Column);
                    if (board.IsEmpty(twoStep))
                        moves.Add(new Move(Position, twoStep, MoveType.Normal));
                }
            }

            // ── 3. Dijagonalno hvatanje ───────────────────────────
            int[] captureCols = { Position.Column - 1, Position.Column + 1 };

            foreach (int col in captureCols)
            {
                var captureSquare = new Square(Position.Row + direction, col);

                if (!captureSquare.IsValid()) continue;

                if (board.HasEnemy(captureSquare, Color))
                {
                    if (captureSquare.Row == promoteRow)
                        AddPromotionMoves(moves, captureSquare, isCapture: true);
                    else
                        moves.Add(new Move(Position, captureSquare, MoveType.Capture));
                }

                // ── 4. En passant ─────────────────────────────────
                if (board.EnPassantSquare != null &&
                    captureSquare.Equals(board.EnPassantSquare))
                {
                    moves.Add(new Move(Position, captureSquare, MoveType.EnPassant));
                }
            }

            return moves;
        }

        // Dodaje sva 4 moguća izbora promocije
        private void AddPromotionMoves(List<Move> moves, Square target, bool isCapture = false)
        {
            var type = isCapture ? MoveType.Capture : MoveType.Normal;

            foreach (var piece in new[] {
                PieceType.Queen,
                PieceType.Rook,
                PieceType.Bishop,
                PieceType.Knight })
            {
                moves.Add(new Move(Position, target, MoveType.PawnPromotion, piece));
            }
        }

        public override Piece Clone() =>
            new Pawn(Color, new Square(Position.Row, Position.Column));
    }
}