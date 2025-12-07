using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "DeliveryAdmin")]
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
        public async Task<IActionResult> Index(string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var filterBuilder = Builders<Order>.Filter;
            var filter = filterBuilder.Empty;

            // Status filter
            if (!string.IsNullOrEmpty(status))
            {
                filter = filter & filterBuilder.Eq(o => o.Status, status);
            }

            // Date range filter
            if (dateFrom.HasValue)
            {
                filter = filter & filterBuilder.Gte(o => o.CreatedAt, dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                filter = filter & filterBuilder.Lte(o => o.CreatedAt, dateTo.Value.AddDays(1));
            }

            var orders = await _orders
                .Find(filter)
                .SortByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> MyDeliveries()
        {
            // Get orders that are In-Transit or Processing
            var orders = await _orders
                .Find(o => o.Status == "In-Transit" || o.Status == "Processing")
                .SortByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeliveryStatus(string orderId, string status)
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

            _logger.LogInformation($"Delivery Admin updated order {orderId} status to {status}");
            TempData["Success"] = $"Order status updated to {status}";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> DeliveryDetails(string id)
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