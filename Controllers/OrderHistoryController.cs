using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "Customer")]
    public class OrderHistoryController : Controller
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly ILogger<OrderHistoryController> _logger;

        public OrderHistoryController(IMongoDatabase database, ILogger<OrderHistoryController> logger)
        {
            _orders = database.GetCollection<Order>("Orders");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var orders = await _orders
                .Find(o => o.UserId == userId)
                .SortByDescending(o => o.CreatedAt)  // CreatedAt kullan, OrderDate değil!
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _orders.Find(o => o.Id == id && o.UserId == userId).FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
    }
}