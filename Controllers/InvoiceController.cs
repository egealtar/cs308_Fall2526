using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly IMongoCollection<Invoice> _invoices;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Product> _products;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(
            IMongoDatabase database,
            IPdfService pdfService,
            IEmailService emailService,
            ILogger<InvoiceController> logger)
        {
            _invoices = database.GetCollection<Invoice>("Invoices");
            _orders = database.GetCollection<Order>("Orders");
            _users = database.GetCollection<User>("Users");
            _products = database.GetCollection<Product>("Products");
            _pdfService = pdfService;
            _emailService = emailService;
            _logger = logger;
        }

        // Sipariş tamamlandıktan sonra fatura oluştur
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Generate(string orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Siparişi getir
            var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
            
            if (order == null)
            {
                return NotFound("Order not found");
            }

            // Kullanıcıyı getir
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Daha önce fatura oluşturulmuş mu kontrol et
            var existingInvoice = await _invoices.Find(i => i.OrderId == orderId).FirstOrDefaultAsync();
            
            if (existingInvoice != null)
            {
                TempData["Info"] = "Invoice already exists for this order.";
                return RedirectToAction("Download", new { id = existingInvoice.Id });
            }

            // Fatura oluştur
            var invoice = new Invoice
            {
                OrderId = orderId,
                UserId = userId ?? "",
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                TotalAmount = order.TotalPrice,
                Items = order.Items.Select(item => new InvoiceItem
                {
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Total = item.Price * item.Quantity
                }).ToList(),
                CreatedAt = DateTime.UtcNow
            };

            // MongoDB'ye kaydet
            await _invoices.InsertOneAsync(invoice);

            try
            {
                // PDF oluştur
                var pdfPath = await _pdfService.GenerateInvoicePdfAsync(invoice, user);
                
                // PDF path'i güncelle
                var update = Builders<Invoice>.Update.Set(i => i.PdfPath, pdfPath);
                await _invoices.UpdateOneAsync(i => i.Id == invoice.Id, update);

                // Email gönder
                await _emailService.SendInvoiceEmailAsync(user.Email, invoice.InvoiceNumber, pdfPath);

                TempData["Success"] = "Invoice generated and sent to your email!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate or send invoice");
                TempData["Error"] = "Invoice created but failed to send email. You can download it from your order history.";
            }

            return RedirectToAction("Download", new { id = invoice.Id });
        }

        // Fatura indirme
        [HttpGet]
        public async Task<IActionResult> Download(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            var invoice = await _invoices.Find(i => i.Id == id).FirstOrDefaultAsync();
            
            if (invoice == null)
            {
                return NotFound("Invoice not found");
            }

            // Sadece kendi faturasını veya admin görebilir
            if (invoice.UserId != userId && userRole != "SalesManager" && userRole != "ProductManager")
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(invoice.PdfPath) || !System.IO.File.Exists(invoice.PdfPath))
            {
                return NotFound("Invoice PDF not found");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(invoice.PdfPath);
            var fileName = Path.GetFileName(invoice.PdfPath);

            return File(fileBytes, "application/pdf", fileName);
        }

        // Kullanıcının tüm faturaları
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> MyInvoices()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var invoices = await _invoices
                .Find(i => i.UserId == userId)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(invoices);
        }

        // SalesManager için tüm faturalar
        [HttpGet]
        [Authorize(Roles = "SalesManager,ProductManager")]
        public async Task<IActionResult> AllInvoices(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var filterBuilder = Builders<Invoice>.Filter;
            var filter = filterBuilder.Empty;

            // Date range filter
            if (dateFrom.HasValue)
            {
                filter = filter & filterBuilder.Gte(i => i.CreatedAt, dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                filter = filter & filterBuilder.Lte(i => i.CreatedAt, dateTo.Value.AddDays(1));
            }

            var invoices = await _invoices
                .Find(filter)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(invoices);
        }

        // Revenue and Profit/Loss calculation
        [HttpGet]
        [Authorize(Roles = "SalesManager")]
        public async Task<IActionResult> RevenueReport(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var filterBuilder = Builders<Invoice>.Filter;
            var filter = filterBuilder.Empty;

            // Default to last 30 days if no dates provided
            if (!dateFrom.HasValue || !dateTo.HasValue)
            {
                dateTo = DateTime.UtcNow;
                dateFrom = dateTo.Value.AddDays(-30);
            }

            filter = filter & filterBuilder.Gte(i => i.CreatedAt, dateFrom.Value);
            filter = filter & filterBuilder.Lte(i => i.CreatedAt, dateTo.Value.AddDays(1));

            var invoices = await _invoices
                .Find(filter)
                .ToListAsync();

            decimal totalRevenue = 0;
            decimal totalCost = 0;
            var dailyData = new List<DailyRevenueData>();

            foreach (var invoice in invoices)
            {
                totalRevenue += invoice.TotalAmount;

                // Calculate cost for each item
                foreach (var item in invoice.Items)
                {
                    // Find the order to get product IDs
                    var order = await _orders.Find(o => o.Id == invoice.OrderId).FirstOrDefaultAsync();
                    if (order != null)
                    {
                        var orderItem = order.Items.FirstOrDefault(oi => oi.ProductName == item.ProductName);
                        if (orderItem != null)
                        {
                            var product = await _products.Find(p => p.Id == orderItem.ProductId).FirstOrDefaultAsync();
                            if (product != null)
                            {
                                var productCost = product.GetProductCost();
                                totalCost += productCost * item.Quantity;
                            }
                        }
                    }
                }

                // Group by date for chart
                var dateKey = invoice.CreatedAt.Date;
                var dailyEntry = dailyData.FirstOrDefault(d => d.Date == dateKey);
                if (dailyEntry == null)
                {
                    dailyEntry = new DailyRevenueData { Date = dateKey };
                    dailyData.Add(dailyEntry);
                }
                dailyEntry.Revenue += invoice.TotalAmount;
            }

            var profit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? (profit / totalRevenue) * 100 : 0;

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCost = totalCost;
            ViewBag.Profit = profit;
            ViewBag.ProfitMargin = profitMargin;
            ViewBag.DateFrom = dateFrom.Value.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo.Value.ToString("yyyy-MM-dd");
            ViewBag.DailyData = dailyData.OrderBy(d => d.Date).ToList();

            return View();
        }
    }

    public class DailyRevenueData
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
    }
}