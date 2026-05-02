using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Models;
using ChessInsight.Engine;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;

namespace ChessInsight.Tests
{
    /// <summary>
    /// Testovi koji mjere i uspoređuju performanse
    /// Minimax vs AlphaBeta algoritma.
    /// </summary>
    public class MinimaxTests
    {
        // ITestOutputHelper omogućava ispis u Test Explorer
        private readonly ITestOutputHelper _output;

        public MinimaxTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ── Minimax testovi ──────────────────────────────────────

        [Fact]
        public void Minimax_Depth2_ReturnsBestMove()
        {
            var state = new GameState();
            var minimax = new Minimax();
            var result = minimax.FindBestMove(state, depth: 2);

            Assert.NotNull(result.BestMove);
            _output.WriteLine($"Minimax dubina 2:");
            _output.WriteLine($"  Potez:   {result.BestMove}");
            _output.WriteLine($"  Skor:    {result.Score}");
            _output.WriteLine($"  Čvorovi: {result.NodesSearched}");
        }

        [Fact]
        public void Minimax_Depth3_ReturnsBestMove()
        {
            var state = new GameState();
            var minimax = new Minimax();

            var sw = Stopwatch.StartNew();
            var result = minimax.FindBestMove(state, depth: 3);
            sw.Stop();

            Assert.NotNull(result.BestMove);
            _output.WriteLine($"Minimax dubina 3:");
            _output.WriteLine($"  Potez:   {result.BestMove}");
            _output.WriteLine($"  Skor:    {result.Score}");
            _output.WriteLine($"  Čvorovi: {result.NodesSearched}");
            _output.WriteLine($"  Vrijeme: {sw.ElapsedMilliseconds}ms");
        }

        // ── AlphaBeta testovi ────────────────────────────────────

        [Fact]
        public void AlphaBeta_Depth2_ReturnsBestMove()
        {
            var state = new GameState();
            var alphaBeta = new AlphaBeta();
            var result = alphaBeta.FindBestMove(state, depth: 2);

            Assert.NotNull(result.BestMove);
            _output.WriteLine($"AlphaBeta dubina 2:");
            _output.WriteLine($"  Potez:   {result.BestMove}");
            _output.WriteLine($"  Skor:    {result.Score}");
            _output.WriteLine($"  Čvorovi: {result.NodesSearched}");
        }

        [Fact]
        public void AlphaBeta_Depth3_ReturnsBestMove()
        {
            var state = new GameState();
            var alphaBeta = new AlphaBeta();

            var sw = Stopwatch.StartNew();
            var result = alphaBeta.FindBestMove(state, depth: 3);
            sw.Stop();

            Assert.NotNull(result.BestMove);
            _output.WriteLine($"AlphaBeta dubina 3:");
            _output.WriteLine($"  Potez:   {result.BestMove}");
            _output.WriteLine($"  Skor:    {result.Score}");
            _output.WriteLine($"  Čvorovi: {result.NodesSearched}");
            _output.WriteLine($"  Vrijeme: {sw.ElapsedMilliseconds}ms");
        }

        // ── Usporedba performansi ────────────────────────────────

        [Fact]
        public void Compare_Minimax_vs_AlphaBeta_SameResult()
        {
            var state = new GameState();
            var minimax = new Minimax();
            var alphaBeta = new AlphaBeta();

            var sw1 = Stopwatch.StartNew();
            var r1 = minimax.FindBestMove(state, depth: 3);
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            var r2 = alphaBeta.FindBestMove(state, depth: 3);
            sw2.Stop();

            _output.WriteLine("══ Usporedba Minimax vs AlphaBeta (dubina 3) ══");
            _output.WriteLine($"Minimax   → čvorovi: {r1.NodesSearched,8} | vrijeme: {sw1.ElapsedMilliseconds,6}ms | potez: {r1.BestMove}");
            _output.WriteLine($"AlphaBeta → čvorovi: {r2.NodesSearched,8} | vrijeme: {sw2.ElapsedMilliseconds,6}ms | potez: {r2.BestMove}");
            _output.WriteLine($"Ubrzanje  → {(double)r1.NodesSearched / r2.NodesSearched:F1}x manje čvorova");

            // Oba algoritma moraju dati isti skor
            Assert.Equal(r1.Score, r2.Score);
        }

        // ── Mat u jednom potezu ──────────────────────────────────

        [Fact]
        public void AlphaBeta_FindsMateInOne()
        {
            // Scholars mate pozicija — bijeli može dati mat u jednom potezu
            var board = new Board();

            // Bijeli
            board.SetPiece(Square.FromAlgebraic("e1"), new King(Core.Enums.PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("h5"), new Queen(Core.Enums.PieceColor.White, Square.FromAlgebraic("h5")));
            board.SetPiece(Square.FromAlgebraic("c4"), new Bishop(Core.Enums.PieceColor.White, Square.FromAlgebraic("c4")));

            // Crni
            board.SetPiece(Square.FromAlgebraic("e8"), new King(Core.Enums.PieceColor.Black, Square.FromAlgebraic("e8")));
            board.SetPiece(Square.FromAlgebraic("e7"), new Pawn(Core.Enums.PieceColor.Black, Square.FromAlgebraic("e7")));
            board.SetPiece(Square.FromAlgebraic("d7"), new Pawn(Core.Enums.PieceColor.Black, Square.FromAlgebraic("d7")));
            board.SetPiece(Square.FromAlgebraic("f7"), new Pawn(Core.Enums.PieceColor.Black, Square.FromAlgebraic("f7")));

            var state = new GameState(board, Core.Enums.PieceColor.White);
            var alphaBeta = new AlphaBeta();
            var result = alphaBeta.FindBestMove(state, depth: 3);

            _output.WriteLine($"Mat u jednom — pronađeni potez: {result.BestMove}");
            _output.WriteLine($"Skor: {result.Score}");

            // Skor treba biti vrlo visok (mat)
            Assert.True(result.Score > 90000);
        }
    }
}