using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class RefundRequestTests
    {
        [Fact]
        public void RefundRequest_DefaultStatus_IsPending()
        {
            var refund = new RefundRequest();
            Assert.Equal("Pending", refund.Status);
        }

        [Fact]
        public void RefundRequest_Items_InitializedEmpty()
        {
            var refund = new RefundRequest();
            Assert.NotNull(refund.Items);
            Assert.Empty(refund.Items);
        }

        [Fact]
        public void RefundRequest_RequestedAt_DefaultIsUtcNowOrEarlier()
        {
            var refund = new RefundRequest();
            Assert.True(refund.RequestedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void RefundRequest_TotalRefundAmount_AssignedCorrectly()
        {
            var refund = new RefundRequest { TotalRefundAmount = 250m };
            Assert.Equal(250m, refund.TotalRefundAmount);
        }

        [Fact]
        public void RefundItem_Subtotal_MatchesQuantityTimesPrice_WhenSet()
        {
            var item = new RefundItem
            {
                Quantity = 2,
                Price = 50m,
                Subtotal = 100m
            };

            Assert.Equal(item.Quantity * item.Price, item.Subtotal);
        }
    }
}