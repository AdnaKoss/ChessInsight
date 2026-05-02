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
    public class KnightTests
    {
        [Fact]
        public void Knight_InCenter_HasEightMoves()
        {
            var board = new Board();
            var knight = new Knight(PieceColor.White, Square.FromAlgebraic("d4"));
            board.SetPiece(knight.Position, knight);

            var moves = knight.GetPseudoLegalMoves(board);

            Assert.Equal(8, moves.Count);
        }

        [Fact]
        public void Knight_InCorner_HasTwoMoves()
        {
            var board = new Board();
            var knight = new Knight(PieceColor.White, Square.FromAlgebraic("a1"));
            board.SetPiece(knight.Position, knight);

            var moves = knight.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count);
        }
    }
}
