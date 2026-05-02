using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class RookTests
    {
        [Fact]
        public void Rook_EmptyBoard_HasFourteenMoves()
        {
            var board = new Board();
            var rook = new Rook(PieceColor.White, Square.FromAlgebraic("d4"));
            board.SetPiece(rook.Position, rook);

            var moves = rook.GetPseudoLegalMoves(board);

            // 7 polja horizontalno + 7 vertikalno = 14
            Assert.Equal(14, moves.Count);
        }

        [Fact]
        public void Rook_BlockedByFriendly_CannotPassThrough()
        {
            var board = new Board();
            var rook = new Rook(PieceColor.White, Square.FromAlgebraic("a1"));
            var blocker = new Rook(PieceColor.White, Square.FromAlgebraic("a4"));
            board.SetPiece(rook.Position, rook);
            board.SetPiece(blocker.Position, blocker);

            var moves = rook.GetPseudoLegalMoves(board);

            // Gore može samo a2, a3 (a4 blokiran), desno a1-h1 = 7
            Assert.Equal(9, moves.Count);
        }

        [Fact]
        public void Rook_CanCaptureEnbemy()
        {
            var board = new Board();
            var rook = new Rook(PieceColor.White, Square.FromAlgebraic("a1"));
            var enemy = new Rook(PieceColor.Black, Square.FromAlgebraic("a4"));
            board.SetPiece(rook.Position, rook);
            board.SetPiece(enemy.Position, enemy);

            var moves = rook.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("a4")) &&
                m.Type == MoveType.Capture);
        }
    }
}