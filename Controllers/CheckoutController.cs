using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CS308Main.Models;
using CS308Main.Services;
using CS308Main.Data;
using System.Threading.Tasks;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CheckoutController : Controller
    {
        private readonly IMongoDBRepository<Order> _orderRepository;
        private readonly IMockPaymentService _mockPaymentService;

        public CheckoutController(
            IMongoDBRepository<Order> orderRepository,
            IMockPaymentService mockPaymentService
        )
        {
            _orderRepository = orderRepository;
            _mockPaymentService = mockPaymentService;
        }

        [HttpGet]
        public async Task<IActionResult> Payment(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return NotFound();

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return NotFound();

            var model = new CardPaymentInput
            {
                OrderId = order.Id!,
                Amount = order.TotalAmount
            };

            return View(model); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(CardPaymentInput input)
        {
            if (!ModelState.IsValid)
                return View(input);

            var order = await _orderRepository.GetByIdAsync(input.OrderId);
            if (order == null)
            {
                ModelState.AddModelError("", "Order not found.");
                return View(input);
            }

            input.Amount = order.TotalAmount;

            var status = await _mockPaymentService.AuthorizeAsync(input);

            if (status == MockPaymentStatus.Approved)
            {
                await _orderRepository.UpdateAsync(order.Id!, order);

                TempData["SuccessMessage"] = "Payment completed successfully (mock).";
                return RedirectToAction("Index", "Orders");
            }

            TempData["ErrorMessage"] = "Payment failed (mock). Please check your card information.";
            return View(input);
        }
    }
}
