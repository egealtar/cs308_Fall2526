using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "Customer")]
    public class WishListController : Controller
    {
        private readonly IMongoCollection<WishList> _wishLists;
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<WishListController> _logger;

        public WishListController(IMongoDatabase database, ILogger<WishListController> logger)
        {
            _wishLists = database.GetCollection<WishList>("WishLists");
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var wishList = await _wishLists.Find(w => w.UserId == userId).FirstOrDefaultAsync();

            if (wishList == null || !wishList.Items.Any())
            {
                return View(new List<WishListViewModel>());
            }

            var wishListItems = new List<WishListViewModel>();

            foreach (var item in wishList.Items)
            {
                var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();

                if (product != null)
                {
                    wishListItems.Add(new WishListViewModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Price = product.Price,
                        DiscountedPrice = product.DiscountedPrice,
                        ImageUrl = product.ImageUrl,
                        QuantityInStock = product.QuantityInStock,
                        AddedAt = item.AddedAt,
                        IsAvailable = product.QuantityInStock > 0,
                        HasDiscount = product.HasDiscount
                    });
                }
            }

            return View(wishListItems.OrderByDescending(w => w.AddedAt).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWishList(string productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if product exists
            var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
            if (product == null)
            {
                TempData["Error"] = "Product not found";
                return RedirectToAction("Index", "Home");
            }

            // Get or create wish list
            var wishList = await _wishLists.Find(w => w.UserId == userId).FirstOrDefaultAsync();

            if (wishList == null)
            {
                wishList = new WishList
                {
                    UserId = userId ?? "",
                    Items = new List<WishListItem>()
                };
                await _wishLists.InsertOneAsync(wishList);
            }

            // Check if product already in wish list
            if (wishList.Items.Any(i => i.ProductId == productId))
            {
                TempData["Info"] = "Product is already in your wish list";
                return RedirectToAction("Index");
            }

            // Add to wish list
            var wishListItem = new WishListItem
            {
                ProductId = productId,
                AddedAt = DateTime.UtcNow
            };

            var update = Builders<WishList>.Update
                .Push(w => w.Items, wishListItem)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            await _wishLists.UpdateOneAsync(w => w.UserId == userId, update);

            _logger.LogInformation($"Product {productId} added to wish list for user {userId}");

            TempData["Success"] = $"{product.Name} added to your wish list!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromWishList(string productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var update = Builders<WishList>.Update
                .PullFilter(w => w.Items, i => i.ProductId == productId)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            var result = await _wishLists.UpdateOneAsync(w => w.UserId == userId, update);

            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation($"Product {productId} removed from wish list for user {userId}");
                TempData["Success"] = "Product removed from wish list";
            }
            else
            {
                TempData["Error"] = "Failed to remove product from wish list";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearWishList()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var update = Builders<WishList>.Update
                .Set(w => w.Items, new List<WishListItem>())
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            await _wishLists.UpdateOneAsync(w => w.UserId == userId, update);

            _logger.LogInformation($"Wish list cleared for user {userId}");

            TempData["Success"] = "Wish list cleared";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveToCart(string productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check product stock
            var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
            if (product == null)
            {
                TempData["Error"] = "Product not found";
                return RedirectToAction("Index");
            }

            if (product.QuantityInStock <= 0)
            {
                TempData["Error"] = "Product is out of stock";
                return RedirectToAction("Index");
            }

            // Remove from wish list and add to cart
            await RemoveFromWishList(productId);

            return RedirectToAction("AddToCart", "ShoppingCart", new { productId, quantity = 1 });
        }
    }
}