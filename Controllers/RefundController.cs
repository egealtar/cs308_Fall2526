using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "SalesManager")]
    public class RefundController : Controller
    {
        private readonly IMongoCollection<RefundRequest> _refundRequests;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<User> _users;
        private readonly IEmailService _emailService;
        private readonly ILogger<RefundController> _logger;

        public RefundController(
            IMongoDatabase database,
            IEmailService emailService,
            ILogger<RefundController> logger)
        {
            _refundRequests = database.GetCollection<RefundRequest>("RefundRequests");
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
            _users = database.GetCollection<User>("Users");
            _emailService = emailService;
            _logger = logger;
        }

        // List all refund requests
        [HttpGet]
        public async Task<IActionResult> Index(string status = null)
        {
            FilterDefinition<RefundRequest> filter = Builders<RefundRequest>.Filter.Empty;

            if (!string.IsNullOrEmpty(status))
            {
                filter = Builders<RefundRequest>.Filter.Eq(r => r.Status, status);
            }

            var refundRequests = await _refundRequests
                .Find(filter)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync();

            ViewBag.StatusFilter = status;
            return View(refundRequests);
        }

        // View refund request details
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var refundRequest = await _refundRequests.Find(r => r.Id == id).FirstOrDefaultAsync();

            if (refundRequest == null)
            {
                return NotFound();
            }

            var order = await _orders.Find(o => o.Id == refundRequest.OrderId).FirstOrDefaultAsync();
            var user = await _users.Find(u => u.Id == refundRequest.UserId).FirstOrDefaultAsync();

            ViewBag.Order = order;
            ViewBag.User = user;

            return View(refundRequest);
        }

        // Approve refund request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id)
        {
            var salesManagerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var refundRequest = await _refundRequests.Find(r => r.Id == id).FirstOrDefaultAsync();

            if (refundRequest == null)
            {
                return NotFound();
            }

            if (refundRequest.Status != "Pending")
            {
                TempData["Error"] = "This refund request has already been processed";
                return RedirectToAction("Details", new { id });
            }

            // Update refund request status to Approved
            var update = Builders<RefundRequest>.Update
                .Set(r => r.Status, "Approved")
                .Set(r => r.ReviewedAt, DateTime.UtcNow)
                .Set(r => r.ReviewedBy, salesManagerId);

            await _refundRequests.UpdateOneAsync(r => r.Id == id, update);

            TempData["Success"] = "Refund request approved. Product must be received back before completing the refund.";
            return RedirectToAction("Details", new { id });
        }

        // Reject refund request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string rejectionReason)
        {
            var salesManagerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var refundRequest = await _refundRequests.Find(r => r.Id == id).FirstOrDefaultAsync();

            if (refundRequest == null)
            {
                return NotFound();
            }

            if (refundRequest.Status != "Pending")
            {
                TempData["Error"] = "This refund request has already been processed";
                return RedirectToAction("Details", new { id });
            }

            // Update refund request status to Rejected
            var update = Builders<RefundRequest>.Update
                .Set(r => r.Status, "Rejected")
                .Set(r => r.ReviewedAt, DateTime.UtcNow)
                .Set(r => r.ReviewedBy, salesManagerId)
                .Set(r => r.RejectionReason, rejectionReason ?? "No reason provided");

            await _refundRequests.UpdateOneAsync(r => r.Id == id, update);

            // Send rejection email
            var user = await _users.Find(u => u.Id == refundRequest.UserId).FirstOrDefaultAsync();
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                try
                {
                    var emailBody = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2>Refund Request Rejected</h2>
    <p>Dear {user.Name},</p>
    <p>Your refund request for Order #{refundRequest.OrderId.Substring(0, 8)} has been rejected.</p>
    <p><strong>Reason:</strong> {rejectionReason ?? "No reason provided"}</p>
    <p>If you have any questions, please contact our support team.</p>
    <br>
    <p>Best regards,</p>
    <p><strong>MotorMatch Team</strong></p>
</body>
</html>";

                    await _emailService.SendEmailAsync(user.Email, "Refund Request Rejected", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send rejection email to {user.Email}");
                }
            }

            TempData["Success"] = "Refund request rejected";
            return RedirectToAction("Index");
        }

        // Complete refund (after product is received)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(string id)
        {
            var refundRequest = await _refundRequests.Find(r => r.Id == id).FirstOrDefaultAsync();

            if (refundRequest == null)
            {
                return NotFound();
            }

            if (refundRequest.Status != "Approved")
            {
                TempData["Error"] = "Only approved refund requests can be completed";
                return RedirectToAction("Details", new { id });
            }

            // Restore products to stock
            foreach (var item in refundRequest.Items)
            {
                var productUpdate = Builders<Product>.Update
                    .Inc(p => p.QuantityInStock, item.Quantity);

                await _products.UpdateOneAsync(p => p.Id == item.ProductId, productUpdate);
            }

            // Update refund request status to Completed
            var update = Builders<RefundRequest>.Update
                .Set(r => r.Status, "Completed")
                .Set(r => r.CompletedAt, DateTime.UtcNow);

            await _refundRequests.UpdateOneAsync(r => r.Id == id, update);

            // Send completion email with refund amount
            var user = await _users.Find(u => u.Id == refundRequest.UserId).FirstOrDefaultAsync();
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                try
                {
                    var emailBody = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2>Refund Approved and Processed</h2>
    <p>Dear {user.Name},</p>
    <p>Your refund request for Order #{refundRequest.OrderId.Substring(0, 8)} has been completed.</p>
    <p><strong>Refunded Amount:</strong> ${refundRequest.TotalRefundAmount:F2}</p>
    <p>The refund has been processed to your original payment method. Please allow 3-5 business days for the refund to appear in your account.</p>
    <br>
    <p>Best regards,</p>
    <p><strong>MotorMatch Team</strong></p>
</body>
</html>";

                    await _emailService.SendEmailAsync(user.Email, "Refund Processed", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send completion email to {user.Email}");
                }
            }

            TempData["Success"] = "Refund completed successfully. Products restored to stock and customer notified.";
            return RedirectToAction("Index");
        }
    }
}

