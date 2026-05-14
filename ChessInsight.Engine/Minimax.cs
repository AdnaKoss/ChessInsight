using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Engine
{
    /// <summary>
    /// Rezultat pretrage — najbolji potez i njegov skor.
    /// </summary>
    public class SearchResult
    {
        public Move? BestMove { get; set; }
        public int Score { get; set; }
        public int NodesSearched { get; set; }
        public bool IsBookMove { get; set; }
    }

    /// <summary>
    /// Minimax algoritam za pretraživanje stabla igre.
    /// Bijeli igrač (MAX) maksimizira skor.
    /// Crni igrač (MIN) minimizira skor.
    /// </summary>
    public class Minimax
    {
        private readonly Evaluator _evaluator = new();
        private readonly MoveGenerator _generator = new();

        // Broj pregledanih čvorova — za mjerenje performansi
        private int _nodesSearched;

        /// <summary>
        /// Pronalazi najbolji potez za trenutnog igrača.
        /// </summary>
        /// <param name="state">Trenutno stanje igre.</param>
        /// <param name="depth">Dubina pretrage (preporučeno 3-4).</param>
        public SearchResult FindBestMove(GameState state, int depth)
        {
            _nodesSearched = 0;

            var legalMoves = _generator.GetLegalMoves(state);

            if (legalMoves.Count == 0)
                return new SearchResult { Score = 0 };

            Move? bestMove = null;
            bool isMaximizing = state.CurrentPlayer == PieceColor.White;
            int bestScore = isMaximizing ? int.MinValue : int.MaxValue;

            foreach (var move in legalMoves)
            {
                var newState = state.ApplyMove(move);
                int score = Search(newState, depth - 1, !isMaximizing);

                if (isMaximizing && score > bestScore ||
                   !isMaximizing && score < bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            return new SearchResult
            {
                BestMove = bestMove,
                Score = bestScore,
                NodesSearched = _nodesSearched
            };
        }

        /// <summary>
        /// Rekurzivna Minimax pretraga.
        /// </summary>
        private int Search(GameState state, int depth, bool isMaximizing)
        {
            _nodesSearched++;

            // Bazni slučaj — dostignuta dubina ili kraj igre
            if (depth == 0)
                return _evaluator.Evaluate(state);

            var legalMoves = _generator.GetLegalMoves(state);

            // Šah-mat ili pat
            if (legalMoves.Count == 0)
            {
                if (state.IsInCheck(state.CurrentPlayer))
                    // Šah-mat — jako loše za igrača koji je mat dobio
                    return isMaximizing ? -99999 : +99999;
                else
                    // Pat — nula (remi)
                    return 0;
            }

            if (isMaximizing)
            {
                int maxScore = int.MinValue;
                foreach (var move in legalMoves)
                {
                    var newState = state.ApplyMove(move);
                    int score = Search(newState, depth - 1, false);
                    maxScore = Math.Max(maxScore, score);
                }
                return maxScore;
            }
            else
            {
                int minScore = int.MaxValue;
                foreach (var move in legalMoves)
                {
                    var newState = state.ApplyMove(move);
                    int score = Search(newState, depth - 1, true);
                    minScore = Math.Min(minScore, score);
                }
                return minScore;
            }
        }
    }
}