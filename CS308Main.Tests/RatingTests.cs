using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class RatingTests
    {
        [Fact]
        public void Rating_Score_AssignedCorrectly()
        {
            var rating = new Rating { Score = 5 };
            Assert.Equal(5, rating.Score);
        }

        [Fact]
        public void Rating_CreatedAt_IsUtcNowOrEarlier()
        {
            var rating = new Rating();
            Assert.True(rating.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Rating_ProductId_AssignedCorrectly()
        {
            var rating = new Rating { ProductId = "p1" };
            Assert.Equal("p1", rating.ProductId);
        }

        [Fact]
        public void Rating_UserId_AssignedCorrectly()
        {
            var rating = new Rating { UserId = "u1" };
            Assert.Equal("u1", rating.UserId);
        }

        [Fact]
        public void Rating_OrderId_AssignedCorrectly()
        {
            var rating = new Rating { OrderId = "o1" };
            Assert.Equal("o1", rating.OrderId);
        }
    }
}