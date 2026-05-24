using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class PawnTests
    {
        [Fact]
        public void WhitePawn_StartingSquare_HasTwoMoves()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("e2"));
            board.SetPiece(pawn.Position, pawn);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count); // e3 i e4
        }

        [Fact]
        public void WhitePawn_Blocked_HasNoMoves()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("e2"));
            var blocker = new Rook(PieceColor.White, Square.FromAlgebraic("e3"));
            board.SetPiece(pawn.Position, pawn);
            board.SetPiece(blocker.Position, blocker);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Equal(0, moves.Count);
        }

        [Fact]
        public void WhitePawn_DiagonalCapture_IncludesCaptureMove()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("d4"));
            var enemy = new Pawn(PieceColor.Black, Square.FromAlgebraic("e5"));
            board.SetPiece(pawn.Position, pawn);
            board.SetPiece(enemy.Position, enemy);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("e5")) &&
                m.Type == MoveType.Capture);
        }

        [Fact]
        public void WhitePawn_EnPassant_GeneratesEnPassantMove()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("d5"));
            board.EnPassantSquare = Square.FromAlgebraic("e6"); // crni pješak je prošao e7→e5
            board.SetPiece(pawn.Position, pawn);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Contains(moves, m =>
                m.To.Equals(Square.FromAlgebraic("e6")) &&
                m.Type == MoveType.EnPassant);
        }

        [Fact]
        public void WhitePawn_Promotion_GeneratesFourChoices()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("a7"));
            board.SetPiece(pawn.Position, pawn);

            var moves = pawn.GetPseudoLegalMoves(board);

            var promotions = moves.Where(m => m.Type == MoveType.PawnPromotion).ToList();
            Assert.Equal(4, promotions.Count);
            Assert.Contains(promotions, m => m.PromotionPiece == PieceType.Queen);
            Assert.Contains(promotions, m => m.PromotionPiece == PieceType.Rook);
            Assert.Contains(promotions, m => m.PromotionPiece == PieceType.Bishop);
            Assert.Contains(promotions, m => m.PromotionPiece == PieceType.Knight);
        }

        [Fact]
        public void BlackPawn_MovesInOppositeDirection()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.Black, Square.FromAlgebraic("e7"));
            board.SetPiece(pawn.Position, pawn);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Equal(2, moves.Count); // e6 i e5
            Assert.All(moves, m => Assert.True(m.To.Row < pawn.Position.Row));
        }

        [Fact]
        public void BlackPawn_OnSecondRank_PromotesWithFourChoices()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.Black, Square.FromAlgebraic("a2"));
            board.SetPiece(pawn.Position, pawn);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.Equal(4, moves.Count);
            Assert.All(moves, m => Assert.Equal(MoveType.PawnPromotion, m.Type));
        }

        [Fact]
        public void WhitePawn_CannotCaptureFriendly()
        {
            var board = new Board();
            var pawn = new Pawn(PieceColor.White, Square.FromAlgebraic("d4"));
            var friendly = new Pawn(PieceColor.White, Square.FromAlgebraic("e5"));
            board.SetPiece(pawn.Position, pawn);
            board.SetPiece(friendly.Position, friendly);

            var moves = pawn.GetPseudoLegalMoves(board);

            Assert.DoesNotContain(moves, m => m.To.Equals(Square.FromAlgebraic("e5")));
        }
    }
}
