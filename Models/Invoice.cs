using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Invoice
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PdfPath { get; set; } = string.Empty;
    }

    public class InvoiceItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }
}