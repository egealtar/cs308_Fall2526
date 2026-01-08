using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class ShoppingCartTests
    {
        [Fact]
        public void ShoppingCart_Items_InitializedEmpty()
        {
            var cart = new ShoppingCart();
            Assert.NotNull(cart.Items);
            Assert.Empty(cart.Items);
        }

        [Fact]
        public void ShoppingCart_UserId_AssignedCorrectly()
        {
            var cart = new ShoppingCart { UserId = "user123" };
            Assert.Equal("user123", cart.UserId);
        }

        [Fact]
        public void ShoppingCart_CreatedAt_IsUtcNowOrEarlier()
        {
            var cart = new ShoppingCart();
            Assert.True(cart.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void CartItem_DefaultAvailability_IsTrue()
        {
            var item = new CartItem();
            Assert.True(item.IsAvailable);
        }

        [Fact]
        public void ShoppingCart_Items_CanAddItem()
        {
            var cart = new ShoppingCart();
            cart.Items.Add(new CartItem { ProductId = "p1", Quantity = 1 });

            Assert.Single(cart.Items);
            Assert.Equal("p1", cart.Items[0].ProductId);
            Assert.Equal(1, cart.Items[0].Quantity);
        }
    }
}