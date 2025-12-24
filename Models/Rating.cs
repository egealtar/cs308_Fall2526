using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Rating
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string ProductId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string OrderId { get; set; } = string.Empty;
    }
}