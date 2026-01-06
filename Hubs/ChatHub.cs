using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CS308Main.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinChat(string chatId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            _logger.LogInformation($"User {Context.UserIdentifier} joined chat {chatId}");
        }

        public async Task LeaveChat(string chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            _logger.LogInformation($"User {Context.UserIdentifier} left chat {chatId}");
        }

        public async Task SendMessage(string chatId, string senderId, string senderName, string senderType, string content, List<object>? attachments = null)
        {
            var messageData = new
            {
                ChatId = chatId,
                SenderId = senderId,
                SenderName = senderName,
                SenderType = senderType,
                Content = content,
                Attachments = attachments ?? new List<object>(),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", messageData);
            
            // If message is from agent, notify customer to update notification badge
            if (senderType == "Agent")
            {
                await Clients.Group($"chat_{chatId}").SendAsync("NewAgentMessage", chatId);
            }
            
            _logger.LogInformation($"Message sent in chat {chatId} by {senderName}");
        }

        public async Task Typing(string chatId, string senderName, bool isTyping)
        {
            await Clients.GroupExcept($"chat_{chatId}", Context.ConnectionId).SendAsync("UserTyping", senderName, isTyping);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}

