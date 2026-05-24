using ChessInsight.Core;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using Xunit;

namespace ChessInsight.Tests
{
    public class FenParserTests
    {
        [Fact]
        public void StartingPosition_ThirtyTwoPieces()
        {
            var state = FenParser.Parse(FenParser.StartingFen);

            Assert.Equal(32, state.Board.GetAllPieces().Count());
        }

        [Fact]
        public void StartingPosition_WhiteToMove()
        {
            var state = FenParser.Parse(FenParser.StartingFen);

            Assert.Equal(PieceColor.White, state.CurrentPlayer);
        }

        [Fact]
        public void StartingPosition_AllCastlingRights()
        {
            var state = FenParser.Parse(FenParser.StartingFen);

            Assert.True(state.WhiteCanCastleKingside);
            Assert.True(state.WhiteCanCastleQueenside);
            Assert.True(state.BlackCanCastleKingside);
            Assert.True(state.BlackCanCastleQueenside);
        }

        [Fact]
        public void BlackToMove_ParsedCorrectly()
        {
            var state = FenParser.Parse("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");

            Assert.Equal(PieceColor.Black, state.CurrentPlayer);
        }

        [Fact]
        public void EnPassantSquare_ParsedCorrectly()
        {
            var state = FenParser.Parse("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");

            Assert.NotNull(state.Board.EnPassantSquare);
            Assert.Equal(Square.FromAlgebraic("e3"), state.Board.EnPassantSquare);
        }

        [Fact]
        public void NoCastlingRights_ParsedCorrectly()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w - - 0 1");

            Assert.False(state.WhiteCanCastleKingside);
            Assert.False(state.WhiteCanCastleQueenside);
            Assert.False(state.BlackCanCastleKingside);
            Assert.False(state.BlackCanCastleQueenside);
        }

        [Fact]
        public void PartialCastlingRights_ParsedCorrectly()
        {
            var state = FenParser.Parse("r3k2r/8/8/8/8/8/8/R3K2R w Kq - 0 1");

            Assert.True(state.WhiteCanCastleKingside);
            Assert.False(state.WhiteCanCastleQueenside);
            Assert.False(state.BlackCanCastleKingside);
            Assert.True(state.BlackCanCastleQueenside);
        }

        [Fact]
        public void HalfMoveAndFullMove_ParsedCorrectly()
        {
            var state = FenParser.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 7 42");

            Assert.Equal(7, state.HalfMoveClock);
            Assert.Equal(42, state.FullMoveNumber);
        }

        [Fact]
        public void EmptyFen_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => FenParser.Parse(""));
        }

        [Fact]
        public void InvalidRankCount_ThrowsArgumentException()
        {
            // Samo 7 redova umjesto 8
            Assert.Throws<ArgumentException>(() =>
                FenParser.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1"));
        }

        [Fact]
        public void FenRoundTrip_PreservesPosition()
        {
            var state = FenParser.Parse(FenParser.StartingFen);
            var roundTrippedFen = state.ToFen();
            var state2 = FenParser.Parse(roundTrippedFen);

            Assert.Equal(state.CurrentPlayer, state2.CurrentPlayer);
            Assert.Equal(state.WhiteCanCastleKingside,  state2.WhiteCanCastleKingside);
            Assert.Equal(state.WhiteCanCastleQueenside, state2.WhiteCanCastleQueenside);
            Assert.Equal(state.BlackCanCastleKingside,  state2.BlackCanCastleKingside);
            Assert.Equal(state.BlackCanCastleQueenside, state2.BlackCanCastleQueenside);
            Assert.Equal(state.Board.GetAllPieces().Count(), state2.Board.GetAllPieces().Count());
        }
    }
}
