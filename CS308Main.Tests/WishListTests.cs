using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class WishListTests
    {
        [Fact]
        public void WishList_Items_InitializedEmpty()
        {
            var wishList = new WishList();
            Assert.NotNull(wishList.Items);
            Assert.Empty(wishList.Items);
        }

        [Fact]
        public void WishList_UserId_AssignedCorrectly()
        {
            var wishList = new WishList { UserId = "user1" };
            Assert.Equal("user1", wishList.UserId);
        }

        [Fact]
        public void WishList_CreatedAt_IsUtcNowOrEarlier()
        {
            var wishList = new WishList();
            Assert.True(wishList.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void WishListItem_ProductId_Assigned()
        {
            var item = new WishListItem { ProductId = "prod123" };
            Assert.Equal("prod123", item.ProductId);
        }

        [Fact]
        public void WishList_Items_CanAddItem()
        {
            var wishList = new WishList();
            wishList.Items.Add(new WishListItem { ProductId = "p1" });

            Assert.Single(wishList.Items);
            Assert.Equal("p1", wishList.Items[0].ProductId);
        }
    }
}