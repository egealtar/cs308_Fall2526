using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<RefundRequest> _refundRequests;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IMongoDatabase database, ILogger<OrderController> logger)
        {
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
            _refundRequests = database.GetCollection<RefundRequest>("RefundRequests");
            _logger = logger;
        }

        // Customer'ın kendi siparişleri
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> MyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var orders = await _orders
                .Find(o => o.UserId == userId)
                .SortByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Sipariş detayı
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            var order = await _orders.Find(o => o.Id == id).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            // Sadece kendi siparişini veya manager görebilir
            if (order.UserId != userId && userRole != "SalesManager" && userRole != "ProductManager")
            {
                return Forbid();
            }

            return View(order);
        }


        // SalesManager - Tüm siparişler
        [HttpGet]
        [Authorize(Roles = "SalesManager,ProductManager")]
        public async Task<IActionResult> AllOrders()
        {
            var orders = await _orders
                .Find(_ => true)
                .SortByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Cancel order (sadece Processing durumunda)
        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _orders.Find(o => o.Id == id && o.UserId == userId).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Processing")
            {
                TempData["Error"] = "Only processing orders can be cancelled";
                return RedirectToAction("MyOrders");
            }

            // Stokları geri ekle
            foreach (var item in order.Items)
            {
                var productUpdate = Builders<Product>.Update
                    .Inc(p => p.QuantityInStock, item.Quantity);
                
                await _products.UpdateOneAsync(p => p.Id == item.ProductId, productUpdate);
            }

            // Sipariş durumunu "Cancelled" olarak güncelle
            var update = Builders<Order>.Update
                .Set(o => o.Status, "Cancelled")
                .Set(o => o.UpdatedAt, DateTime.UtcNow);

            await _orders.UpdateOneAsync(o => o.Id == id, update);

            TempData["Success"] = "Order cancelled successfully";
            return RedirectToAction("MyOrders");
        }

        // Request refund for delivered orders
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RequestRefund(string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Delivered")
            {
                TempData["Error"] = "Only delivered orders can be refunded";
                return RedirectToAction("MyOrders");
            }

            // Check if order is within 30 days
            var daysSincePurchase = (DateTime.UtcNow - order.CreatedAt).TotalDays;
            if (daysSincePurchase > 30)
            {
                TempData["Error"] = "Refund requests must be made within 30 days of purchase";
                return RedirectToAction("Details", new { id = orderId });
            }

            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(string orderId, List<string> productIds, List<int> quantities, string reason)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Delivered")
            {
                TempData["Error"] = "Only delivered orders can be refunded";
                return RedirectToAction("MyOrders");
            }

            // Check if order is within 30 days
            var daysSincePurchase = (DateTime.UtcNow - order.CreatedAt).TotalDays;
            if (daysSincePurchase > 30)
            {
                TempData["Error"] = "Refund requests must be made within 30 days of purchase";
                return RedirectToAction("Details", new { id = orderId });
            }

            if (productIds == null || !productIds.Any() || quantities == null || !quantities.Any())
            {
                TempData["Error"] = "Please select at least one product to refund";
                return RedirectToAction("RequestRefund", new { orderId });
            }

            // Create refund items
            var refundItems = new List<RefundItem>();
            decimal totalRefundAmount = 0;

            for (int i = 0; i < productIds.Count && i < quantities.Count; i++)
            {
                var productId = productIds[i];
                var quantity = quantities[i];

                if (quantity <= 0) continue;

                var orderItem = order.Items.FirstOrDefault(item => item.ProductId == productId);
                if (orderItem == null) continue;

                // Ensure quantity doesn't exceed ordered quantity
                var refundQuantity = Math.Min(quantity, orderItem.Quantity);

                var refundItem = new RefundItem
                {
                    ProductId = productId,
                    ProductName = orderItem.ProductName,
                    Quantity = refundQuantity,
                    Price = orderItem.Price, // Price at time of purchase (including discount)
                    Subtotal = orderItem.Price * refundQuantity
                };

                refundItems.Add(refundItem);
                totalRefundAmount += refundItem.Subtotal;
            }

            if (!refundItems.Any())
            {
                TempData["Error"] = "No valid items selected for refund";
                return RedirectToAction("RequestRefund", new { orderId });
            }

            // Create refund request
            var refundRequest = new RefundRequest
            {
                OrderId = orderId,
                UserId = userId,
                Items = refundItems,
                TotalRefundAmount = totalRefundAmount,
                Status = "Pending",
                Reason = reason ?? string.Empty,
                RequestedAt = DateTime.UtcNow
            };

            await _refundRequests.InsertOneAsync(refundRequest);

            TempData["Success"] = "Refund request submitted successfully. A sales manager will review your request.";
            return RedirectToAction("MyOrders");
        }
    }
}