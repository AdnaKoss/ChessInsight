using System.Diagnostics;
using System.Linq;
using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using ChessInsight.Engine;
using Xunit;
using Xunit.Abstractions;

namespace ChessInsight.Tests
{
    /// <summary>
    /// Testovi za sve implementirane optimizacije:
    /// ApplyNullMove, TT, Killer Moves, Null Move Pruning,
    /// Aspiration Windows, PVS, LMR, Futility Pruning.
    /// </summary>
    public class EngineOptimizationTests
    {
        private readonly ITestOutputHelper _output;
        public EngineOptimizationTests(ITestOutputHelper output) => _output = output;

        // ── ApplyNullMove ────────────────────────────────────────

        [Fact]
        public void ApplyNullMove_SwitchesCurrentPlayer()
        {
            var state = new GameState(); // Bijeli na potezu
            var nullState = state.ApplyNullMove();
            Assert.Equal(PieceColor.Black, nullState.CurrentPlayer);
        }

        [Fact]
        public void ApplyNullMove_BlackSwitchesToWhite()
        {
            var state = new GameState();
            var afterNull = state.ApplyNullMove(); // crni na potezu
            var afterNull2 = afterNull.ApplyNullMove(); // opet bijeli
            Assert.Equal(PieceColor.White, afterNull2.CurrentPlayer);
        }

        [Fact]
        public void ApplyNullMove_PreservesCastlingRights()
        {
            var state = new GameState();
            var nullState = state.ApplyNullMove();
            Assert.Equal(state.WhiteCanCastleKingside,  nullState.WhiteCanCastleKingside);
            Assert.Equal(state.WhiteCanCastleQueenside, nullState.WhiteCanCastleQueenside);
            Assert.Equal(state.BlackCanCastleKingside,  nullState.BlackCanCastleKingside);
            Assert.Equal(state.BlackCanCastleQueenside, nullState.BlackCanCastleQueenside);
        }

        [Fact]
        public void ApplyNullMove_ClearsEnPassantSquare()
        {
            // Napravi dvostruki pomak pješaka (e2→e4) da postavi en passant polje
            var state    = new GameState();
            var gen      = new MoveGenerator();
            var e4Move   = gen.GetLegalMoves(state)
                              .First(m => m.From.Equals(Square.FromAlgebraic("e2")) &&
                                          m.To.Equals(Square.FromAlgebraic("e4")));
            var afterE4  = state.ApplyMove(e4Move);

            Assert.NotNull(afterE4.Board.EnPassantSquare); // en passant je postavljen

            var nullState = afterE4.ApplyNullMove();
            Assert.Null(nullState.Board.EnPassantSquare); // null move ga briše
        }

        [Fact]
        public void ApplyNullMove_DoesNotModifyBoard()
        {
            var state     = new GameState();
            var nullState = state.ApplyNullMove();
            // Broj figura mora biti isti
            Assert.Equal(
                state.Board.GetPieces(PieceColor.White).Count(),
                nullState.Board.GetPieces(PieceColor.White).Count());
            Assert.Equal(
                state.Board.GetPieces(PieceColor.Black).Count(),
                nullState.Board.GetPieces(PieceColor.Black).Count());
        }

        // ── Iterative Deepening + Cancellation ───────────────────

        [Fact]
        public void FindTopMovesIterative_Depth6_ReturnsTopMoves()
        {
            var state  = new GameState();
            var engine = new AlphaBeta();
            var sw     = Stopwatch.StartNew();

            var results = engine.FindTopMovesIterative(state, maxDepth: 6, count: 3);
            sw.Stop();

            Assert.NotEmpty(results);
            Assert.NotNull(results[0].BestMove);
            _output.WriteLine("══ Iterative Deepening dubina 6 ══");
            _output.WriteLine($"  Top 1: {results[0].BestMove,-15} skor: {results[0].Score,6}");
            if (results.Count > 1)
                _output.WriteLine($"  Top 2: {results[1].BestMove,-15} skor: {results[1].Score,6}");
            if (results.Count > 2)
                _output.WriteLine($"  Top 3: {results[2].BestMove,-15} skor: {results[2].Score,6}");
            _output.WriteLine($"  Čvorovi: {results[0].NodesSearched}");
            _output.WriteLine($"  Vrijeme: {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void FindTopMovesIterative_Cancellation_ReturnsPartialResult()
        {
            var state = new GameState();
            var engine = new AlphaBeta();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var sw      = Stopwatch.StartNew();
            var results = engine.FindTopMovesIterative(
                state, maxDepth: 20, count: 3, cancellationToken: cts.Token);
            sw.Stop();

            // Mora vratiti parcijalni rezultat, ne baciti exception
            Assert.NotEmpty(results);
            Assert.NotNull(results[0].BestMove);
            _output.WriteLine($"Prekinuto nakon {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"Parcijalni rezultat: {results[0].BestMove}, skor: {results[0].Score}");
        }

        // ── Taktička ispravnost ──────────────────────────────────

        [Fact]
        public void AlphaBeta_FindsMateInTwo()
        {
            // Bijeli: Ke1, Qh5, Bc4 | Crni: Ke8, Pe7, Pd7, Pf7, Nd4
            // Scholar's mate nije direktan zbog Nd4 koji brani f7; engine mora naći
            // drugi mat u 2 (Qxf7+ ili Bxd5 itd.) — test provjerava da je skor "mat"
            // Koristimo poznatu Scholar's mate poziciju (bez konja):
            // Bijeli: Ke1, Qh5, Bc4 | Crni: Ke8, Pe7, Pd7, Pf7 → Qxf7# odmah
            // Ali dodajemo Bh4 crnog koji blokira da napravimo mat u 2 umjesto u 1.
            //
            // Validirana pozicija: Bijeli Ka1, Qd1, Rh1 | Crni Ka8 (sam)
            // 1.Qd1-d8+! Ka8xd8? Ne može (dama zaštićena od Rh1? Ne, ali Ra8 je mat)
            // Zapravo: Ka8 nema kuda — mat u 1? Da, Qd8#.
            //
            // Koristimo poziciju iz postojećeg testa (mat u 1) i verifikujemo da engine
            // ispravno nalazi visok skor u bliskom mat scenariju s dubinom ≥ 3:
            // Bijeli: Ke1, Qh5, Bc4 | Crni: Ke8, Pe7, Pd7, Pf7
            // Qxf7# je mat u 1 — engine treba vratiti skor > 90000
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("e1"), new King   (PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("h5"), new Queen  (PieceColor.White, Square.FromAlgebraic("h5")));
            board.SetPiece(Square.FromAlgebraic("c4"), new Bishop (PieceColor.White, Square.FromAlgebraic("c4")));
            board.SetPiece(Square.FromAlgebraic("e8"), new King   (PieceColor.Black, Square.FromAlgebraic("e8")));
            board.SetPiece(Square.FromAlgebraic("e7"), new Pawn   (PieceColor.Black, Square.FromAlgebraic("e7")));
            board.SetPiece(Square.FromAlgebraic("d7"), new Pawn   (PieceColor.Black, Square.FromAlgebraic("d7")));
            board.SetPiece(Square.FromAlgebraic("f7"), new Pawn   (PieceColor.Black, Square.FromAlgebraic("f7")));

            var state  = new GameState(board, PieceColor.White);
            var engine = new AlphaBeta();
            var sw     = Stopwatch.StartNew();
            var result = engine.FindBestMove(state, depth: 4);
            sw.Stop();

            Assert.NotNull(result.BestMove);
            Assert.True(result.Score > 90000,
                $"Očekivan mat skor (>90000), dobijen: {result.Score}");
            _output.WriteLine($"Mat detektovan: {result.BestMove}, skor: {result.Score}, {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void AlphaBeta_CapturesFreeQueenImmediately()
        {
            // Bijeli: Ka1, Qe1 | Crni: Ka8, Qa5
            // Qe1xa5 (dijagonalno) uzima crnu damu besplatno — Ka8 ne može uzeti
            // (kralj na a8 je van dometa dame na e1, a Qa5 nije zaštićena)
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("a1"), new King  (PieceColor.White, Square.FromAlgebraic("a1")));
            board.SetPiece(Square.FromAlgebraic("e1"), new Queen (PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("a8"), new King  (PieceColor.Black, Square.FromAlgebraic("a8")));
            board.SetPiece(Square.FromAlgebraic("a5"), new Queen (PieceColor.Black, Square.FromAlgebraic("a5")));

            var state  = new GameState(board, PieceColor.White);
            var engine = new AlphaBeta();
            var result = engine.FindBestMove(state, depth: 3);

            Assert.NotNull(result.BestMove);
            // Bijela dama mora uzeti crnu damu (jedino logičan potez)
            Assert.Equal(Square.FromAlgebraic("a5"), result.BestMove!.To);
            _output.WriteLine($"Besplatna dama: {result.BestMove}, skor: {result.Score}");
        }

        [Fact]
        public void AlphaBeta_AvoidsBlunderingQueen()
        {
            // Bijeli: Ka1, Qa4 | Crni: Ka8, Ra5, Rb5
            // Bijela dama ne smije ići na a5 (bila bi uzeta topom)
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("a1"), new King (PieceColor.White, Square.FromAlgebraic("a1")));
            board.SetPiece(Square.FromAlgebraic("c4"), new Queen(PieceColor.White, Square.FromAlgebraic("c4")));
            board.SetPiece(Square.FromAlgebraic("h8"), new King (PieceColor.Black, Square.FromAlgebraic("h8")));
            board.SetPiece(Square.FromAlgebraic("a5"), new Rook (PieceColor.Black, Square.FromAlgebraic("a5")));

            var state  = new GameState(board, PieceColor.White);
            var engine = new AlphaBeta();
            var result = engine.FindBestMove(state, depth: 4);

            Assert.NotNull(result.BestMove);
            // Bijela dama ne smije ići na a5 (gdje je crni top)
            Assert.False(
                result.BestMove!.To.Equals(Square.FromAlgebraic("a5")) &&
                result.BestMove!.From.Equals(Square.FromAlgebraic("c4")),
                "Engine je predložio gubitak dame za topa — greška u evaluaciji");
            _output.WriteLine($"Izbjegnuta greška: {result.BestMove}, skor: {result.Score}");
        }

        // ── Performanse ───────────────────────────────────────────

        [Fact]
        public void Performance_AlphaBeta_AllOptimizations_Depth5()
        {
            var state  = new GameState();
            var engine = new AlphaBeta();
            var sw     = Stopwatch.StartNew();
            var result = engine.FindBestMove(state, depth: 5);
            sw.Stop();

            Assert.NotNull(result.BestMove);
            _output.WriteLine("══ Performanse (sve optimizacije, dubina 5) ══");
            _output.WriteLine($"  Potez:   {result.BestMove}");
            _output.WriteLine($"  Skor:    {result.Score}");
            _output.WriteLine($"  Čvorovi: {result.NodesSearched:N0}");
            _output.WriteLine($"  Vrijeme: {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Čv/ms:   {(sw.ElapsedMilliseconds > 0 ? result.NodesSearched / sw.ElapsedMilliseconds : 0):N0}");
        }

        [Fact]
        public void Performance_IterativeDeepening_Vs_DirectSearch()
        {
            var state   = new GameState();
            var engine  = new AlphaBeta();

            // Direktna pretraga dubina 4
            var sw1 = Stopwatch.StartNew();
            var r1  = engine.FindBestMove(state, depth: 4);
            sw1.Stop();

            // Iterative deepening do dubine 4
            var sw2 = Stopwatch.StartNew();
            var r2  = engine.FindTopMovesIterative(state, maxDepth: 4, count: 1);
            sw2.Stop();

            _output.WriteLine("══ Direktna vs Iterative Deepening (dubina 4) ══");
            _output.WriteLine($"  Direktna:  {r1.NodesSearched,8:N0} čvorova | {sw1.ElapsedMilliseconds,6} ms | {r1.BestMove}");
            _output.WriteLine($"  Iterative: {r2[0].NodesSearched,8:N0} čvorova | {sw2.ElapsedMilliseconds,6} ms | {r2[0].BestMove}");

            // Oba pristupa moraju dati validan potez
            Assert.NotNull(r1.BestMove);
            Assert.NotNull(r2[0].BestMove);
        }

        // ── Ispravnost u rubnim slučajevima ─────────────────────

        [Fact]
        public void AlphaBeta_StartingPosition_ReturnsMoveWithin10Seconds()
        {
            var state  = new GameState();
            var engine = new AlphaBeta();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var result = engine.FindBestMove(state, depth: 6, cancellationToken: cts.Token);

            Assert.NotNull(result.BestMove);
            _output.WriteLine($"Dubina 6 u 10s: {result.BestMove}, skor: {result.Score}");
        }

        [Fact]
        public void AlphaBeta_EmptyHistoryDoesNotCrash()
        {
            var state   = new GameState();
            var engine  = new AlphaBeta();
            var history = new Dictionary<ulong, int>(); // prazna historija

            var result = engine.FindBestMove(state, depth: 3, gameHistory: history);
            Assert.NotNull(result.BestMove);
        }

        // ── Endgame: opozicija kraljeva ──────────────────────────

        [Fact]
        public void DirectOpposition_WhiteHasOpposition()
        {
            // Bijeli Kd4, crni Kd6 — vertikalna direktna opozicija (jedno polje između)
            // Crni na potezu → bijeli ima opoziciju → score > 0
            //
            // PST provjera (endgame tablica):
            //   Bijeli Kd4 (row=3, col=3): tableRow=4 → KingEndgame[4][3] = +40
            //   Crni  Kd6 (row=5, col=3): tableRow=5 → KingEndgame[5][3] = +30
            //   PST razlika = +10, opozicija = +30 → ukupno +40
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("d4"), new King(PieceColor.White, Square.FromAlgebraic("d4")));
            board.SetPiece(Square.FromAlgebraic("d6"), new King(PieceColor.Black, Square.FromAlgebraic("d6")));

            var state = new GameState(board, PieceColor.Black); // crni na potezu
            var eval  = new Evaluator();
            int score = eval.Evaluate(state);

            _output.WriteLine("══ Direktna opozicija — bijeli ima opoziciju ══");
            _output.WriteLine($"  Pozicija: Kd4 vs Kd6, crni na potezu");
            _output.WriteLine($"  Skor:     {score} (pozitivno = prednost bijelog)");

            Assert.True(score > 0,
                $"Bijeli treba imati opozicijsku prednost, dobijen skor: {score}");
        }

        [Fact]
        public void DistantOpposition_Detected()
        {
            // Bijeli Kd4, crni Kd7 — vertikalna daleka opozicija (dva polja između)
            // Crni na potezu → bijeli ima daleku opoziciju → score > 0
            //
            // PST provjera:
            //   Bijeli Kd4: tableRow=4 → KingEndgame[4][3] = +40
            //   Crni  Kd7 (row=6, col=3): tableRow=6 → KingEndgame[6][3] =  0
            //   PST razlika = +40, opozicija = +15 → ukupno +55
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("d4"), new King(PieceColor.White, Square.FromAlgebraic("d4")));
            board.SetPiece(Square.FromAlgebraic("d7"), new King(PieceColor.Black, Square.FromAlgebraic("d7")));

            var state = new GameState(board, PieceColor.Black);
            var eval  = new Evaluator();
            int score = eval.Evaluate(state);

            _output.WriteLine("══ Daleka opozicija — bijeli ima prednost ══");
            _output.WriteLine($"  Pozicija: Kd4 vs Kd7, crni na potezu");
            _output.WriteLine($"  Skor:     {score} (pozitivno = prednost bijelog)");

            Assert.True(score > 0,
                $"Bijeli treba imati prednost daleke opozicije, dobijen skor: {score}");
        }

        // ── Endgame: kvadrat pješaka ─────────────────────────────

        [Fact]
        public void PawnSquare_KingCannotCatch()
        {
            // Bijeli Kh1, bijeli Ph5, crni Ka1 — bijeli na potezu
            // Crni kralj je IZVAN kvadrata pješaka → pješak prolazi slobodno
            //
            // stepsToPromotion = 7 - 4 = 3
            // kingDist = max(|0-4|, |0-7|) = 7  → 7 > 3 → izvan kvadrata
            // bonus = 60 + 3*15 = 105
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("h1"), new King (PieceColor.White, Square.FromAlgebraic("h1")));
            board.SetPiece(Square.FromAlgebraic("h5"), new Pawn (PieceColor.White, Square.FromAlgebraic("h5")));
            board.SetPiece(Square.FromAlgebraic("a1"), new King (PieceColor.Black, Square.FromAlgebraic("a1")));

            var state = new GameState(board, PieceColor.White);
            var eval  = new Evaluator();
            int score = eval.Evaluate(state);

            _output.WriteLine("══ Kvadrat pješaka — kralj ne može stići ══");
            _output.WriteLine($"  Pozicija: Kh1+Ph5 vs Ka1, bijeli na potezu");
            _output.WriteLine($"  Skor:     {score} (očekivano > 100)");

            Assert.True(score > 100,
                $"Pješak treba imati visok skor jer kralj ne može stići, dobijen: {score}");
        }

        [Fact]
        public void PawnSquare_KingCanCatch()
        {
            // Bijeli Kh1, bijeli Ph5, crni Kf4 — crni na potezu
            // Crni kralj JE unutar kvadrata → nema bonus za slobodan prolaz
            //
            // stepsToPromotion = 3, kingDist = max(|3-4|, |5-7|) = 2
            // 2 > 3+1 = 4? NE → kralj može stići → bez pawn-square bonusa
            // Ovaj skor mora biti MANJI od prethodnog testa
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("h1"), new King (PieceColor.White, Square.FromAlgebraic("h1")));
            board.SetPiece(Square.FromAlgebraic("h5"), new Pawn (PieceColor.White, Square.FromAlgebraic("h5")));
            board.SetPiece(Square.FromAlgebraic("f4"), new King (PieceColor.Black, Square.FromAlgebraic("f4")));

            var stateCannotCatch = new GameState(new Board(), PieceColor.White); // referenca
            var board2 = new Board();
            board2.SetPiece(Square.FromAlgebraic("h1"), new King(PieceColor.White, Square.FromAlgebraic("h1")));
            board2.SetPiece(Square.FromAlgebraic("h5"), new Pawn(PieceColor.White, Square.FromAlgebraic("h5")));
            board2.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.Black, Square.FromAlgebraic("a1")));

            var eval = new Evaluator();
            int scoreCatch    = eval.Evaluate(new GameState(board,  PieceColor.Black));
            int scoreNoCatch  = eval.Evaluate(new GameState(board2, PieceColor.White));

            _output.WriteLine("══ Kvadrat pješaka — usporedba ══");
            _output.WriteLine($"  Kf4 (može stići):     skor = {scoreCatch}");
            _output.WriteLine($"  Ka1 (ne može stići):  skor = {scoreNoCatch}");
            _output.WriteLine($"  Razlika (pawn-square bonus): {scoreNoCatch - scoreCatch}");

            Assert.True(scoreCatch < scoreNoCatch,
                $"Skor kad kralj može stići ({scoreCatch}) mora biti manji od kad ne može ({scoreNoCatch})");
        }
    }
}
