using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Minimax s alfa-beta rezanjem i četiri napredne optimizacije:
    /// Transposition Table, Killer Move Heuristic, Null Move Pruning,
    /// Aspiration Windows + Iterative Deepening.
    /// </summary>
    public class AlphaBeta
    {
        private readonly Evaluator          _evaluator   = new();
        private readonly MoveGenerator      _generator   = new();
        private readonly TranspositionTable _tt          = new();
        private readonly OpeningBook        _openingBook = new();

        private const int MaxPly    = 64;
        private const int NullMoveR = 2; // null-move redukcija dubine

        private int    _nodesSearched;
        private Move?[,] _killers = new Move?[MaxPly, 2]; // 2 killer poteza po plyu

        // ── Root pretrage ────────────────────────────────────────

        /// <summary>
        /// Iterative deepening s aspiration windows.
        /// Prozor ±50 cp; pri fail-low/high širi se 4× sve dok ne dođe do pune pretrage.
        /// </summary>
        public List<SearchResult> FindTopMovesIterative(
            GameState state, int maxDepth, int count,
            IProgress<(int depth, List<SearchResult> results)>? progress = null,
            CancellationToken cancellationToken = default,
            Dictionary<ulong, int>? gameHistory = null)
        {
            // Pripremi book poteze jednom — sortirane po težini, verifikovane kao legalne
            var bookEntries = _openingBook.GetBookEntries(state);
            var legalRoot   = _generator.GetLegalMoves(state);
            var bookMoves   = new List<(Move move, int weight)>(bookEntries.Count);
            foreach (var (uci, weight) in bookEntries.OrderByDescending(e => e.weight))
            {
                if (uci.Length < 4) continue;
                var from = Square.FromAlgebraic(uci[..2]);
                var to   = Square.FromAlgebraic(uci[2..4]);
                var m    = legalRoot.FirstOrDefault(x => x.From.Equals(from) && x.To.Equals(to));
                if (m != null) bookMoves.Add((m, weight));
            }

            _tt.Clear();
            Array.Clear(_killers, 0, _killers.Length);

            List<SearchResult> last = [];
            int prevScore = 0;

            for (int d = 1; d <= maxDepth; d++)
            {
                try
                {
                    List<SearchResult> current;

                    if (d <= 2)
                    {
                        current = FindTopMoves(state, d, count,
                                               int.MinValue, int.MaxValue,
                                               cancellationToken, gameHistory);
                    }
                    else
                    {
                        int delta = 50;
                        int lo = prevScore - delta;
                        int hi = prevScore + delta;
                        current = last;

                        while (true)
                        {
                            var attempt = FindTopMoves(state, d, count, lo, hi,
                                                       cancellationToken, gameHistory);
                            if (attempt.Count == 0) break;

                            int s = attempt[0].Score;
                            current = attempt;

                            if (s <= lo)
                            {
                                delta *= 4;
                                lo     = prevScore - delta;
                                if (lo < -90000) { lo = int.MinValue; hi = int.MaxValue; break; }
                            }
                            else if (s >= hi)
                            {
                                delta *= 4;
                                hi     = prevScore + delta;
                                if (hi > 90000)  { lo = int.MinValue; hi = int.MaxValue; break; }
                            }
                            else break;
                        }
                    }

                    if (current.Count > 0)
                    {
                        prevScore = current[0].Score;
                        last      = current;
                    }

                    // Umetni SVE book poteze kao top rezultate (sortirano po težini)
                    if (bookMoves.Count > 0)
                    {
                        int baseScore = last.Count > 0 ? last[0].Score : 0;
                        int baseNodes = last.Count > 0 ? last[0].NodesSearched : 0;

                        var bookSet = new HashSet<Move>(bookMoves.Select(b => b.move));
                        var nonBook = last.Where(r => r.BestMove != null && !bookSet.Contains(r.BestMove!)).ToList();

                        last.Clear();
                        foreach (var (m, _) in bookMoves.Take(count))
                            last.Add(new SearchResult { BestMove = m, Score = baseScore, NodesSearched = baseNodes, IsBookMove = true });
                        foreach (var r in nonBook)
                        {
                            if (last.Count >= count) break;
                            last.Add(r);
                        }
                    }

                    progress?.Report((d, last));
                }
                catch (OperationCanceledException) { break; }
            }

            return last;
        }


        public List<SearchResult> FindTopMoves(GameState state, int depth, int count,
            int rootAlpha, int rootBeta,
            CancellationToken cancellationToken = default,
            Dictionary<ulong, int>? gameHistory = null)
        {
            _nodesSearched = 0;

            var history = gameHistory != null
                ? new Dictionary<ulong, int>(gameHistory)
                : new Dictionary<ulong, int>();

            ulong rootHash = Zobrist.Compute(state);
            history.TryGetValue(rootHash, out int rootCount);
            history[rootHash] = rootCount + 1;

            var legalMoves = _generator.GetLegalMoves(state);
            if (legalMoves.Count == 0) return [];

            bool isMax = state.CurrentPlayer == PieceColor.White;
            var scored = new (Move move, int score)[legalMoves.Count];
            int idx = 0;

            _tt.TryProbe(rootHash, depth, rootAlpha, rootBeta, out _, out Move? ttMove);

            foreach (var move in OrderMoves(legalMoves, state.Board, ttMove, null, 0))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var next  = state.ApplyMove(move);
                int score = Search(next, depth - 1, !isMax,
                                   rootAlpha, rootBeta,
                                   cancellationToken, history, 1);
                scored[idx++] = (move, score);
            }

            Array.Sort(scored, 0, idx, isMax
                ? Comparer<(Move, int score)>.Create((a, b) => b.score.CompareTo(a.score))
                : Comparer<(Move, int score)>.Create((a, b) => a.score.CompareTo(b.score)));

            int totalNodes = _nodesSearched;
            var results    = new List<SearchResult>(Math.Min(count, idx));
            for (int i = 0; i < Math.Min(count, idx); i++)
                results.Add(new SearchResult
                {
                    BestMove      = scored[i].move,
                    Score         = scored[i].score,
                    NodesSearched = totalNodes
                });
            return results;
        }

        public SearchResult FindBestMove(GameState state, int depth,
            CancellationToken cancellationToken = default,
            Dictionary<ulong, int>? gameHistory = null)
        {
            var bookMove = _openingBook.GetBookMove(state);
            if (bookMove != null)
                return new SearchResult { BestMove = bookMove, Score = 0, NodesSearched = 0, IsBookMove = true };

            _nodesSearched = 0;
            Array.Clear(_killers, 0, _killers.Length);

            var history = gameHistory != null
                ? new Dictionary<ulong, int>(gameHistory)
                : new Dictionary<ulong, int>();

            ulong rootHash = Zobrist.Compute(state);
            history.TryGetValue(rootHash, out int rootCount);
            history[rootHash] = rootCount + 1;

            var legalMoves = _generator.GetLegalMoves(state);
            if (legalMoves.Count == 0) return new SearchResult { Score = 0 };

            bool isMax    = state.CurrentPlayer == PieceColor.White;
            Move? bestMove = null;
            int bestScore  = isMax ? int.MinValue : int.MaxValue;
            int alpha      = int.MinValue;
            int beta       = int.MaxValue;

            _tt.TryProbe(rootHash, depth, alpha, beta, out _, out Move? ttMove);

            foreach (var move in OrderMoves(legalMoves, state.Board, ttMove, null, 0))
            {
                var next     = state.ApplyMove(move);
                var histCopy = new Dictionary<ulong, int>(history);
                int score    = Search(next, depth - 1, !isMax, alpha, beta,
                                      cancellationToken, histCopy, 1);

                if (isMax ? score > bestScore : score < bestScore)
                {
                    bestScore = score;
                    bestMove  = move;
                }
                if (isMax) alpha = Math.Max(alpha, bestScore);
                else       beta  = Math.Min(beta,  bestScore);
            }

            return new SearchResult
            {
                BestMove      = bestMove,
                Score         = bestScore,
                NodesSearched = _nodesSearched
            };
        }

        // ── Rekurzivna pretraga ──────────────────────────────────

        private int Search(GameState state, int depth, bool isMax, int alpha, int beta,
            CancellationToken cancellationToken, Dictionary<ulong, int> history, int ply)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Detekcija trostrukog ponavljanja
            ulong hash = Zobrist.Compute(state);
            history.TryGetValue(hash, out int repCount);
            if (repCount >= 2) return 0;

            _nodesSearched++;

            // ── Transposition Table probe ────────────────────────
            if (_tt.TryProbe(hash, depth, alpha, beta, out int ttScore, out Move? ttMove))
                return ttScore;

            if (depth == 0)
                return QSearch(state, alpha, beta, isMax, cancellationToken: cancellationToken);

            var moves = _generator.GetLegalMoves(state);

            if (moves.Count == 0)
            {
                if (state.IsInCheck(state.CurrentPlayer))
                    return isMax ? -99999 : +99999;
                return 0; // pat
            }

            bool inCheck = state.IsInCheck(state.CurrentPlayer);

            // ── Null Move Pruning ────────────────────────────────
            // Preskačemo vlastiti potez — ako je pozicija i dalje dobra, pruning je siguran.
            // Isključeno: pri šahu, u endgameu (zugzwang) i na ply 0 (root).
            if (!inCheck && depth >= 3 && ply > 0 && !IsEndgame(state))
            {
                int R         = depth > 6 ? 3 : NullMoveR;
                var nullState = state.ApplyNullMove();

                if (isMax)
                {
                    int nullScore = Search(nullState, depth - 1 - R, false,
                                          beta - 1, beta, cancellationToken, history, ply + 1);
                    if (nullScore >= beta) return beta;
                }
                else
                {
                    int nullScore = Search(nullState, depth - 1 - R, true,
                                          alpha, alpha + 1, cancellationToken, history, ply + 1);
                    if (nullScore <= alpha) return alpha;
                }
            }

            // ── Futility pruning setup ───────────────────────────
            // Statička evaluacija: izračunaj samo za čvorove blizu horizonta
            int staticEval = (!inCheck && depth <= 2) ? _evaluator.Evaluate(state) : 0;

            // Dodaj poziciju u historiju (backtrack nakon pretrage)
            history[hash] = repCount + 1;

            int origAlpha     = alpha;
            int origBeta      = beta;
            Move? bestMove    = null;
            int best          = isMax ? int.MinValue : int.MaxValue;
            bool hadCutoff    = false;
            int moveIdx       = 0; // pozicija u listi (za LMR)
            int searchedCount = 0; // broj stvarno pretraženih poteza (za PVS)

            foreach (var move in OrderMoves(moves, state.Board, ttMove, _killers, ply))
            {
                bool isQuiet = move.Type == MoveType.Normal;

                // ── Futility pruning ─────────────────────────────
                // Preskačemo tihe poteze kad statička evaluacija + margin ne može poboljšati alfa/betu.
                // Nikad ne preskačemo prvi potez (garantujemo barem jednu pretragu).
                if (searchedCount > 0 && !inCheck && depth <= 2 && isQuiet)
                {
                    int margin = depth == 1 ? 200 : 500;
                    if (isMax  && staticEval + margin <= alpha) { moveIdx++; continue; }
                    if (!isMax && staticEval - margin >= beta)  { moveIdx++; continue; }
                }

                var nextState = state.ApplyMove(move);
                int score;

                if (searchedCount == 0)
                {
                    // ── PV potez: puna dubina, puni prozor ──────
                    score = Search(nextState, depth - 1, !isMax, alpha, beta,
                                   cancellationToken, history, ply + 1);
                }
                else if (isMax)
                {
                    // ── LMR + PVS (maximizer) ────────────────────
                    // Late Move Reduction: smanji dubinu za kasne tihe poteze
                    int reduction = (isQuiet && depth >= 3 && moveIdx >= 4 && !inCheck)
                        ? (moveIdx >= 8 ? 2 : 1) : 0;
                    int lmrDepth = Math.Max(0, depth - 1 - reduction);

                    // PVS: null-window pretraga na (eventualno) smanjenoj dubini
                    score = Search(nextState, lmrDepth, false, alpha, alpha + 1,
                                   cancellationToken, history, ply + 1);

                    // Fail-high → re-search s punom dubinom i punim prozorom
                    if (score > alpha)
                        score = Search(nextState, depth - 1, false, alpha, beta,
                                       cancellationToken, history, ply + 1);
                }
                else
                {
                    // ── LMR + PVS (minimizer) ────────────────────
                    int reduction = (isQuiet && depth >= 3 && moveIdx >= 4 && !inCheck)
                        ? (moveIdx >= 8 ? 2 : 1) : 0;
                    int lmrDepth = Math.Max(0, depth - 1 - reduction);

                    score = Search(nextState, lmrDepth, true, beta - 1, beta,
                                   cancellationToken, history, ply + 1);

                    // Fail-low → re-search s punom dubinom i punim prozorom
                    if (score < beta)
                        score = Search(nextState, depth - 1, true, alpha, beta,
                                       cancellationToken, history, ply + 1);
                }

                moveIdx++;
                searchedCount++;

                if (isMax ? score > best : score < best) { best = score; bestMove = move; }
                if (isMax) alpha = Math.Max(alpha, best);
                else       beta  = Math.Min(beta,  best);

                if (alpha >= beta)
                {
                    if (isQuiet) StoreKiller(move, ply);
                    hadCutoff = true;
                    break;
                }
            }

            // Backtrack historije
            history[hash] = repCount;
            if (repCount == 0) history.Remove(hash);

            // Svi potezi preskočeni futility pruningom — vrati graničnu vrijednost
            if (searchedCount == 0)
                return isMax ? alpha : beta;

            // ── Transposition Table store ────────────────────────
            TtFlag flag;
            if (hadCutoff)
                flag = isMax ? TtFlag.LowerBound : TtFlag.UpperBound;
            else if (isMax ? best > origAlpha : best < origBeta)
                flag = TtFlag.Exact;
            else
                flag = isMax ? TtFlag.UpperBound : TtFlag.LowerBound;

            _tt.Store(hash, depth, best, flag, bestMove);
            return best;
        }

        // ── Quiescence search ────────────────────────────────────

        private int QSearch(GameState state, int alpha, int beta, bool isMax, int qDepth = 0,
            CancellationToken cancellationToken = default)
        {
            _nodesSearched++;

            int standPat = _evaluator.Evaluate(state);
            if (qDepth >= 6) return standPat;

            if (isMax)
            {
                if (standPat >= beta) return beta;
                alpha = Math.Max(alpha, standPat);
            }
            else
            {
                if (standPat <= alpha) return alpha;
                beta = Math.Min(beta, standPat);
            }

            var captures = _generator.GetLegalMoves(state)
                .Where(m => m.Type is MoveType.Capture or MoveType.EnPassant or MoveType.PawnPromotion)
                .ToList();

            if (captures.Count == 0) return standPat;

            captures = OrderMoves(captures, state.Board, null, null, 0);

            if (isMax)
            {
                int max = standPat;
                foreach (var move in captures)
                {
                    int score = QSearch(state.ApplyMove(move), alpha, beta, false, qDepth + 1, cancellationToken);
                    max   = Math.Max(max, score);
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta) break;
                }
                return max;
            }
            else
            {
                int min = standPat;
                foreach (var move in captures)
                {
                    int score = QSearch(state.ApplyMove(move), alpha, beta, true, qDepth + 1, cancellationToken);
                    min  = Math.Min(min, score);
                    beta = Math.Min(beta, score);
                    if (alpha >= beta) break;
                }
                return min;
            }
        }

        // ── Killer Move heuristika ───────────────────────────────

        private void StoreKiller(Move move, int ply)
        {
            if (ply >= MaxPly) return;
            // Ne dupliciraj isti killer
            if (!SameMove(_killers[ply, 0], move))
            {
                _killers[ply, 1] = _killers[ply, 0];
                _killers[ply, 0] = move;
            }
        }

        private static bool SameMove(Move? a, Move b) =>
            a != null && a.Equals(b);

        // ── Move ordering: TT > Promocija > MVV-LVA > Killer > Tihi ──

        private static List<Move> OrderMoves(List<Move> moves, Board board,
            Move? ttMove, Move?[,]? killers, int ply)
        {
            var buf = new (Move m, int score)[moves.Count];
            for (int i = 0; i < moves.Count; i++)
                buf[i] = (moves[i], MoveScore(moves[i], board, ttMove, killers, ply));

            Array.Sort(buf, (a, b) => b.score.CompareTo(a.score));

            var result = new List<Move>(moves.Count);
            foreach (var (m, _) in buf) result.Add(m);
            return result;
        }

        private static int MoveScore(Move m, Board board, Move? ttMove,
            Move?[,]? killers, int ply)
        {
            if (ttMove != null && m.Equals(ttMove)) return 40000;

            if (m.Type == MoveType.PawnPromotion) return 30000;

            if (m.Type is MoveType.Capture or MoveType.EnPassant)
            {
                var victim      = board.GetPiece(m.To);
                var attacker    = board.GetPiece(m.From);
                int victimVal   = victim   != null ? PieceVal(victim.Type)   : 100;
                int attackerVal = attacker != null ? PieceVal(attacker.Type) : 100;
                return 10000 + victimVal * 10 - attackerVal;
            }

            if (killers != null && ply < MaxPly)
            {
                if (SameMove(killers[ply, 0], m)) return 9000;
                if (SameMove(killers[ply, 1], m)) return 8000;
            }

            return 0;
        }

        // ── Pomoćne funkcije ─────────────────────────────────────

        // Endgame: ni bijeli ni crni nemaju damu → veća opasnost od zugzwanga
        private static bool IsEndgame(GameState state) =>
            !state.Board.GetPieces(PieceColor.White).Any(p => p.Type == PieceType.Queen) &&
            !state.Board.GetPieces(PieceColor.Black).Any(p => p.Type == PieceType.Queen);

        private static int PieceVal(PieceType t) => t switch
        {
            PieceType.Pawn   => 100,
            PieceType.Knight => 320,
            PieceType.Bishop => 330,
            PieceType.Rook   => 500,
            PieceType.Queen  => 900,
            PieceType.King   => 20000,
            _                => 0
        };
    }
}
