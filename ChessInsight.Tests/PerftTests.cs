using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    /// <summary>
    /// Perft testovi — verifikacija korektnosti generatora poteza
    /// prebrojavanjem čvorova stabla pretrage do zadane dubine.
    /// Referentne vrijednosti su matematički dokazane za standardnu šah poziciju.
    /// </summary>
    public class PerftTests
    {
        private readonly MoveGenerator _gen = new();

        private long Perft(GameState state, int depth)
        {
            if (depth == 0) return 1;
            long nodes = 0;
            foreach (var move in _gen.GetLegalMoves(state))
                nodes += Perft(state.ApplyMove(move), depth - 1);
            return nodes;
        }

        [Fact]
        public void Perft_Depth1_TwentyMoves()
        {
            var state = new GameState();
            Assert.Equal(20L, Perft(state, 1));
        }

        [Fact]
        public void Perft_Depth2_FourHundredNodes()
        {
            var state = new GameState();
            Assert.Equal(400L, Perft(state, 2));
        }

        [Fact]
        public void Perft_Depth3_EightThousandNineHundredTwo()
        {
            var state = new GameState();
            Assert.Equal(8902L, Perft(state, 3));
        }

        // Perft 4 = 197.281 čvorova — opciono, sporije u debug modu
        [Fact]
        public void Perft_Depth4_CorrectNodeCount()
        {
            var state = new GameState();
            Assert.Equal(197281L, Perft(state, 4));
        }
    }
}
