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
    /// Minimax s alfa-beta rezanjem.
    /// Alpha = minimum koji MAX garantovano može postići.
    /// Beta  = maximum koji MIN garantovano može postići.
    /// Kada alpha >= beta — granu preskačemo (pruning).
    /// </summary>
    public class AlphaBeta
    {
        private readonly Evaluator _evaluator = new();
        private readonly MoveGenerator _generator = new();

        private int _nodesSearched;

        /// <summary>
        /// Pronalazi najbolji potez koristeći alfa-beta rezanje.
        /// </summary>
        public SearchResult FindBestMove(GameState state, int depth)
        {
            _nodesSearched = 0;

            var legalMoves = _generator.GetLegalMoves(state);

            if (legalMoves.Count == 0)
                return new SearchResult { Score = 0 };

            Move? bestMove = null;
            bool isMaximizing = state.CurrentPlayer == PieceColor.White;
            int bestScore = isMaximizing ? int.MinValue : int.MaxValue;
            int alpha = int.MinValue;
            int beta = int.MaxValue;

            foreach (var move in legalMoves)
            {
                var newState = state.ApplyMove(move);
                int score = Search(newState, depth - 1, !isMaximizing, alpha, beta);

                if (isMaximizing && score > bestScore ||
                   !isMaximizing && score < bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                // Ažuriraj alpha/beta na najvišem nivou
                if (isMaximizing) alpha = Math.Max(alpha, bestScore);
                else beta = Math.Min(beta, bestScore);
            }

            return new SearchResult
            {
                BestMove = bestMove,
                Score = bestScore,
                NodesSearched = _nodesSearched
            };
        }

        /// <summary>
        /// Rekurzivna pretraga s alfa-beta rezanjem.
        /// </summary>
        private int Search(GameState state, int depth, bool isMaximizing,
                           int alpha, int beta)
        {
            _nodesSearched++;

            if (depth == 0)
                return _evaluator.Evaluate(state);

            var legalMoves = _generator.GetLegalMoves(state);

            if (legalMoves.Count == 0)
            {
                if (state.IsInCheck(state.CurrentPlayer))
                    return isMaximizing ? -99999 : +99999;
                else
                    return 0;
            }

            if (isMaximizing)
            {
                int maxScore = int.MinValue;

                foreach (var move in legalMoves)
                {
                    var newState = state.ApplyMove(move);
                    int score = Search(newState, depth - 1, false, alpha, beta);
                    maxScore = Math.Max(maxScore, score);
                    alpha = Math.Max(alpha, score);

                    // Beta rezanje — MIN već ima bolju opciju, preskoči
                    if (alpha >= beta) break;
                }
                return maxScore;
            }
            else
            {
                int minScore = int.MaxValue;

                foreach (var move in legalMoves)
                {
                    var newState = state.ApplyMove(move);
                    int score = Search(newState, depth - 1, true, alpha, beta);
                    minScore = Math.Min(minScore, score);
                    beta = Math.Min(beta, score);

                    // Alpha rezanje — MAX već ima bolju opciju, preskoči
                    if (alpha >= beta) break;
                }
                return minScore;
            }
        }
    }
}