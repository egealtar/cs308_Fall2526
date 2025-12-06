using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly ShoppingCartService _cartService;
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<ShoppingCartController> _logger;

        public ShoppingCartController(
            ShoppingCartService cartService,
            IMongoDatabase database,
            ILogger<ShoppingCartController> logger)
        {
            _cartService = cartService;
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user"; // For browsing without login
            }

            var cart = await _cartService.GetCartAsync(userId);
            
            if (cart == null || !cart.Items.Any())
            {
                ViewBag.Message = "Your cart is empty";
                return View(new ShoppingCart { UserId = userId });
            }

            // Get product details for each cart item
            var cartWithDetails = new List<CartItemViewModel>();
            
            foreach (var item in cart.Items)
            {
                var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                
                if (product != null)
                {
                    cartWithDetails.Add(new CartItemViewModel
                    {
                        ProductId = item.ProductId,
                        ProductName = product.Name,
                        Price = product.Price,
                        Quantity = item.Quantity,
                        ImageUrl = product.ImageUrl,
                        IsAvailable = item.IsAvailable,
                        QuantityInStock = product.QuantityInStock
                    });
                }
            }

            ViewBag.CartItems = cartWithDetails;
            ViewBag.Total = cartWithDetails.Sum(item => item.Price * item.Quantity);

            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string productId, int quantity = 1)
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user";
            }

            // Check product availability
            var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
            
            if (product == null)
            {
                TempData["Error"] = "Product not found";
                return RedirectToAction("Index", "Home");
            }

            if (product.QuantityInStock < quantity)
            {
                TempData["Error"] = $"Only {product.QuantityInStock} items available in stock";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            await _cartService.AddToCartAsync(userId, productId, quantity);

            TempData["Success"] = $"{product.Name} added to cart";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(string productId, int quantity)
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user";
            }

            if (quantity <= 0)
            {
                return await RemoveFromCart(productId);
            }

            // Check stock
            var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
            
            if (product == null)
            {
                TempData["Error"] = "Product not found";
                return RedirectToAction("Index");
            }

            if (product.QuantityInStock < quantity)
            {
                TempData["Error"] = $"Only {product.QuantityInStock} items available";
                return RedirectToAction("Index");
            }

            await _cartService.UpdateQuantityAsync(userId, productId, quantity);

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(string productId)
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user";
            }

            await _cartService.RemoveFromCartAsync(userId, productId);

            TempData["Success"] = "Item removed from cart";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user";
            }

            await _cartService.ClearCartAsync(userId);

            TempData["Success"] = "Cart cleared";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            string userId;

            if (User.Identity?.IsAuthenticated == true)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            }
            else
            {
                userId = "guest_user";
            }

            var cart = await _cartService.GetCartAsync(userId);
            var count = cart?.Items.Sum(i => i.Quantity) ?? 0;

            return Json(new { count });
        }
    }

    // ViewModel for cart display
    public class CartItemViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int QuantityInStock { get; set; }
    }
}