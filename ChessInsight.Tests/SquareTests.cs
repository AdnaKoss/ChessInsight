using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChessInsight.Core.Models;
using Xunit;
namespace ChessInsight.Tests
{
    public class SquareTests
    {
        [Fact]
        public void ToAlgebraic_ReturnsCorrectNotation()
        {
            var sq = new Square(0, 0);   // a1
            Assert.Equal("a1", sq.ToAlgebraic());
        }

        [Fact]
        public void FromAlgebraic_ReturnsCorrectSquare()
        {
            var sq = Square.FromAlgebraic("e4");
            Assert.Equal(3, sq.Row);
            Assert.Equal(4, sq.Column);
        }

        [Fact]
        public void IsValid_ReturnsFalseForOutOfBounds()
        {
            var sq = new Square(8, 0);
            Assert.False(sq.IsValid());
        }
    }
}