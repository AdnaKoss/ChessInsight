using ChessInsight.Core;
using ChessInsight.Core.Engine;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class GameStateTests
    {
        // ── ApplyMove: kretanje figura ───────────────────────────────

        [Fact]
        public void ApplyMove_PieceMoves_FromEmptyToDestination()
        {
            var state = new GameState();
            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("e2")));
            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("e4")));
        }

        [Fact]
        public void ApplyMove_SwitchesCurrentPlayer()
        {
            var state = new GameState();
            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Equal(PieceColor.Black, newState.CurrentPlayer);
        }

        // ── En passant ────────────────────────────────────────────────

        [Fact]
        public void ApplyMove_PawnDoubleStep_SetsEnPassantSquare()
        {
            var state = new GameState();
            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            // EP polje je e3 (iza pješaka koji je preskočio)
            Assert.NotNull(newState.Board.EnPassantSquare);
            Assert.Equal(Square.FromAlgebraic("e3"), newState.Board.EnPassantSquare);
        }

        [Fact]
        public void ApplyMove_EnPassant_RemovesCapturedPawn()
        {
            // Postavi: bijeli pješak d5, crni pješak e5, EP polje = e6
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("e1"), new King(PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("e8"), new King(PieceColor.Black, Square.FromAlgebraic("e8")));
            board.SetPiece(Square.FromAlgebraic("d5"), new Pawn(PieceColor.White, Square.FromAlgebraic("d5")));
            board.SetPiece(Square.FromAlgebraic("e5"), new Pawn(PieceColor.Black, Square.FromAlgebraic("e5")));
            board.EnPassantSquare = Square.FromAlgebraic("e6");

            var state = new GameState(board, PieceColor.White);
            var epMove = new Move(Square.FromAlgebraic("d5"), Square.FromAlgebraic("e6"), MoveType.EnPassant);

            var newState = state.ApplyMove(epMove);

            // Uhvaćeni crni pješak mora biti uklonjen s e5
            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("e5")));
            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("e6")));
        }

        // ── Rokada ────────────────────────────────────────────────────

        [Fact]
        public void ApplyMove_CastleKingside_MovesKingAndRook()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var castleMove = new Move(Square.FromAlgebraic("e1"), Square.FromAlgebraic("g1"), MoveType.CastleKingside);

            var newState = state.ApplyMove(castleMove);

            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("g1")));
            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("f1")));
            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("e1")));
            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("h1")));
        }

        [Fact]
        public void ApplyMove_CastleQueenside_MovesKingAndRook()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var castleMove = new Move(Square.FromAlgebraic("e1"), Square.FromAlgebraic("c1"), MoveType.CastleQueenside);

            var newState = state.ApplyMove(castleMove);

            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("c1")));
            Assert.NotNull(newState.Board.GetPiece(Square.FromAlgebraic("d1")));
            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("e1")));
            Assert.Null(newState.Board.GetPiece(Square.FromAlgebraic("a1")));
        }

        // ── Prava na rokadu ───────────────────────────────────────────

        [Fact]
        public void ApplyMove_KingMove_RevokesAllCastlingRights()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var kingMove = new Move(Square.FromAlgebraic("e1"), Square.FromAlgebraic("e2"), MoveType.Normal);

            var newState = state.ApplyMove(kingMove);

            Assert.False(newState.WhiteCanCastleKingside);
            Assert.False(newState.WhiteCanCastleQueenside);
        }

        [Fact]
        public void ApplyMove_KingsideRookMove_RevokesKingsideRight()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var rookMove = new Move(Square.FromAlgebraic("h1"), Square.FromAlgebraic("h2"), MoveType.Normal);

            var newState = state.ApplyMove(rookMove);

            Assert.False(newState.WhiteCanCastleKingside);
            Assert.True(newState.WhiteCanCastleQueenside); // queenside nepromijenjen
        }

        [Fact]
        public void ApplyMove_QueensideRookMove_RevokesQueensideRight()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            var rookMove = new Move(Square.FromAlgebraic("a1"), Square.FromAlgebraic("a2"), MoveType.Normal);

            var newState = state.ApplyMove(rookMove);

            Assert.False(newState.WhiteCanCastleQueenside);
            Assert.True(newState.WhiteCanCastleKingside);
        }

        // ── Promocija ─────────────────────────────────────────────────

        [Fact]
        public void ApplyMove_PawnPromotion_ReplacesWithChosenPiece()
        {
            var board = new Board();
            board.SetPiece(Square.FromAlgebraic("e1"), new King(PieceColor.White, Square.FromAlgebraic("e1")));
            board.SetPiece(Square.FromAlgebraic("e8"), new King(PieceColor.Black, Square.FromAlgebraic("e8")));
            board.SetPiece(Square.FromAlgebraic("a7"), new Pawn(PieceColor.White, Square.FromAlgebraic("a7")));

            var state = new GameState(board, PieceColor.White);
            var promMove = new Move(Square.FromAlgebraic("a7"), Square.FromAlgebraic("a8"), MoveType.PawnPromotion, PieceType.Queen);

            var newState = state.ApplyMove(promMove);

            var promoted = newState.Board.GetPiece(Square.FromAlgebraic("a8"));
            Assert.NotNull(promoted);
            Assert.Equal(PieceType.Queen, promoted!.Type);
            Assert.Equal(PieceColor.White, promoted.Color);
        }

        // ── HalfMoveClock ─────────────────────────────────────────────

        [Fact]
        public void ApplyMove_PawnMove_ResetsHalfMoveClock()
        {
            var state = FenParser.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 10 1");
            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Equal(0, newState.HalfMoveClock);
        }

        [Fact]
        public void ApplyMove_NormalMove_IncrementsHalfMoveClock()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 5 1");
            var move = new Move(Square.FromAlgebraic("a1"), Square.FromAlgebraic("a2"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Equal(6, newState.HalfMoveClock);
        }

        [Fact]
        public void ApplyMove_BlackMove_IncrementsFullMoveNumber()
        {
            var state = FenParser.Parse("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
            var move = new Move(Square.FromAlgebraic("e7"), Square.FromAlgebraic("e5"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Equal(2, newState.FullMoveNumber);
        }

        [Fact]
        public void ApplyMove_WhiteMove_DoesNotIncrementFullMoveNumber()
        {
            var state = new GameState();
            var move = new Move(Square.FromAlgebraic("e2"), Square.FromAlgebraic("e4"), MoveType.Normal);

            var newState = state.ApplyMove(move);

            Assert.Equal(1, newState.FullMoveNumber);
        }

        // ── UpdateStatus ──────────────────────────────────────────────

        [Fact]
        public void UpdateStatus_HalfMoveClockAt100_DrawStatus()
        {
            // Pozicija s HalfMoveClock = 100
            var state = FenParser.Parse("4k3/8/8/8/8/8/8/4K3 w - - 100 50");
            var gen = new MoveGenerator();
            var moves = gen.GetLegalMoves(state);
            state.UpdateStatus(moves);

            Assert.Equal(GameStatus.Draw, state.Status);
        }
    }
}
