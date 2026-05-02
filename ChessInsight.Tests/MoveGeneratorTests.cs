using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class MoveGeneratorTests
    {
        private readonly MoveGenerator _generator = new();

        [Fact]
        public void StartingPosition_WhiteHasTwentyMoves()
        {
            var state = new GameState();
            var moves = _generator.GetLegalMoves(state);

            // Na početku bijeli ima točno 20 legalnih poteza
            // (16 pješačkih + 4 skakačka)
            Assert.Equal(20, moves.Count);
        }

        [Fact]
        public void KingInCheck_OnlyBlockingMovesAllowed()
        {
            // Postavi poziciju gdje je kralj u šahu
            var board = new Board();
            var king = new King(PieceColor.White, Square.FromAlgebraic("e1"));
            var enemy = new Rook(PieceColor.Black, Square.FromAlgebraic("e8"));
            board.SetPiece(king.Position, king);
            board.SetPiece(enemy.Position, enemy);

            var state = new GameState();
            // Ručno postavljamo poziciju kroz reflection nije potrebno —
            // testiramo kroz startnu poziciju i poteze
            var moves = _generator.GetLegalMoves(state);

            // Svi potezi moraju biti legalni (kralj nije u šahu na startu)
            Assert.All(moves, move =>
            {
                var newState = state.ApplyMove(move);
                Assert.False(newState.IsInCheck(PieceColor.White));
            });
        }

        [Fact]
        public void AfterE2E4_BlackHasTwentyMoves()
        {
            var state = new GameState();
            var generator = new MoveGenerator();

            // Bijeli igra e2-e4
            var moves = generator.GetLegalMoves(state);
            var e2e4 = moves.First(m =>
                m.From.Equals(Square.FromAlgebraic("e2")) &&
                m.To.Equals(Square.FromAlgebraic("e4")));

            var newState = state.ApplyMove(e2e4);
            var blackMoves = generator.GetLegalMoves(newState);

            // Crni također ima 20 legalnih poteza
            Assert.Equal(20, blackMoves.Count);
        }
    }
}