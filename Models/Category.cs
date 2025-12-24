using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRequired]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}