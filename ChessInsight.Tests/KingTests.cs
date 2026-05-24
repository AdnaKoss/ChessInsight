using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class KingTests
    {
        [Fact]
        public void King_Center_EightNormalMoves()
        {
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("e4"));
            board.SetPiece(king.Position, king);

            var moves = king.GetPseudoLegalMoves(board);

            // Na e4 kralj nije na e1/e8 — bez rokade
            Assert.Equal(8, moves.Count);
        }

        [Fact]
        public void King_Corner_ThreeMoves()
        {
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("a1"));
            board.SetPiece(king.Position, king);

            var moves = king.GetPseudoLegalMoves(board);

            // Samo a2, b1, b2
            Assert.Equal(3, moves.Count);
        }

        [Fact]
        public void King_OnStartingSquare_GeneratesCastlingCandidates()
        {
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("e1"));
            board.SetPiece(king.Position, king);

            var moves = king.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m => m.Type == MoveType.CastleKingside);
            Assert.Contains(moves, m => m.Type == MoveType.CastleQueenside);
        }

        [Fact]
        public void King_BlackOnStartingSquare_GeneratesCastlingCandidates()
        {
            var board = new Board();
            var king = new King(PieceColor.Black, Square.FromAlgebraic("e8"));
            board.SetPiece(king.Position, king);

            var moves = king.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m => m.Type == MoveType.CastleKingside);
            Assert.Contains(moves, m => m.Type == MoveType.CastleQueenside);
        }

        [Fact]
        public void King_BlockedByFriendly_ReducedMoves()
        {
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("a1"));
            board.SetPiece(king.Position, king);
            board.SetPiece(Square.FromAlgebraic("a2"), new Pawn(PieceColor.White, Square.FromAlgebraic("a2")));
            board.SetPiece(Square.FromAlgebraic("b1"), new Pawn(PieceColor.White, Square.FromAlgebraic("b1")));

            var moves = king.GetPseudoLegalMoves(board);

            // Jedino slobodno polje: b2
            Assert.Equal(1, moves.Count);
        }

        [Fact]
        public void King_CapturesEnemy_CountedAsCaptureMove()
        {
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("e4"));
            var enemy = new Pawn(PieceColor.Black, Square.FromAlgebraic("f5"));
            board.SetPiece(king.Position, king);
            board.SetPiece(enemy.Position, enemy);

            var moves = king.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("f5")) &&
                m.Type == MoveType.Capture);
        }
    }
}
