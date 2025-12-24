using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    public class CommentController : Controller
    {
        private readonly IMongoCollection<Comment> _comments;
        private readonly IMongoCollection<Rating> _ratings;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<CommentController> _logger;

        public CommentController(IMongoDatabase database, ILogger<CommentController> logger)
        {
            _comments = database.GetCollection<Comment>("Comments");
            _ratings = database.GetCollection<Rating>("Ratings");
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        // Ürün detay sayfasından yorum ekleme
        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(string productId, string text, string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);

            // Siparişin teslim edilmiş olduğunu kontrol et
            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
            
            if (order == null || order.Status != "Delivered")
            {
                TempData["Error"] = "You can only comment on delivered orders.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            // Bu ürün bu siparişte var mı kontrol et
            var orderItem = order.Items.FirstOrDefault(i => i.ProductId == productId);
            if (orderItem == null)
            {
                TempData["Error"] = "You can only comment on products you have purchased.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            // Yorum oluştur
            var comment = new Comment
            {
                ProductId = productId,
                UserId = userId ?? "",
                UserName = userName ?? "",
                Text = text,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                OrderId = orderId
            };

            await _comments.InsertOneAsync(comment);
            
            TempData["Success"] = "Your comment has been submitted for approval.";
            return RedirectToAction("Details", "Product", new { id = productId });
        }

        // Rating ekleme (onaysız)
        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRating(string productId, int score, string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Siparişin teslim edilmiş olduğunu kontrol et
            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
            
            if (order == null || order.Status != "Delivered")
            {
                TempData["Error"] = "You can only rate delivered orders.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            // Bu ürün bu siparişte var mı kontrol et
            var orderItem = order.Items.FirstOrDefault(i => i.ProductId == productId);
            if (orderItem == null)
            {
                TempData["Error"] = "You can only rate products you have purchased.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            // Daha önce rating vermiş mi kontrol et
            var existingRating = await _ratings.Find(r => r.ProductId == productId && r.UserId == userId).FirstOrDefaultAsync();
            
            if (existingRating != null)
            {
                // Update existing rating
                var update = Builders<Rating>.Update
                    .Set(r => r.Score, score)
                    .Set(r => r.CreatedAt, DateTime.UtcNow);
                
                await _ratings.UpdateOneAsync(r => r.Id == existingRating.Id, update);
                TempData["Success"] = "Your rating has been updated.";
            }
            else
            {
                // Create new rating
                var rating = new Rating
                {
                    ProductId = productId,
                    UserId = userId ?? "",
                    Score = score,
                    CreatedAt = DateTime.UtcNow,
                    OrderId = orderId
                };

                await _ratings.InsertOneAsync(rating);
                TempData["Success"] = "Thank you for rating this product!";
            }

            return RedirectToAction("Details", "Product", new { id = productId });
        }

        // Product Manager için yorum onaylama sayfası
        [HttpGet]
        [Authorize(Roles = "ProductManager")]
        public async Task<IActionResult> Manage()
        {
            // Onay bekleyen yorumları getir
            var pendingComments = await _comments
                .Find(c => !c.IsApproved)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Her yorum için ürün bilgisini ekle
            var commentsWithProducts = new List<CommentViewModel>();
            
            foreach (var comment in pendingComments)
            {
                var product = await _products.Find(p => p.Id == comment.ProductId).FirstOrDefaultAsync();
                
                commentsWithProducts.Add(new CommentViewModel
                {
                    Comment = comment,
                    ProductName = product?.Name ?? "Unknown Product"
                });
            }

            return View(commentsWithProducts);
        }

        // Yorum onaylama
        [HttpPost]
        [Authorize(Roles = "ProductManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveComment(string commentId)
        {
            var update = Builders<Comment>.Update.Set(c => c.IsApproved, true);
            await _comments.UpdateOneAsync(c => c.Id == commentId, update);
            
            TempData["Success"] = "Comment approved successfully.";
            return RedirectToAction("Manage");
        }

        // Yorum reddetme (silme)
        [HttpPost]
        [Authorize(Roles = "ProductManager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectComment(string commentId)
        {
            await _comments.DeleteOneAsync(c => c.Id == commentId);
            
            TempData["Success"] = "Comment rejected and deleted.";
            return RedirectToAction("Manage");
        }

        // Ürün için onaylanmış yorumları getir (Product Details sayfası için)
        [HttpGet]
        public async Task<IActionResult> GetApprovedComments(string productId)
        {
            var comments = await _comments
                .Find(c => c.ProductId == productId && c.IsApproved)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            return PartialView("_CommentsPartial", comments);
        }

        // Ürün için ortalama rating hesapla
        [HttpGet]
        public async Task<IActionResult> GetAverageRating(string productId)
        {
            var ratings = await _ratings
                .Find(r => r.ProductId == productId)
                .ToListAsync();

            if (ratings.Count == 0)
            {
                return Json(new { average = 0, count = 0 });
            }

            var average = ratings.Average(r => r.Score);
            return Json(new { average = Math.Round(average, 1), count = ratings.Count });
        }
    }

    // ViewModel for Comment Management
    public class CommentViewModel
    {
        public Comment Comment { get; set; } = new Comment();
        public string ProductName { get; set; } = string.Empty;
    }
}