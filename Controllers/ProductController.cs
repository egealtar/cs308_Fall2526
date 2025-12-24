using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    public class ProductController : Controller
    {
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Comment> _comments;
        private readonly IMongoCollection<Rating> _ratings;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IMongoDatabase database, ILogger<ProductController> logger)
        {
            _products = database.GetCollection<Product>("Products");
            _orders = database.GetCollection<Order>("Orders");
            _comments = database.GetCollection<Comment>("Comments");
            _ratings = database.GetCollection<Rating>("Ratings");
            _logger = logger;
        }

        // Product Manager: Create Product
        [HttpGet]
        [Authorize(Roles = "ProductManager")]
        public async Task<IActionResult> Create()
        {
            // Get all genres for category dropdown
            var products = await _products.Find(_ => true).ToListAsync();
            var genres = products.Select(p => p.Genre).Distinct().OrderBy(g => g).ToList();
            ViewBag.Genres = genres;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "ProductManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (!ModelState.IsValid)
            {
                var products = await _products.Find(_ => true).ToListAsync();
                var genres = products.Select(p => p.Genre).Distinct().OrderBy(g => g).ToList();
                ViewBag.Genres = genres;
                return View(product);
            }

            // Product Managers cannot set prices - set to 0, Sales Manager must set it
            product.Price = 0;
            
            // Set default product cost if not provided (based on 0 price, will need to be updated)
            if (!product.ProductCost.HasValue)
            {
                product.ProductCost = 0;
            }

            await _products.InsertOneAsync(product);
            _logger.LogInformation($"Product {product.Name} created by Product Manager (price must be set by Sales Manager)");
            TempData["Success"] = $"Product '{product.Name}' created successfully! Note: Price must be set by a Sales Manager.";
            return RedirectToAction("Directory");
        }

        // Product Manager: Edit Product
        [HttpGet]
        [Authorize(Roles = "ProductManager")]
        public async Task<IActionResult> Edit(string id)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }

            var products = await _products.Find(_ => true).ToListAsync();
            var genres = products.Select(p => p.Genre).Distinct().OrderBy(g => g).ToList();
            ViewBag.Genres = genres;
            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = "ProductManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var products = await _products.Find(_ => true).ToListAsync();
                var genres = products.Select(p => p.Genre).Distinct().OrderBy(g => g).ToList();
                ViewBag.Genres = genres;
                return View(product);
            }

            // Set default product cost if not provided
            if (!product.ProductCost.HasValue)
            {
                product.ProductCost = product.Price * 0.5m;
            }

            // Get existing product to preserve price (Product Managers cannot change price)
            var existingProduct = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (existingProduct == null)
            {
                return NotFound();
            }

            var update = Builders<Product>.Update
                .Set(p => p.Name, product.Name)
                .Set(p => p.Model, product.Model)
                .Set(p => p.SerialNumber, product.SerialNumber)
                .Set(p => p.Description, product.Description)
                .Set(p => p.QuantityInStock, product.QuantityInStock)
                // Price is NOT updated - only Sales Managers can set prices
                .Set(p => p.ProductCost, product.ProductCost)
                .Set(p => p.WarrantyStatus, product.WarrantyStatus)
                .Set(p => p.DistributorInformation, product.DistributorInformation)
                .Set(p => p.Genre, product.Genre)
                .Set(p => p.ImagePath, product.ImagePath);

            await _products.UpdateOneAsync(p => p.Id == id, update);
            _logger.LogInformation($"Product {product.Name} updated by Product Manager (price unchanged)");
            TempData["Success"] = $"Product '{product.Name}' updated successfully!";
            return RedirectToAction("Directory");
        }

        // Sales Manager: Set Product Price
        [HttpGet]
        [Authorize(Roles = "SalesManager")]
        public async Task<IActionResult> SetPrice(string id)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = "SalesManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrice(string id, [FromForm] decimal price)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }

            if (price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than 0");
                return View(product);
            }

            var update = Builders<Product>.Update
                .Set(p => p.Price, price);

            // If product cost is 0 or not set, set it to 50% of price
            if (!product.ProductCost.HasValue || product.ProductCost == 0)
            {
                update = update.Set(p => p.ProductCost, price * 0.5m);
            }

            await _products.UpdateOneAsync(p => p.Id == id, update);
            _logger.LogInformation($"Price set to ${price} for product {product.Name} by Sales Manager");
            TempData["Success"] = $"Price set to ${price:F2} for '{product.Name}'";
            return RedirectToAction("Directory");
        }

        // Product Manager: Delete Product
        [HttpPost]
        [Authorize(Roles = "ProductManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (product == null)
            {
                TempData["Error"] = "Product not found";
                return RedirectToAction("Directory");
            }

            // Check if product is in any orders
            var orderCount = await _orders.CountDocumentsAsync(
                Builders<Order>.Filter.ElemMatch(o => o.Items, 
                    item => item.ProductId == id));

            if (orderCount > 0)
            {
                TempData["Error"] = $"Cannot delete product '{product.Name}' because it is associated with {orderCount} order(s).";
                return RedirectToAction("Directory");
            }

            await _products.DeleteOneAsync(p => p.Id == id);
            _logger.LogInformation($"Product {product.Name} deleted by Product Manager");
            TempData["Success"] = $"Product '{product.Name}' deleted successfully!";
            return RedirectToAction("Directory");
        }

        [HttpGet]
        public async Task<IActionResult> Directory()
        {
            var products = await _products.Find(_ => true).ToListAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            // Get approved comments
            var comments = await _comments
                .Find(c => c.ProductId == id && c.IsApproved)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Get average rating
            var ratings = await _ratings
                .Find(r => r.ProductId == id)
                .ToListAsync();

            var averageRating = ratings.Any() ? ratings.Average(r => r.Score) : 0;
            var ratingCount = ratings.Count;

            // If user is logged in, check if they have delivered orders with this product
            List<Order>? deliveredOrders = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                deliveredOrders = await _orders
                    .Find(o => o.UserId == userId && 
                               o.Status == "Delivered" && 
                               o.Items.Any(i => i.ProductId == id))
                    .ToListAsync();
            }

            ViewBag.Comments = comments;
            ViewBag.AverageRating = Math.Round(averageRating, 1);
            ViewBag.RatingCount = ratingCount;
            ViewBag.DeliveredOrders = deliveredOrders;

            return View(product);
        }
    }
}