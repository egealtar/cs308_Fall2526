using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "SalesManager")]
    public class DiscountController : Controller
    {
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<Discount> _discounts;
        private readonly IMongoCollection<WishList> _wishLists;
        private readonly IMongoCollection<User> _users;
        private readonly IEmailService _emailService;
        private readonly ILogger<DiscountController> _logger;

        public DiscountController(
            IMongoDatabase database,
            IEmailService emailService,
            ILogger<DiscountController> logger)
        {
            _products = database.GetCollection<Product>("Products");
            _discounts = database.GetCollection<Discount>("Discounts");
            _wishLists = database.GetCollection<WishList>("WishLists");
            _users = database.GetCollection<User>("Users");
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var discounts = await _discounts
                .Find(_ => true)
                .SortByDescending(d => d.CreatedAt)
                .ToListAsync();

            return View(discounts);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var products = await _products
                .Find(_ => true)
                .SortBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = products;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var products = await _products.Find(_ => true).ToListAsync();
                ViewBag.Products = products;
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Validate dates
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be after start date");
                var products = await _products.Find(_ => true).ToListAsync();
                ViewBag.Products = products;
                return View(model);
            }

            // Create discount
            var discount = new Discount
            {
                ProductIds = model.ProductIds,
                DiscountPercentage = model.DiscountPercentage,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                IsActive = true,
                CreatedBy = userId ?? "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _discounts.InsertOneAsync(discount);

            // Apply discount to products
            foreach (var productId in model.ProductIds)
            {
                var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
                if (product != null)
                {
                    var discountedPrice = product.Price * (1 - model.DiscountPercentage / 100);

                    var update = Builders<Product>.Update
                        .Set(p => p.DiscountedPrice, discountedPrice);

                    await _products.UpdateOneAsync(p => p.Id == productId, update);

                    _logger.LogInformation($"Applied {model.DiscountPercentage}% discount to product {productId}");
                }
            }

            // Notify users with products in their wish list
            await NotifyWishListUsers(model.ProductIds, model.DiscountPercentage);

            TempData["Success"] = $"Discount of {model.DiscountPercentage}% applied to {model.ProductIds.Count} product(s)!";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            var discount = await _discounts.Find(d => d.Id == id).FirstOrDefaultAsync();

            if (discount == null)
            {
                TempData["Error"] = "Discount not found";
                return RedirectToAction("Index");
            }

            // Deactivate discount
            var discountUpdate = Builders<Discount>.Update
                .Set(d => d.IsActive, false)
                .Set(d => d.UpdatedAt, DateTime.UtcNow);

            await _discounts.UpdateOneAsync(d => d.Id == id, discountUpdate);

            // Remove discounts from products
            foreach (var productId in discount.ProductIds)
            {
                var productUpdate = Builders<Product>.Update
                    .Set(p => p.DiscountedPrice, null);

                await _products.UpdateOneAsync(p => p.Id == productId, productUpdate);

                _logger.LogInformation($"Removed discount from product {productId}");
            }

            TempData["Success"] = "Discount deactivated successfully";

            return RedirectToAction("Index");
        }

        private async Task NotifyWishListUsers(List<string> productIds, decimal discountPercentage)
        {
            try
            {
                // Find all wish lists containing any of the discounted products
                var filter = Builders<WishList>.Filter.ElemMatch(
                    w => w.Items,
                    i => productIds.Contains(i.ProductId)
                );

                var wishLists = await _wishLists.Find(filter).ToListAsync();

                _logger.LogInformation($"Found {wishLists.Count} wish lists with discounted products");

                foreach (var wishList in wishLists)
                {
                    var user = await _users.Find(u => u.Id == wishList.UserId).FirstOrDefaultAsync();
                    if (user == null) continue;

                    // Get products in user's wish list that are now discounted
                    var discountedWishListItems = wishList.Items
                        .Where(item => productIds.Contains(item.ProductId))
                        .ToList();

                    if (!discountedWishListItems.Any()) continue;

                    // Get product details
                    var productNames = new List<string>();
                    foreach (var item in discountedWishListItems)
                    {
                        var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                        if (product != null)
                        {
                            productNames.Add(product.Name);
                        }
                    }

                    // Send email notification
                    var subject = $"Great News! {discountPercentage}% OFF on Your Wish List Items!";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #dc3545;'>🎉 Special Discount Alert!</h2>
                                <p>Hi {user.Name},</p>
                                <p>Great news! Some products in your wish list are now on sale!</p>
                                
                                <div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0;'>
                                    <h3 style='color: #856404; margin-top: 0;'>
                                        {discountPercentage}% OFF
                                    </h3>
                                    <p style='margin-bottom: 0;'>Don't miss out on this amazing deal!</p>
                                </div>

                                <h4>Products on Sale:</h4>
                                <ul>
                                    {string.Join("", productNames.Select(name => $"<li>{name}</li>"))}
                                </ul>

                                <p>Hurry! This offer won't last forever.</p>
                                
                                <div style='text-align: center; margin: 30px 0;'>
                                    <a href='https://localhost:7001/WishList' 
                                       style='background-color: #007bff; color: white; padding: 12px 30px; 
                                              text-decoration: none; border-radius: 5px; display: inline-block;'>
                                        View My Wish List
                                    </a>
                                </div>

                                <p>Best regards,<br/><strong>MotorMatch Team</strong></p>
                                
                                <hr style='margin-top: 30px; border: none; border-top: 1px solid #dee2e6;'>
                                <p style='font-size: 12px; color: #6c757d;'>
                                    This email was sent because products in your wish list are on sale.
                                </p>
                            </div>
                        </body>
                        </html>
                    ";

                    await _emailService.SendEmailAsync(user.Email, subject, body);

                    _logger.LogInformation($"Sent discount notification to {user.Email}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying wish list users about discount");
                // Don't throw - discount should still be applied even if notifications fail
            }
        }
    }
}