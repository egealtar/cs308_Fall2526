using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Hubs;
using CS308Main.Services;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "SupportAgent")]
    public class SupportChatController : Controller
    {
        private readonly IMongoCollection<Chat> _chats;
        private readonly IMongoCollection<ChatMessage> _messages;
        private readonly IMongoCollection<ShoppingCart> _carts;
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<WishList> _wishLists;
        private readonly IMongoCollection<User> _users;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IAuthService _authService;
        private readonly ILogger<SupportChatController> _logger;

        public SupportChatController(
            IMongoDatabase database,
            IHubContext<ChatHub> hubContext,
            IAuthService authService,
            ILogger<SupportChatController> logger)
        {
            _chats = database.GetCollection<Chat>("Chats");
            _messages = database.GetCollection<ChatMessage>("ChatMessages");
            _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
            _orders = database.GetCollection<Order>("Orders");
            _wishLists = database.GetCollection<WishList>("WishLists");
            _users = database.GetCollection<User>("Users");
            _hubContext = hubContext;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Get waiting chats (queue)
            var waitingChats = await _chats.Find(c => c.Status == "Waiting")
                .SortBy(c => c.CreatedAt)
                .ToListAsync();

            // Get active chats for current agent
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var activeChatsList = await _chats.Find(c => c.AgentId == agentId && c.Status == "Active")
                .ToListAsync();
            
            // Sort in memory since MongoDB LINQ doesn't support null-coalescing in SortBy
            var activeChats = activeChatsList
                .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
                .ToList();

            ViewBag.WaitingChats = waitingChats;
            ViewBag.ActiveChats = activeChats;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimChat(string chatId)
        {
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var agent = await _authService.GetUserByIdAsync(agentId ?? "");

            if (agent == null)
            {
                return Unauthorized();
            }

            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();
            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            if (chat.Status != "Waiting")
            {
                return BadRequest("Chat is not available for claiming");
            }

            chat.AgentId = agentId;
            chat.Status = "Active";
            chat.UpdatedAt = DateTime.UtcNow;
            await _chats.ReplaceOneAsync(c => c.Id == chatId, chat);

            // Send system message
            var systemMessage = new ChatMessage
            {
                ChatId = chatId,
                SenderId = "system",
                SenderName = "System",
                SenderType = "System",
                Content = $"{agent.Name} has joined the conversation",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _messages.InsertOneAsync(systemMessage);

            // Notify via SignalR
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                systemMessage.Id,
                systemMessage.ChatId,
                systemMessage.SenderId,
                systemMessage.SenderName,
                systemMessage.SenderType,
                systemMessage.Content,
                systemMessage.CreatedAt,
                systemMessage.IsRead
            });

            _logger.LogInformation($"Agent {agent.Name} claimed chat {chatId}");

            return RedirectToAction("Chat", new { chatId });
        }

        [HttpGet]
        public async Task<IActionResult> Chat(string chatId)
        {
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();

            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            // Verify agent has access
            if (chat.AgentId != agentId && chat.Status != "Waiting")
            {
                return Forbid("You don't have access to this chat");
            }

            // Get messages
            var messages = await _messages.Find(m => m.ChatId == chatId)
                .SortBy(m => m.CreatedAt)
                .ToListAsync();

            // Get customer details if logged in
            CustomerDetails? customerDetails = null;
            if (!string.IsNullOrEmpty(chat.CustomerId) && chat.CustomerId != HttpContext.Session.Id)
            {
                // Check if CustomerId is a valid MongoDB ObjectId (not a GUID/session ID)
                // ObjectId is 24 characters hex string
                if (MongoDB.Bson.ObjectId.TryParse(chat.CustomerId, out _))
                {
                    var customer = await _authService.GetUserByIdAsync(chat.CustomerId);
                    if (customer != null)
                {
                    var cart = await _carts.Find(c => c.UserId == chat.CustomerId).FirstOrDefaultAsync();
                    var orders = await _orders.Find(o => o.UserId == chat.CustomerId)
                        .SortByDescending(o => o.CreatedAt)
                        .ToListAsync();
                    var wishList = await _wishLists.Find(w => w.UserId == chat.CustomerId).FirstOrDefaultAsync();
                    customerDetails = new CustomerDetails
                    {
                        UserId = customer.Id,
                        Name = customer.Name,
                        Email = customer.Email,
                        HomeAddress = customer.HomeAddress,
                        CartItemCount = cart?.Items?.Count ?? 0,
                        Orders = orders.Select(o => new OrderDetails
                        {
                            Id = o.Id,
                            TotalPrice = o.TotalPrice,
                            Status = o.Status,
                            CreatedAt = o.CreatedAt,
                            ShippingAddress = o.ShippingAddress,
                            Items = o.Items.Select(i => new OrderItemDetails
                            {
                                ProductName = i.ProductName,
                                Quantity = i.Quantity,
                                Price = i.Price
                            }).ToList()
                        }).ToList(),
                        WishListItemCount = wishList?.Items?.Count ?? 0
                    };
                    }
                }
            }

            ViewBag.Chat = chat;
            ViewBag.Messages = messages;
            ViewBag.CustomerDetails = customerDetails;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string chatId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Message content is required");
            }

            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var agent = await _authService.GetUserByIdAsync(agentId ?? "");

            if (agent == null)
            {
                return Unauthorized();
            }

            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();
            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            if (chat.AgentId != agentId)
            {
                return Forbid("You don't have access to this chat");
            }

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderId = agentId ?? "",
                SenderName = agent.Name,
                SenderType = "Agent",
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

            // Notify customer to update notification badge
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("NewAgentMessage", chatId);

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

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Allowed: PDF, images, videos");
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest("File size exceeds 10MB limit");
            }

            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var agent = await _authService.GetUserByIdAsync(agentId ?? "");

            if (agent == null)
            {
                return Unauthorized();
            }

            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();
            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            if (chat.AgentId != agentId)
            {
                return Forbid("You don't have access to this chat");
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

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderId = agentId ?? "",
                SenderName = agent.Name,
                SenderType = "Agent",
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

            // Notify customer to update notification badge
            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("NewAgentMessage", chatId);

            return Ok(new { success = true, messageId = message.Id, attachment });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseChat(string chatId)
        {
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync();

            if (chat == null)
            {
                return NotFound("Chat not found");
            }

            if (chat.AgentId != agentId)
            {
                return Forbid("You don't have access to this chat");
            }

            chat.Status = "Closed";
            chat.UpdatedAt = DateTime.UtcNow;
            await _chats.ReplaceOneAsync(c => c.Id == chatId, chat);

            // Send system message
            var systemMessage = new ChatMessage
            {
                ChatId = chatId,
                SenderId = "system",
                SenderName = "System",
                SenderType = "System",
                Content = "This conversation has been closed",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _messages.InsertOneAsync(systemMessage);

            await _hubContext.Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                systemMessage.Id,
                systemMessage.ChatId,
                systemMessage.SenderId,
                systemMessage.SenderName,
                systemMessage.SenderType,
                systemMessage.Content,
                systemMessage.CreatedAt,
                systemMessage.IsRead
            });

            return RedirectToAction("Index");
        }
    }

    public class CustomerDetails
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HomeAddress { get; set; } = string.Empty;
        public int CartItemCount { get; set; }
        public List<OrderDetails> Orders { get; set; } = new List<OrderDetails>();
        public int WishListItemCount { get; set; }
    }

    public class OrderDetails
    {
        public string Id { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public List<OrderItemDetails> Items { get; set; } = new List<OrderItemDetails>();
    }

    public class OrderItemDetails
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}

