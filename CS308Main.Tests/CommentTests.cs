using System;
using Xunit;
using CS308Main.Models;

namespace CS308Main.Tests
{
    public class CommentTests
    {
        [Fact]
        public void Comment_DefaultIsApproved_False()
        {
            var comment = new Comment();
            Assert.False(comment.IsApproved);
        }

        [Fact]
        public void Comment_Text_AssignedCorrectly()
        {
            var comment = new Comment { Text = "Nice product" };
            Assert.Equal("Nice product", comment.Text);
        }

        [Fact]
        public void Comment_CreatedAt_IsUtcNowOrEarlier()
        {
            var comment = new Comment();
            Assert.True(comment.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Comment_ProductId_AssignedCorrectly()
        {
            var comment = new Comment { ProductId = "p1" };
            Assert.Equal("p1", comment.ProductId);
        }

        [Fact]
        public void Comment_UserName_AssignedCorrectly()
        {
            var comment = new Comment { UserName = "Efe" };
            Assert.Equal("Efe", comment.UserName);
        }
    }
}