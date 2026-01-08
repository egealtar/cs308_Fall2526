using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class DiscountTests
    {
        [Fact]
        public void Discount_ProductIds_InitializedEmpty()
        {
            var discount = new Discount();
            Assert.NotNull(discount.ProductIds);
            Assert.Empty(discount.ProductIds);
        }

        [Fact]
        public void Discount_IsActive_DefaultTrue()
        {
            var discount = new Discount();
            Assert.True(discount.IsActive);
        }

        [Fact]
        public void Discount_DiscountPercentage_AssignedCorrectly()
        {
            var discount = new Discount { DiscountPercentage = 15m };
            Assert.Equal(15m, discount.DiscountPercentage);
        }

        [Fact]
        public void Discount_StartDate_BeforeEndDate_WhenGiven()
        {
            var discount = new Discount
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(5)
            };

            Assert.True(discount.StartDate < discount.EndDate);
        }

        [Fact]
        public void Discount_CreatedAt_IsUtcNowOrEarlier()
        {
            var discount = new Discount();
            Assert.True(discount.CreatedAt <= DateTime.UtcNow);
        }
    }
}