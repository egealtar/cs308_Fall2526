using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("TaxId")]
        public string TaxId { get; set; } = string.Empty;

        [BsonElement("HomeAddress")]
        public string HomeAddress { get; set; } = string.Empty;

        [BsonElement("Role")]
        public string Role { get; set; } = "Customer"; // Customer, SalesManager, ProductManager, SupportAgent, Admin

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;
    }
}