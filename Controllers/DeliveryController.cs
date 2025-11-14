using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CS308Main.Data;
using CS308Main.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "SalesManager")]
    public class DeliveryController : Controller
    {
        private readonly IMongoDBRepository<Order> _orderRepository;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(
            IMongoDBRepository<Order> orderRepository,
            ILogger<DeliveryController> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string status = "All")
        {
            try
            {
                var allOrders = await _orderRepository.GetAllAsync();

                var filtered = allOrders
                    .Where(o =>
                        status == "All" ||
                        string.Equals(o.OrderStatus, status, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();

                ViewBag.SelectedStatus = status;
                return View(filtered);
            }
            catch
            {
                return View(Enumerable.Empty<Order>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string orderId, string newStatus)
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(newStatus))
            {
                TempData["ErrorMessage"] = "Order id or status is missing.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var order = await _orderRepository.FindByIdAsync(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                var oldStatus = order.OrderStatus;
                newStatus = newStatus.Trim();

                if (!IsValidTransition(oldStatus, newStatus))
                {
                    TempData["ErrorMessage"] = $"Invalid status change: {oldStatus} → {newStatus}.";
                    return RedirectToAction(nameof(Index));
                }

                order.OrderStatus = newStatus;

                var now = DateTime.Now;

                switch (newStatus)
                {
                    case "Preparing":
                        order.PreparingDate ??= now;
                        break;
                    case "Shipped":
                        order.ShippedDate ??= now;
                        break;
                    case "Delivered":
                        order.DeliveredDate ??= now;
                        break;
                }

                await _orderRepository.ReplaceOneAsync(order);
                TempData["SuccessMessage"] = $"Order status updated: {oldStatus} → {newStatus}";
                return RedirectToAction(nameof(Index), new { status = newStatus });
            }
            catch
            {
                TempData["ErrorMessage"] = "Error updating order status.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPreparing(string orderId)
        {
            return await UpdateStatus(orderId, "Preparing");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsShipped(string orderId)
        {
            return await UpdateStatus(orderId, "Shipped");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsDelivered(string orderId)
        {
            return await UpdateStatus(orderId, "Delivered");
        }

        private bool IsValidTransition(string oldStatus, string newStatus)
        {
            oldStatus ??= "Pending";
            if (oldStatus == newStatus) return false;

            return oldStatus switch
            {
                "Pending"   => newStatus == "Preparing",
                "Preparing" => newStatus == "Shipped",
                "Shipped"   => newStatus == "Delivered",
                "Delivered" => false,
                _           => false
            };
        }
    }
}
