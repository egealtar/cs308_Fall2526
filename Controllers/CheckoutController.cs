using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CheckoutController : Controller
    {
        private readonly IMongoCollection<ShoppingCart> _carts;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<User> _users;
        private readonly MockPaymentService _paymentService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            IMongoDatabase database,
            MockPaymentService paymentService,
            ILogger<CheckoutController> logger)
        {
            _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
            _users = database.GetCollection<User>("Users");
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get user info
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get cart
            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
            
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Your cart is empty";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Calculate total
            decimal total = 0;
            foreach (var item in cart.Items)
            {
                var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product != null)
                {
                    total += product.Price * item.Quantity;
                }
            }

            ViewBag.CartItems = cart.Items;
            ViewBag.Total = total;
            ViewBag.User = user;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(
            string cardNumber,
            string cardName,
            string expiryMonth,
            string expiryYear,
            string cvv,
            string shippingAddress)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get cart
            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
            
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Your cart is empty";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Validate card with Luhn algorithm
            if (!_paymentService.ValidateCardNumber(cardNumber))
            {
                TempData["Error"] = "Invalid card number";
                return RedirectToAction("Index");
            }

            // Check stock availability and calculate total
            var orderItems = new List<OrderItem>();
            decimal totalPrice = 0;

            foreach (var cartItem in cart.Items)
            {
                var product = await _products.Find(p => p.Id == cartItem.ProductId).FirstOrDefaultAsync();
                
                if (product == null)
                {
                    TempData["Error"] = $"Product not found: {cartItem.ProductId}";
                    return RedirectToAction("Index");
                }

                if (product.QuantityInStock < cartItem.Quantity)
                {
                    TempData["Error"] = $"Insufficient stock for {product.Name}. Available: {product.QuantityInStock}";
                    return RedirectToAction("Index");
                }

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = cartItem.Quantity,
                    Price = product.Price
                });

                totalPrice += product.Price * cartItem.Quantity;
            }

            // Process payment (mock)
            var paymentResult = _paymentService.ProcessPayment(cardNumber, totalPrice);
            
            if (!paymentResult.Success)
            {
                TempData["Error"] = $"Payment failed: {paymentResult.Message}";
                return RedirectToAction("Index");
            }

            // Create order
            var order = new Order
            {
                UserId = userId ?? "",
                Items = orderItems,
                TotalPrice = totalPrice,
                Status = "Processing",
                ShippingAddress = shippingAddress,
                PaymentMethod = $"Card ending in {cardNumber.Substring(cardNumber.Length - 4)}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _orders.InsertOneAsync(order);

            // Update stock
            foreach (var item in orderItems)
            {
                var update = Builders<Product>.Update
                    .Inc(p => p.QuantityInStock, -item.Quantity);
                
                await _products.UpdateOneAsync(p => p.Id == item.ProductId, update);
            }

            // Clear cart
            await _carts.DeleteOneAsync(c => c.UserId == userId);

            _logger.LogInformation($"Order {order.Id} created successfully for user {userId}");

            TempData["Success"] = "Order placed successfully!";
            TempData["OrderId"] = order.Id;

            return RedirectToAction("Success", new { orderId = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Success(string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
            
            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(order);
        }
    }
}