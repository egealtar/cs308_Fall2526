using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "ProductManager")]
    public class DeliveryController : Controller
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<User> _users;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(IMongoDatabase database, ILogger<DeliveryController> logger)
        {
            _orders = database.GetCollection<Order>("Orders");
            _users = database.GetCollection<User>("Users");
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

            // Create delivery list with all required properties
            var deliveryList = new List<DeliveryListItem>();
            foreach (var order in orders)
            {
                var user = await _users.Find(u => u.Id == order.UserId).FirstOrDefaultAsync();
                foreach (var item in order.Items)
                {
                    deliveryList.Add(new DeliveryListItem
                    {
                        DeliveryId = order.Id,
                        CustomerId = order.UserId,
                        CustomerName = user?.Name ?? "Unknown",
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        TotalPrice = item.Price * item.Quantity,
                        DeliveryAddress = order.ShippingAddress,
                        IsCompleted = order.Status == "Delivered",
                        OrderStatus = order.Status,
                        OrderDate = order.CreatedAt
                    });
                }
            }

            ViewBag.CurrentStatus = status;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.DeliveryList = deliveryList;

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

            _logger.LogInformation($"Product Manager updated order {orderId} status to {status}");
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

    // Delivery List Item ViewModel with all required properties
    public class DeliveryListItem
    {
        public string DeliveryId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
    }
}