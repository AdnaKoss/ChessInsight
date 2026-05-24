using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class BishopTests
    {
        [Fact]
        public void Bishop_EmptyBoard_ThirteenMoves()
        {
            var board = new Board();
            var bishop = new Bishop(PieceColor.White, Square.FromAlgebraic("d4"));
            board.SetPiece(bishop.Position, bishop);

            var moves = bishop.GetPseudoLegalMoves(board);

            // NE=4, NW=3, SE=3, SW=3 = 13
            Assert.Equal(13, moves.Count);
        }

        [Fact]
        public void Bishop_Corner_SevenMoves()
        {
            var board = new Board();
            var bishop = new Bishop(PieceColor.White, Square.FromAlgebraic("a1"));
            board.SetPiece(bishop.Position, bishop);

            var moves = bishop.GetPseudoLegalMoves(board);

            // Jedina slobodna dijagonala: b2, c3, d4, e5, f6, g7, h8
            Assert.Equal(7, moves.Count);
        }

        [Fact]
        public void Bishop_BlockedByFriendly_CannotPass()
        {
            var board = new Board();
            var bishop = new Bishop(PieceColor.White, Square.FromAlgebraic("a1"));
            var blocker = new Rook(PieceColor.White, Square.FromAlgebraic("b2"));
            board.SetPiece(bishop.Position, bishop);
            board.SetPiece(blocker.Position, blocker);

            var moves = bishop.GetPseudoLegalMoves(board);

            Assert.Equal(0, moves.Count);
        }

        [Fact]
        public void Bishop_CapturesEnemy_AndStops()
        {
            var board = new Board();
            var bishop = new Bishop(PieceColor.White, Square.FromAlgebraic("a1"));
            var enemy = new Pawn(PieceColor.Black, Square.FromAlgebraic("c3"));
            board.SetPiece(bishop.Position, bishop);
            board.SetPiece(enemy.Position, enemy);

            var moves = bishop.GetPseudoLegalMoves(board);

            // b2 (Normal) i c3 (Capture) — ne može dalje
            Assert.Equal(2, moves.Count);
            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("c3")) &&
                m.Type == MoveType.Capture);
        }

        [Fact]
        public void Bishop_DoesNotCaptureOwnPiece()
        {
            var board = new Board();
            var bishop = new Bishop(PieceColor.White, Square.FromAlgebraic("a1"));
            var friendly = new Pawn(PieceColor.White, Square.FromAlgebraic("c3"));
            board.SetPiece(bishop.Position, bishop);
            board.SetPiece(friendly.Position, friendly);

            var moves = bishop.GetPseudoLegalMoves(board);

            Assert.DoesNotContain(moves, m => m.To.Equals(Square.FromAlgebraic("c3")));
        }
    }
}
