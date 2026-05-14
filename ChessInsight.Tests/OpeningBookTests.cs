using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using ChessInsight.Engine;
using Xunit;
using Xunit.Abstractions;

namespace ChessInsight.Tests
{
    public class OpeningBookTests
    {
        private readonly ITestOutputHelper _output;
        private readonly OpeningBook _book = new();
        private readonly MoveGenerator _gen = new();
        private readonly Evaluator _eval = new();

        public OpeningBookTests(ITestOutputHelper output) => _output = output;

        // ── Test 1 ────────────────────────────────────────────────

        [Fact]
        public void StartingPosition_RecommendsE4OrD4()
        {
            var state = new GameState();
            var seen = new HashSet<string>();

            for (int i = 0; i < 300; i++)
            {
                var m = _book.GetBookMove(state);
                Assert.NotNull(m);
                seen.Add(m!.From.ToAlgebraic() + m.To.ToAlgebraic());
            }

            _output.WriteLine($"Viđeni potezi: {string.Join(", ", seen)}");
            Assert.True(seen.Contains("e2e4") || seen.Contains("d2d4"),
                "Početna pozicija treba preporučiti e2e4 ili d2d4");
        }

        // ── Test 2 ────────────────────────────────────────────────

        [Fact]
        public void AfterE4_RecommendsE5OrC5_NotKnightFirst()
        {
            var state = new GameState();
            var e4 = _gen.GetLegalMoves(state)
                .First(m => m.From.ToAlgebraic() == "e2" && m.To.ToAlgebraic() == "e4");
            var afterE4 = state.ApplyMove(e4);

            var seen = new HashSet<string>();
            for (int i = 0; i < 500; i++)
            {
                var m = _book.GetBookMove(afterE4);
                Assert.NotNull(m);
                seen.Add(m!.From.ToAlgebraic() + m.To.ToAlgebraic());
            }

            _output.WriteLine($"Crni odgovori na e4: {string.Join(", ", seen)}");
            Assert.Contains("e7e5", seen);
            Assert.Contains("c7c5", seen);
            Assert.DoesNotContain("g8f6", seen);
        }

        // ── Test 3 ────────────────────────────────────────────────

        [Fact]
        public void AfterE4E5Nf3Nc6_RecommendsRuyLopezOrItalian()
        {
            var state = new GameState();
            state = state.ApplyMove(FindMove(state, "e2", "e4"));
            state = state.ApplyMove(FindMove(state, "e7", "e5"));
            state = state.ApplyMove(FindMove(state, "g1", "f3"));
            state = state.ApplyMove(FindMove(state, "b8", "c6"));

            var seen = new HashSet<string>();
            for (int i = 0; i < 500; i++)
            {
                var m = _book.GetBookMove(state);
                Assert.NotNull(m);
                seen.Add(m!.From.ToAlgebraic() + m.To.ToAlgebraic());
            }

            _output.WriteLine($"Bijeli odgovori na e4 e5 Sf3 Sc6: {string.Join(", ", seen)}");
            Assert.True(seen.Contains("f1b5") || seen.Contains("f1c4"),
                "Knjiga treba preporučiti Ruy Lopez (f1b5) ili Italijansku (f1c4)");
        }

        // ── Test 4 ────────────────────────────────────────────────

        [Fact]
        public void BookMove_IsAlwaysLegal()
        {
            var positions = new[]
            {
                FenParser.StartingFen,
                "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
                "rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR b KQkq d3 0 1",
                "rnbqkbnr/pppppppp/8/8/2P5/8/PP1PPPPP/RNBQKBNR b KQkq c3 0 1",
                "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2",
                "rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
                "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3",
                "rnbqkb1r/pppp1ppp/5n2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3",
                "rnbqkbnr/ppp1pppp/8/3p4/3P4/8/PPP1PPPP/RNBQKBNR w KQkq d6 0 2",
                "rnbqkb1r/pppppppp/5n2/8/3P4/8/PPP1PPPP/RNBQKBNR w KQkq - 1 2",
            };

            foreach (var fen in positions)
            {
                var state = FenParser.Parse(fen);
                var legalMoves = _gen.GetLegalMoves(state);

                for (int i = 0; i < 20; i++)
                {
                    var bookMove = _book.GetBookMove(state);
                    if (bookMove == null) break;

                    bool isLegal = legalMoves.Any(m => m.Equals(bookMove));
                    Assert.True(isLegal,
                        $"Potez {bookMove.From}{bookMove.To} nije legalan u poziciji: {fen}");
                }
            }
        }

        // ── Test 5 ────────────────────────────────────────────────

        [Fact]
        public void OpeningPrinciples_PenalizesKnightOnRim()
        {
            // Bijeli skakač na a3 — treba biti kažnjen (rub ploče)
            // Crni skakač na h6 — simetričan slučaj
            var state = FenParser.Parse("4k3/8/7n/8/8/N7/8/4K3 w - - 0 1");
            int score = _eval.Evaluate(state);

            _output.WriteLine($"Skor s konjima na rubu: {score}");
            // Oba skakača su na rubu — trebala bi biti neutralna ili malo negativna za bijelog
            // Bijeli skakač a3 → -25cp za bijelog
            // Crni skakač h6 → +25cp za bijelog (crni kažnjen)
            // Neto: ≈ 0 za oba, ali PST KnightTable penalizira rubne pozicije dodatno
            // Testiramo da bijeli skakač na a3 SÂMO kažnjava bijelog:
            var stateOnlyWhiteKnight = FenParser.Parse("4k3/8/8/8/8/N7/8/4K3 w - - 0 1");
            var stateKnightInCenter  = FenParser.Parse("4k3/8/8/8/8/4N3/8/4K3 w - - 0 1");
            int rimScore    = _eval.Evaluate(stateOnlyWhiteKnight);
            int centerScore = _eval.Evaluate(stateKnightInCenter);

            _output.WriteLine($"Skakač na rubu (a3): {rimScore}, Skakač u centru (e3): {centerScore}");
            Assert.True(rimScore < centerScore,
                $"Skakač na rubu ({rimScore}) treba imati lošiji skor od skakača u centru ({centerScore})");
        }

        // ── Test 6 ────────────────────────────────────────────────

        [Fact]
        public void OpeningPrinciples_RewardsCenter()
        {
            // Bijeli pješaci na e4 i d4 — centralna kontrola → nagrađena
            var centerState = FenParser.Parse("4k3/8/8/8/3PP3/8/8/4K3 w - - 0 1");
            // Bijeli pješaci na a4 i h4 — rubni pješaci → slabiji
            var rimState    = FenParser.Parse("4k3/8/8/8/P6P/8/8/4K3 w - - 0 1");

            int centerScore = _eval.Evaluate(centerState);
            int rimScore    = _eval.Evaluate(rimState);

            _output.WriteLine($"Centralni pješaci (d4+e4): {centerScore}, Rubni pješaci (a4+h4): {rimScore}");
            Assert.True(centerScore > rimScore,
                $"Centralni pješaci ({centerScore}) trebaju imati veći skor od rubnih ({rimScore})");
        }

        // ── Pomoćna metoda ────────────────────────────────────────

        private Move FindMove(GameState state, string from, string to) =>
            _gen.GetLegalMoves(state)
                .First(m => m.From.ToAlgebraic() == from && m.To.ToAlgebraic() == to);
    }
}
