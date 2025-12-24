using CS308Main.Models;
using Xunit;

namespace CS308Main.Tests
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

        // Test 16: Category should return the same value as Genre.
        [Fact]
        public void Category_UsesGenreProperty()
        {
            var product = new Product
            {
                Genre = "Brake Parts"
            };

            Assert.Equal("Brake Parts", product.Category);
        }

        // Test 17: ImageUrl should return the same value as ImagePath.
        [Fact]
        public void ImageUrl_UsesImagePathProperty()
        {
            var product = new Product
            {
                ImagePath = "/images/parts/brakepad.png"
            };

            Assert.Equal("/images/parts/brakepad.png", product.ImageUrl);
        }

        // Test 18: DiscountPercentage should be 0 when there is no valid discount.
        [Fact]
        public void DiscountPercentage_IsZero_WhenNoDiscount()
        {
            var product = new Product
            {
                Price = 100m,
                DiscountedPrice = null
            };

            Assert.Equal(0m, product.DiscountPercentage);
        }

        // Test 19: DiscountPercentage should be correctly calculated when discount is valid.
        [Fact]
        public void DiscountPercentage_CalculatedCorrectly_WhenDiscountValid()
        {
            var product = new Product
            {
                Price = 200m,
                DiscountedPrice = 150m  // 25% discount
            };

            Assert.Equal(25m, product.DiscountPercentage);
        }
    }
}
