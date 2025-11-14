using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class ShoppingCart
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("items")]
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public decimal TotalPrice => Items.Sum(item => item.Price * item.Quantity);

        public int TotalItems => Items.Sum(item => item.Quantity);
    }

    public class CartItem
    {
        [BsonElement("id")]
        public string Id { get; set; } = string.Empty;

        [BsonElement("productId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("productName")]
        public string ProductName { get; set; } = string.Empty;

        [BsonElement("price")]
        public decimal Price { get; set; }

        [BsonElement("unitPrice")]
        public decimal UnitPrice { get; set; }

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("quantityInCart")]
        public int QuantityInCart { get; set; }

        [BsonElement("availableQuantity")]
        public int AvailableQuantity { get; set; }

        [BsonElement("imagePath")]
        public string ImagePath { get; set; } = string.Empty;
    }
}