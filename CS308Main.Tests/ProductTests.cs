using CS308Main.Models;
using Xunit;

namespace CS308Main.Tests.Models
{
    public class ProductTests
    {
        // Test 1: FinalPrice should return normal Price when DiscountedPrice is null.
        [Fact]
        public void FinalPrice_UsesNormalPrice_WhenNoDiscount()
        {
            var product = new Product { Price = 100m, DiscountedPrice = null };

            Assert.Equal(100m, product.FinalPrice);
        }

        // Test 2: FinalPrice should return DiscountedPrice when a discount exists.
        [Fact]
        public void FinalPrice_UsesDiscountedPrice_WhenDiscountExists()
        {
            var product = new Product { Price = 100m, DiscountedPrice = 80m };

            Assert.Equal(80m, product.FinalPrice);
        }

        // Test 3: HasDiscount should be false when DiscountedPrice is null.
        [Fact]
        public void HasDiscount_False_WhenNoDiscount()
        {
            var product = new Product { Price = 100m, DiscountedPrice = null };

            Assert.False(product.HasDiscount);
        }

        // Test 4: HasDiscount should be false when DiscountedPrice >= Price (invalid discount).
        [Fact]
        public void HasDiscount_False_WhenDiscountInvalid()
        {
            var greater = new Product { Price = 100m, DiscountedPrice = 120m };
            var equal   = new Product { Price = 100m, DiscountedPrice = 100m };

            Assert.False(greater.HasDiscount);
            Assert.False(equal.HasDiscount);
        }

        // Test 5: HasDiscount should be true when DiscountedPrice is lower than Price.
        [Fact]
        public void HasDiscount_True_WhenDiscountedPriceLowerThanPrice()
        {
            var product = new Product { Price = 100m, DiscountedPrice = 80m };

            Assert.True(product.HasDiscount);
        }

        // Test 6: Popularity should default to 0 and be incrementable.
        [Fact]
        public void Popularity_DefaultZero_AndCanIncrease()
        {
            var product = new Product();
            Assert.Equal(0, product.Popularity);

            product.Popularity += 5;
            Assert.Equal(5, product.Popularity);
        }
    }
}
