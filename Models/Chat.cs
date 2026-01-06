using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Chat
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("CustomerId")]
        public string? CustomerId { get; set; } // null for guest users

        [BsonElement("CustomerName")]
        public string CustomerName { get; set; } = string.Empty;

        [BsonElement("CustomerEmail")]
        public string? CustomerEmail { get; set; } // null for guest users

        [BsonElement("AgentId")]
        public string? AgentId { get; set; } // null if not claimed

        [BsonElement("Status")]
        public string Status { get; set; } = "Waiting"; // Waiting, Active, Closed

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("LastMessageAt")]
        public DateTime? LastMessageAt { get; set; }
    }

    public class ChatMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("ChatId")]
        public string ChatId { get; set; } = string.Empty;

        [BsonElement("SenderId")]
        public string SenderId { get; set; } = string.Empty;

        [BsonElement("SenderName")]
        public string SenderName { get; set; } = string.Empty;

        [BsonElement("SenderType")]
        public string SenderType { get; set; } = string.Empty; // Customer, Agent, System

        [BsonElement("Content")]
        public string Content { get; set; } = string.Empty;

        [BsonElement("Attachments")]
        public List<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("IsRead")]
        public bool IsRead { get; set; } = false;
    }

    public class ChatAttachment
    {
        [BsonElement("FileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("FileType")]
        public string FileType { get; set; } = string.Empty; // pdf, image, video

        [BsonElement("FileSize")]
        public long FileSize { get; set; }

        [BsonElement("FilePath")]
        public string FilePath { get; set; } = string.Empty;

        [BsonElement("UploadedAt")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}

