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
        private readonly ILogger<OrderController> _logger;

        public OrderController(IMongoDatabase database, ILogger<OrderController> logger)
        {
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
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

            // Siparişi sil
            await _orders.DeleteOneAsync(o => o.Id == id);

            TempData["Success"] = "Order cancelled successfully";
            return RedirectToAction("MyOrders");
        }
    }
}