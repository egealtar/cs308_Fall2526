using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class RefundRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public List<RefundItem> Items { get; set; } = new List<RefundItem>();
        public decimal TotalRefundAmount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed
        public string Reason { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; } // SalesManager userId
        public string? RejectionReason { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class RefundItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Price at time of purchase (including discount)
        public decimal Subtotal { get; set; }
    }
}

