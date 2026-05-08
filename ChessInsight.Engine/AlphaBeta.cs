using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Minimax s alfa-beta rezanjem, move orderingom i quiescence searchom.
    /// </summary>
    public class AlphaBeta
    {
        private readonly Evaluator _evaluator = new();
        private readonly MoveGenerator _generator = new();

        private int _nodesSearched;

        // ── Root pretrage ────────────────────────────────────────

        /// <summary>
        /// Iterative deepening: pretražuje dubinu 1..maxDepth i izvještava nakon svake.
        /// Progress callback se poziva na pozivajućem threadu (wrappan u IProgress).
        /// </summary>
        public List<SearchResult> FindTopMovesIterative(
            GameState state, int maxDepth, int count,
            IProgress<(int depth, List<SearchResult> results)>? progress = null)
        {
            List<SearchResult> last = new();
            for (int d = 1; d <= maxDepth; d++)
            {
                last = FindTopMoves(state, d, count);
                progress?.Report((d, last));
            }
            return last;
        }

        public List<SearchResult> FindTopMoves(GameState state, int depth, int count)
        {
            _nodesSearched = 0;

            var legalMoves = _generator.GetLegalMoves(state);
            if (legalMoves.Count == 0) return new List<SearchResult>();

            bool isMax = state.CurrentPlayer == PieceColor.White;
            var scored = new (Move move, int score)[legalMoves.Count];
            int idx = 0;

            foreach (var move in OrderMoves(legalMoves, state.Board))
            {
                var next = state.ApplyMove(move);
                int score = Search(next, depth - 1, !isMax, int.MinValue, int.MaxValue);
                scored[idx++] = (move, score);
            }

            Array.Sort(scored, 0, idx, isMax
                ? Comparer<(Move, int score)>.Create((a, b) => b.score.CompareTo(a.score))
                : Comparer<(Move, int score)>.Create((a, b) => a.score.CompareTo(b.score)));

            int totalNodes = _nodesSearched;
            var results = new List<SearchResult>(Math.Min(count, idx));
            for (int i = 0; i < Math.Min(count, idx); i++)
                results.Add(new SearchResult
                {
                    BestMove      = scored[i].move,
                    Score         = scored[i].score,
                    NodesSearched = totalNodes
                });
            return results;
        }

        public SearchResult FindBestMove(GameState state, int depth)
        {
            _nodesSearched = 0;

            var legalMoves = _generator.GetLegalMoves(state);
            if (legalMoves.Count == 0) return new SearchResult { Score = 0 };

            bool isMax = state.CurrentPlayer == PieceColor.White;
            Move? bestMove = null;
            int bestScore = isMax ? int.MinValue : int.MaxValue;
            int alpha = int.MinValue;
            int beta  = int.MaxValue;

            foreach (var move in OrderMoves(legalMoves, state.Board))
            {
                var next  = state.ApplyMove(move);
                int score = Search(next, depth - 1, !isMax, alpha, beta);

                if (isMax && score > bestScore || !isMax && score < bestScore)
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

        private int Search(GameState state, int depth, bool isMax, int alpha, int beta)
        {
            _nodesSearched++;

            if (depth == 0)
                return QSearch(state, alpha, beta, isMax);

            var moves = _generator.GetLegalMoves(state);

            if (moves.Count == 0)
            {
                if (state.IsInCheck(state.CurrentPlayer))
                    return isMax ? -99999 : +99999;
                return 0; // pat
            }

            if (isMax)
            {
                int max = int.MinValue;
                foreach (var move in OrderMoves(moves, state.Board))
                {
                    int score = Search(state.ApplyMove(move), depth - 1, false, alpha, beta);
                    max   = Math.Max(max, score);
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta) break;
                }
                return max;
            }
            else
            {
                int min = int.MaxValue;
                foreach (var move in OrderMoves(moves, state.Board))
                {
                    int score = Search(state.ApplyMove(move), depth - 1, true, alpha, beta);
                    min  = Math.Min(min, score);
                    beta = Math.Min(beta, score);
                    if (alpha >= beta) break;
                }
                return min;
            }
        }

        // ── Quiescence search ────────────────────────────────────
        // Nastavlja pretragu samo kroz hvatanja dok se ne dođe do "mirne" pozicije.
        // Sprečava horizon effect — engine ne može ignorisati hvatanje odmah iza horizonta.

        private int QSearch(GameState state, int alpha, int beta, bool isMax, int qDepth = 0)
        {
            _nodesSearched++;

            int standPat = _evaluator.Evaluate(state);

            // Sigurnosni izlaz da se spriječi beskonačna rekurzija
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

            captures = OrderMoves(captures, state.Board);

            if (isMax)
            {
                int max = standPat;
                foreach (var move in captures)
                {
                    int score = QSearch(state.ApplyMove(move), alpha, beta, false, qDepth + 1);
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
                    int score = QSearch(state.ApplyMove(move), alpha, beta, true, qDepth + 1);
                    min  = Math.Min(min, score);
                    beta = Math.Min(beta, score);
                    if (alpha >= beta) break;
                }
                return min;
            }
        }

        // ── Move ordering (MVV-LVA) ──────────────────────────────
        // Promocije prvo, zatim hvatanja (MVV-LVA), zatim mirni potezi.

        private static List<Move> OrderMoves(List<Move> moves, Board board)
        {
            // Scoring bez LINQ alokacija
            var buf = new (Move m, int score)[moves.Count];
            for (int i = 0; i < moves.Count; i++)
                buf[i] = (moves[i], MoveScore(moves[i], board));

            Array.Sort(buf, (a, b) => b.score.CompareTo(a.score));

            var result = new List<Move>(moves.Count);
            foreach (var (m, _) in buf) result.Add(m);
            return result;
        }

        private static int MoveScore(Move m, Board board)
        {
            if (m.Type == MoveType.PawnPromotion) return 30000;
            if (m.Type is MoveType.Capture or MoveType.EnPassant)
            {
                var victim   = board.GetPiece(m.To);
                var attacker = board.GetPiece(m.From);
                int victimVal   = victim   != null ? PieceVal(victim.Type)   : 100;
                int attackerVal = attacker != null ? PieceVal(attacker.Type) : 100;
                return 10000 + victimVal * 10 - attackerVal;
            }
            return 0;
        }

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
