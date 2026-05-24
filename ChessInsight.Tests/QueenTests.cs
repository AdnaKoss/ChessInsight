using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class QueenTests
    {
        [Fact]
        public void Queen_EmptyBoard_TwentySevenMoves()
        {
            var board = new Board();
            var queen = new Queen(PieceColor.White, Square.FromAlgebraic("d4"));
            board.SetPiece(queen.Position, queen);

            var moves = queen.GetPseudoLegalMoves(board);

            // Poznata činjenica: dama u centru prazne table ima 27 poteza
            Assert.Equal(27, moves.Count);
        }

        [Fact]
        public void Queen_BlockedByFriendly_CannotPassThrough()
        {
            var board = new Board();
            var queen = new Queen(PieceColor.White, Square.FromAlgebraic("a1"));
            var blocker1 = new Rook(PieceColor.White, Square.FromAlgebraic("a2"));
            var blocker2 = new Rook(PieceColor.White, Square.FromAlgebraic("b2"));
            board.SetPiece(queen.Position, queen);
            board.SetPiece(blocker1.Position, blocker1);
            board.SetPiece(blocker2.Position, blocker2);

            var moves = queen.GetPseudoLegalMoves(board);

            // Jedino slobodan smjer: horizontalno desno b1-h1 = 7 polja
            Assert.Equal(7, moves.Count);
        }

        [Fact]
        public void Queen_CapturesEnemy_IncludesCaptureMove()
        {
            var board = new Board();
            var queen = new Queen(PieceColor.White, Square.FromAlgebraic("d1"));
            var enemy = new Pawn(PieceColor.Black, Square.FromAlgebraic("d5"));
            board.SetPiece(queen.Position, queen);
            board.SetPiece(enemy.Position, enemy);

            var moves = queen.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("d5")) &&
                m.Type == MoveType.Capture);
        }

        [Fact]
        public void Queen_CombinesRookAndBishopDirections()
        {
            var board = new Board();
            var queen = new Queen(PieceColor.White, Square.FromAlgebraic("a1"));
            board.SetPiece(queen.Position, queen);

            var moves = queen.GetPseudoLegalMoves(board);

            // Iz ugla: 7 horizontalnih + 7 vertikalnih + 7 dijagonalnih = 21
            Assert.Equal(21, moves.Count);
        }
    }
}
