using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "SalesManager")]
    public class DeliveryController : Controller
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(IMongoDatabase database, ILogger<DeliveryController> logger)
        {
            _orders = database.GetCollection<Order>("Orders");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _orders
                .Find(_ => true)
                .SortByDescending(o => o.CreatedAt)  // CreatedAt kullan, OrderDate değil!
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string orderId, string status)
        {
            var validStatuses = new[] { "Processing", "In-Transit", "Delivered" };

            if (!validStatuses.Contains(status))
            {
                TempData["Error"] = "Invalid status";
                return RedirectToAction("Index");
            }

            var update = Builders<Order>.Update
                .Set(o => o.Status, status)
                .Set(o => o.UpdatedAt, DateTime.UtcNow);

            await _orders.UpdateOneAsync(o => o.Id == orderId, update);

            _logger.LogInformation($"Order {orderId} status updated to {status}");
            TempData["Success"] = $"Order status updated to {status}";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var order = await _orders.Find(o => o.Id == id).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
    }
}