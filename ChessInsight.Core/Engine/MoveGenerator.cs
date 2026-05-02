using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;

namespace ChessInsight.Core.Engine
{
    /// <summary>
    /// Generiše sve legalne poteze za trenutnu poziciju.
    /// Razlika između pseudo-legalnih i legalnih poteza:
    /// — Pseudo-legalni: ispravni po kretanju figure
    /// — Legalni: pseudo-legalni koji ne ostavljaju kralja u šahu
    /// </summary>
    public class MoveGenerator
    {
        /// <summary>
        /// Vraća sve legalne poteze za trenutnog igrača.
        /// </summary>
        public List<Move> GetLegalMoves(GameState state)
        {
            var legalMoves = new List<Move>();
            var pieces = state.Board.GetPieces(state.CurrentPlayer).ToList();

            foreach (var piece in pieces)
            {
                var pseudoMoves = piece.GetPseudoLegalMoves(state.Board);

                foreach (var move in pseudoMoves)
                {
                    // Filtriraj rokadu — posebna provjera
                    if (move.Type == MoveType.CastleKingside ||
                        move.Type == MoveType.CastleQueenside)
                    {
                        if (IsCastlingLegal(move, state))
                            legalMoves.Add(move);
                        continue;
                    }

                    // Simuliraj potez i provjeri da li kralj ostaje u šahu
                    if (!LeavesKingInCheck(move, state))
                        legalMoves.Add(move);
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// Vraća legalne poteze samo za jednu figuru.
        /// Korisno za UI — kad igrač klikne na figuru.
        /// </summary>
        public List<Move> GetLegalMovesForPiece(Piece piece, GameState state)
        {
            var legalMoves = new List<Move>();
            var pseudoMoves = piece.GetPseudoLegalMoves(state.Board);

            foreach (var move in pseudoMoves)
            {
                if (move.Type == MoveType.CastleKingside ||
                    move.Type == MoveType.CastleQueenside)
                {
                    if (IsCastlingLegal(move, state))
                        legalMoves.Add(move);
                    continue;
                }

                if (!LeavesKingInCheck(move, state))
                    legalMoves.Add(move);
            }

            return legalMoves;
        }

        // ── Provjera šaha ────────────────────────────────────────

        /// <summary>
        /// Simulira potez i provjerava da li vlastiti kralj ostaje u šahu.
        /// Ako da — potez je ilegalan.
        /// </summary>
        private bool LeavesKingInCheck(Move move, GameState state)
        {
            var newState = state.ApplyMove(move);

            // ApplyMove mijenja CurrentPlayer — provjeravamo originalnog igrača
            return newState.IsInCheck(state.CurrentPlayer);
        }

        // ── Provjera rokade ──────────────────────────────────────

        /// <summary>
        /// Rokada je legalna samo ako:
        /// 1. Igrač ima pravo na rokadu (kralj/top se nisu pomakli)
        /// 2. Polja između kralja i topa su prazna
        /// 3. Kralj nije u šahu
        /// 4. Kralj ne prolazi kroz polje pod napadom
        /// 5. Kralj ne završava na polju pod napadom
        /// </summary>
        private bool IsCastlingLegal(Move move, GameState state)
        {
            var color = state.CurrentPlayer;
            bool isKingside = move.Type == MoveType.CastleKingside;

            // ── Provjera 1: prava na rokadu ──────────────────────
            bool hasRight = (color, isKingside) switch
            {
                (PieceColor.White, true) => state.WhiteCanCastleKingside,
                (PieceColor.White, false) => state.WhiteCanCastleQueenside,
                (PieceColor.Black, true) => state.BlackCanCastleKingside,
                (PieceColor.Black, false) => state.BlackCanCastleQueenside,
                _ => false
            };
            if (!hasRight) return false;

            int row = color == PieceColor.White ? 0 : 7;

            // ── Provjera 2: polja između su prazna ───────────────
            if (isKingside)
            {
                // Između kralja (e) i topa (h) — polja f i g moraju biti prazna
                if (!state.Board.IsEmpty(new Square(row, 5))) return false;
                if (!state.Board.IsEmpty(new Square(row, 6))) return false;
            }
            else
            {
                // Između kralja (e) i topa (a) — polja b, c, d moraju biti prazna
                if (!state.Board.IsEmpty(new Square(row, 1))) return false;
                if (!state.Board.IsEmpty(new Square(row, 2))) return false;
                if (!state.Board.IsEmpty(new Square(row, 3))) return false;
            }

            // ── Provjera 3: kralj nije u šahu ────────────────────
            if (state.IsInCheck(color)) return false;

            // ── Provjera 4 i 5: kralj ne prolazi/završava u šahu ─
            var opponent = GameState.Opponent(color);

            // Polja kroz koja kralj prolazi
            int[] kingPath = isKingside
                ? new[] { 5, 6 }   // f1, g1 (ili f8, g8)
                : new[] { 3, 2 };  // d1, c1 (ili d8, c8)

            foreach (int col in kingPath)
            {
                var square = new Square(row, col);
                if (IsSquareAttackedBy(square, opponent, state.Board))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Provjerava da li protivnik napada određeno polje.
        /// </summary>
        private bool IsSquareAttackedBy(Square square, PieceColor attacker, Board board)
        {
            return board.GetPieces(attacker)
                        .Any(p => p.CanAttackSquare(square, board));
        }
    }
}