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
        private readonly IMongoCollection<Invoice> _invoices;
        private readonly MockPaymentService _paymentService;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            IMongoDatabase database,
            MockPaymentService paymentService,
            IPdfService pdfService,
            IEmailService emailService,
            IWebHostEnvironment environment,
            ILogger<CheckoutController> logger)
        {
            _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
            _orders = database.GetCollection<Order>("Orders");
            _products = database.GetCollection<Product>("Products");
            _users = database.GetCollection<User>("Users");
            _invoices = database.GetCollection<Invoice>("Invoices");
            _paymentService = paymentService;
            _pdfService = pdfService;
            _emailService = emailService;
            _environment = environment;
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
                    total += product.FinalPrice * item.Quantity;
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

            // Validate card
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
                    Price = product.FinalPrice
                });

                totalPrice += product.FinalPrice * cartItem.Quantity;
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

            // Generate invoice and send email
            try
            {
                _logger.LogInformation($"Generating invoice for order {order.Id}");
                await GenerateAndSendInvoiceAsync(order.Id, userId ?? "");
                TempData["Success"] = "Order placed successfully! Invoice has been sent to your email.";
                _logger.LogInformation($"Invoice generated and email sent for order {order.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate or send invoice for order {order.Id}");
                TempData["Success"] = "Order placed successfully! Invoice generation failed, but you can download it from your order history.";
            }

            TempData["OrderId"] = order.Id;

            return RedirectToAction("Success", new { orderId = order.Id });
        }

        private async Task GenerateAndSendInvoiceAsync(string orderId, string userId)
        {
            try
            {
                _logger.LogInformation($"Starting invoice generation for order {orderId}");

                // Get user
                var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found");
                    throw new Exception("User not found");
                }

                _logger.LogInformation($"User found: {user.Email}");

                // Get order
                var order = await _orders.Find(o => o.Id == orderId).FirstOrDefaultAsync();
                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    throw new Exception("Order not found");
                }

                _logger.LogInformation($"Order found: {order.Id}");

                // Check if invoice already exists
                var existingInvoice = await _invoices.Find(i => i.OrderId == orderId).FirstOrDefaultAsync();
                if (existingInvoice != null)
                {
                    _logger.LogInformation($"Invoice already exists: {existingInvoice.InvoiceNumber}");
                    return;
                }

                // Create invoice
                var invoice = new Invoice
                {
                    OrderId = orderId,
                    UserId = userId,
                    InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                    TotalAmount = order.TotalPrice,
                    Items = order.Items.Select(item => new InvoiceItem
                    {
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        Total = item.Price * item.Quantity
                    }).ToList(),
                    CreatedAt = DateTime.UtcNow,
                    PdfPath = ""
                };

                // Save invoice to DB
                await _invoices.InsertOneAsync(invoice);
                _logger.LogInformation($"Invoice saved to DB: {invoice.InvoiceNumber} with ID: {invoice.Id}");

                // Generate PDF
                _logger.LogInformation($"Generating PDF for invoice {invoice.InvoiceNumber}");
                var pdfPath = await _pdfService.GenerateInvoicePdfAsync(invoice, user);
                _logger.LogInformation($"PDF generated at: {pdfPath}");

                // Update invoice with PDF path
                var filter = Builders<Invoice>.Filter.Eq(i => i.Id, invoice.Id);
                var update = Builders<Invoice>.Update.Set(i => i.PdfPath, pdfPath);
                var result = await _invoices.UpdateOneAsync(filter, update);

                _logger.LogInformation($"PDF path updated in DB. Modified count: {result.ModifiedCount}");

                if (result.ModifiedCount == 0)
                {
                    _logger.LogWarning($"Failed to update PDF path for invoice {invoice.Id}");
                }

                // Send email
                try
                {
                    _logger.LogInformation($"Sending email to: {user.Email}");
                    
                    var fullPdfPath = Path.Combine(_environment.WebRootPath, pdfPath.TrimStart('/'));
                    
                    if (!File.Exists(fullPdfPath))
                    {
                        _logger.LogError($"PDF file not found at: {fullPdfPath}");
                        throw new Exception("PDF file not found for email attachment");
                    }

                    await _emailService.SendInvoiceEmailAsync(user.Email, invoice.InvoiceNumber, fullPdfPath);
                    _logger.LogInformation($"Email sent successfully to {user.Email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, $"Failed to send email: {emailEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GenerateAndSendInvoiceAsync: {ex.Message}");
                throw;
            }
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