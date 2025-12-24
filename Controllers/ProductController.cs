using Microsoft.AspNetCore.Mvc;
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