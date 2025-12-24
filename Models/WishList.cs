using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class WishList
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("UserId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("Items")]
        public List<WishListItem> Items { get; set; } = new List<WishListItem>();

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WishListItem
    {
        [BsonElement("ProductId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("AddedAt")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}