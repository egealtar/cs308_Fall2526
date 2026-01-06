using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Hubs;

namespace CS308Main.Controllers
{
    public class ChatController : Controller
    {
        private readonly IMongoCollection<Chat> _chats;
        private readonly IMongoCollection<ChatMessage> _messages;
        private readonly IMongoCollection<ShoppingCart> _carts;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<WishList> _wishLists;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IMongoDatabase database,
            IHubContext<ChatHub> hubContext,
            ILogger<ChatController> logger)
        {
            _chats = database.GetCollection<Chat>("Chats");
            _messages = database.GetCollection<ChatMessage>("ChatMessages");
            _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
            _orders = database.GetCollection<Order>("Orders");
            _wishLists = database.GetCollection<WishList>("WishLists");
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Guest";
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            // Find existing active chat or create new one
            Chat? chat;
            if (userId != null)
            {
                chat = await _chats.Find(c => c.CustomerId == userId && c.Status != "Closed")
                    .SortByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            else
            {
                // For guest users, use session ID
                var sessionId = HttpContext.Session.Id;
                chat = await _chats.Find(c => c.CustomerId == sessionId && c.Status != "Closed")
                    .SortByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (chat == null)
            {
                chat = new Chat
                {
                    CustomerId = userId ?? HttpContext.Session.Id,
                    CustomerName = userName,
                    CustomerEmail = userEmail,
                    Status = "Waiting",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _chats.InsertOneAsync(chat);
            }

            // Get messages
            var messages = await _messages.Find(m => m.ChatId == chat.Id)
                .SortBy(m => m.CreatedAt)
                .ToListAsync();

            // Mark all agent messages as read when customer opens chat
            if (messages.Any(m => m.SenderType == "Agent" && !m.IsRead))
            {
                var update = Builders<ChatMessage>.Update
                    .Set(m => m.IsRead, true);

                await _messages.UpdateManyAsync(
                    m => m.ChatId == chat.Id && m.SenderType == "Agent" && !m.IsRead,
                    update);

                // Refresh messages after marking as read
                messages = await _messages.Find(m => m.ChatId == chat.Id)
                    .SortBy(m => m.CreatedAt)
                    .ToListAsync();
            }

            // Get customer context if logged in
            CustomerContext? context = null;
            if (userId != null)
            {
                var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
                var orders = await _orders.Find(o => o.UserId == userId)
                    .SortByDescending(o => o.CreatedAt)
                    .Limit(5)
                    .ToListAsync();
                var wishList = await _wishLists.Find(w => w.UserId == userId).FirstOrDefaultAsync();

                context = new CustomerContext
                {
                    CartItemCount = cart?.Items?.Count ?? 0,
                    RecentOrders = orders.Select(o => new OrderSummary
                    {
                        Id = o.Id,
                        TotalPrice = o.TotalPrice,
                        Status = o.Status,
                        CreatedAt = o.CreatedAt
                    }).ToList(),
                    WishListItemCount = wishList?.Items?.Count ?? 0
                };
            }

            ViewBag.ChatId = chat.Id;
            ViewBag.CustomerContext = context;
            ViewBag.Messages = messages;

            return View(chat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string chatId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Message content is required");
            }

            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();
            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Guest";
            var senderId = userId ?? HttpContext.Session.Id;

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderId = senderId,
                SenderName = userName,
                SenderType = userId != null ? "Customer" : "Guest",
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _messages.InsertOneAsync(message);

            // Update chat
            chat.LastMessageAt = DateTime.UtcNow;
            chat.UpdatedAt = DateTime.UtcNow;
            await _chats.ReplaceOneAsync(c => c.Id == chatId, chat);

            // Send via SignalR
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.ChatId,
                message.SenderId,
                message.SenderName,
                message.SenderType,
                message.Content,
                message.Attachments,
                message.CreatedAt,
                message.IsRead
            });

            return Ok(new { success = true, messageId = message.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(string chatId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Allowed: PDF, images, videos");
            }

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest("File size exceeds 10MB limit");
            }

            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();
            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            // Save file
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ChatAttachments");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new ChatAttachment
            {
                FileName = file.FileName,
                FileType = extension == ".pdf" ? "pdf" : (extension.StartsWith(".mp4") || extension.StartsWith(".mov") || extension.StartsWith(".avi") ? "video" : "image"),
                FileSize = file.Length,
                FilePath = $"/ChatAttachments/{fileName}",
                UploadedAt = DateTime.UtcNow
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Guest";
            var senderId = userId ?? HttpContext.Session.Id;

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderId = senderId,
                SenderName = userName,
                SenderType = userId != null ? "Customer" : "Guest",
                Content = $"Sent attachment: {file.FileName}",
                Attachments = new List<ChatAttachment> { attachment },
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _messages.InsertOneAsync(message);

            // Update chat
            chat.LastMessageAt = DateTime.UtcNow;
            chat.UpdatedAt = DateTime.UtcNow;
            await _chats.ReplaceOneAsync(c => c.Id == chatId, chat);

            // Send via SignalR
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.ChatId,
                message.SenderId,
                message.SenderName,
                message.SenderType,
                message.Content,
                Attachments = message.Attachments.Select(a => new
                {
                    a.FileName,
                    a.FileType,
                    a.FileSize,
                    a.FilePath,
                    a.UploadedAt
                }),
                message.CreatedAt,
                message.IsRead
            });

            return Ok(new { success = true, messageId = message.Id, attachment });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string chatId)
        {
            var messages = await _messages.Find(m => m.ChatId == chatId)
                .SortBy(m => m.CreatedAt)
                .ToListAsync();

            return Json(messages.Select(m => new
            {
                m.Id,
                m.SenderId,
                m.SenderName,
                m.SenderType,
                m.Content,
                m.Attachments,
                m.CreatedAt,
                m.IsRead
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionId = HttpContext.Session.Id;
            var customerId = userId ?? sessionId;

            // Find active chat
            Chat? chat;
            if (userId != null)
            {
                chat = await _chats.Find(c => c.CustomerId == userId && c.Status != "Closed")
                    .SortByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            else
            {
                chat = await _chats.Find(c => c.CustomerId == sessionId && c.Status != "Closed")
                    .SortByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (chat == null)
            {
                return Json(new { unreadCount = 0 });
            }

            // Count unread messages from agents
            var unreadCount = await _messages.CountDocumentsAsync(m => 
                m.ChatId == chat.Id && 
                m.SenderType == "Agent" && 
                !m.IsRead);

            return Json(new { unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMessagesAsRead(string chatId)
        {
            var update = Builders<ChatMessage>.Update
                .Set(m => m.IsRead, true);

            await _messages.UpdateManyAsync(
                m => m.ChatId == chatId && m.SenderType == "Agent" && !m.IsRead,
                update);

            return Ok(new { success = true });
        }
    }

    public class CustomerContext
    {
        public int CartItemCount { get; set; }
        public List<OrderSummary> RecentOrders { get; set; } = new List<OrderSummary>();
        public int WishListItemCount { get; set; }
    }

    public class OrderSummary
    {
        public string Id { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

