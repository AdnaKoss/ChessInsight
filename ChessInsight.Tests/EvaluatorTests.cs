using ChessInsight.Core;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using ChessInsight.Engine;
using Xunit;

namespace ChessInsight.Tests
{
    public class EvaluatorTests
    {
        private readonly Evaluator _eval = new();

        // ── Početna pozicija ─────────────────────────────────────────

        [Fact]
        public void StartingPosition_EvalIsZero()
        {
            // Simetričnost garantuje tačno 0
            var state = new GameState();
            Assert.Equal(0, _eval.Evaluate(state));
        }

        // ── Materijalna prednost ──────────────────────────────────────

        [Fact]
        public void MaterialAdvantage_ExtraQueenScoresAbove500()
        {
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("e1"), new King(PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("d4"), new Queen(PieceColor.White, Square.FromAlgebraic("d4")));
            board.SetPiece(Square.FromAlgebraic("e8"), new King(PieceColor.Black, Square.FromAlgebraic("e8")));
            var state = new GameState(board, PieceColor.White);

            Assert.True(_eval.Evaluate(state) > 500);
        }

        [Fact]
        public void EqualMaterial_EvalNearZero()
        {
            // Bijeli: top, crni: top — simetričan raspored
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            board.SetPiece(Square.FromAlgebraic("d4"), new Rook(PieceColor.White, Square.FromAlgebraic("d4")));
            board.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            board.SetPiece(Square.FromAlgebraic("d5"), new Rook(PieceColor.Black, Square.FromAlgebraic("d5")));
            var state = new GameState(board, PieceColor.White);

            // Material jednak, mali PST razlika prihvatljiva
            int score = _eval.Evaluate(state);
            Assert.InRange(score, -100, 100);
        }

        // ── Pješačka struktura ────────────────────────────────────────

        [Fact]
        public void DoublePawns_ScoreLowerThanNonDoubled()
        {
            // Crni kralj na a8 da pravilo kvadrata ne interferira s pješačkim bodovanjem
            // Pozicija A: udvostručeni bijeli pješaci na c-liniji
            var boardD = new Board();
            boardD.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardD.SetPiece(Square.FromAlgebraic("c3"), new Pawn(PieceColor.White, Square.FromAlgebraic("c3")));
            boardD.SetPiece(Square.FromAlgebraic("c4"), new Pawn(PieceColor.White, Square.FromAlgebraic("c4")));
            boardD.SetPiece(Square.FromAlgebraic("a8"), new King(PieceColor.Black, Square.FromAlgebraic("a8")));
            var stateDoubled = new GameState(boardD, PieceColor.White);

            // Pozicija B: isti pješaci na različitim linijama
            var boardN = new Board();
            boardN.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardN.SetPiece(Square.FromAlgebraic("c3"), new Pawn(PieceColor.White, Square.FromAlgebraic("c3")));
            boardN.SetPiece(Square.FromAlgebraic("d4"), new Pawn(PieceColor.White, Square.FromAlgebraic("d4")));
            boardN.SetPiece(Square.FromAlgebraic("a8"), new King(PieceColor.Black, Square.FromAlgebraic("a8")));
            var stateNormal = new GameState(boardN, PieceColor.White);

            Assert.True(_eval.Evaluate(stateDoubled) < _eval.Evaluate(stateNormal));
        }

        [Fact]
        public void PassedPawn_ScoresHigherThanBlocked()
        {
            // Bijeli pješak bez crnih pješaka ispred — "prolazni"
            var boardPassed = new Board();
            boardPassed.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardPassed.SetPiece(Square.FromAlgebraic("e5"), new Pawn(PieceColor.White, Square.FromAlgebraic("e5")));
            boardPassed.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            var statePassed = new GameState(boardPassed, PieceColor.White);

            // Bijeli pješak blokiran crnim pješakom na istoj liniji
            var boardBlocked = new Board();
            boardBlocked.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardBlocked.SetPiece(Square.FromAlgebraic("e5"), new Pawn(PieceColor.White, Square.FromAlgebraic("e5")));
            boardBlocked.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            boardBlocked.SetPiece(Square.FromAlgebraic("e6"), new Pawn(PieceColor.Black, Square.FromAlgebraic("e6")));
            var stateBlocked = new GameState(boardBlocked, PieceColor.White);

            Assert.True(_eval.Evaluate(statePassed) > _eval.Evaluate(stateBlocked));
        }

        // ── Par lovaca ────────────────────────────────────────────────

        [Fact]
        public void BishopPair_ScoresHigherThanBishopPlusKnight()
        {
            // Bijeli: 2 lovca — par lovaca; crni: lovac + skakač — nema para
            var boardPair = new Board();
            boardPair.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardPair.SetPiece(Square.FromAlgebraic("c1"), new Bishop(PieceColor.White, Square.FromAlgebraic("c1")));
            boardPair.SetPiece(Square.FromAlgebraic("f1"), new Bishop(PieceColor.White, Square.FromAlgebraic("f1")));
            boardPair.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            boardPair.SetPiece(Square.FromAlgebraic("c8"), new Bishop(PieceColor.Black, Square.FromAlgebraic("c8")));
            boardPair.SetPiece(Square.FromAlgebraic("f8"), new Knight(PieceColor.Black, Square.FromAlgebraic("f8")));
            var statePair = new GameState(boardPair, PieceColor.White);

            // Simetričan: oba imaju par lovaca
            var boardSym = new Board();
            boardSym.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardSym.SetPiece(Square.FromAlgebraic("c1"), new Bishop(PieceColor.White, Square.FromAlgebraic("c1")));
            boardSym.SetPiece(Square.FromAlgebraic("f1"), new Bishop(PieceColor.White, Square.FromAlgebraic("f1")));
            boardSym.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            boardSym.SetPiece(Square.FromAlgebraic("c8"), new Bishop(PieceColor.Black, Square.FromAlgebraic("c8")));
            boardSym.SetPiece(Square.FromAlgebraic("f8"), new Bishop(PieceColor.Black, Square.FromAlgebraic("f8")));
            var stateSym = new GameState(boardSym, PieceColor.White);

            // Bijeli ima ekskluzivni par lovaca → treba bolje ocjeniti
            Assert.True(_eval.Evaluate(statePair) > _eval.Evaluate(stateSym));
        }

        // ── PST (Piece-Square Tables) ─────────────────────────────────

        [Fact]
        public void PST_KnightInCenter_BetterThanCorner()
        {
            var boardCenter = new Board();
            boardCenter.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardCenter.SetPiece(Square.FromAlgebraic("d4"), new Knight(PieceColor.White, Square.FromAlgebraic("d4")));
            boardCenter.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            var stateCenter = new GameState(boardCenter, PieceColor.White);

            var boardCorner = new Board();
            boardCorner.SetPiece(Square.FromAlgebraic("a1"), new King(PieceColor.White, Square.FromAlgebraic("a1")));
            boardCorner.SetPiece(Square.FromAlgebraic("a8"), new Knight(PieceColor.White, Square.FromAlgebraic("a8")));
            boardCorner.SetPiece(Square.FromAlgebraic("h8"), new King(PieceColor.Black, Square.FromAlgebraic("h8")));
            var stateCorner = new GameState(boardCorner, PieceColor.White);

            Assert.True(_eval.Evaluate(stateCenter) > _eval.Evaluate(stateCorner));
        }
    }
}
