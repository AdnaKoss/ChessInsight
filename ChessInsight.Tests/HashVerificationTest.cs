using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace ChessInsight.Tests
{
    public class HashVerificationTest
    {
        private readonly ITestOutputHelper _output;
        public HashVerificationTest(ITestOutputHelper output) => _output = output;

        [Fact]
        public void IncrementalHash_MatchesFullCompute_AfterEachMove()
        {
            var gen   = new MoveGenerator();
            var state = new GameState();
            CheckNode(state, gen, depth: 3, _output);
        }

        private static void CheckNode(GameState state, MoveGenerator gen, int depth, ITestOutputHelper output)
        {
            ulong expected = Zobrist.Compute(state);
            if (state.ZobristHash != expected)
                output.WriteLine($"MISMATCH: cached={state.ZobristHash:X16} full={expected:X16} fen={state.ToFen()}");
            Assert.Equal(expected, state.ZobristHash);
            if (depth == 0) return;
            foreach (var move in gen.GetLegalMoves(state))
                CheckNode(state.ApplyMove(move), gen, depth - 1, output);
        }
    }
}
