using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Model")]
        public string Model { get; set; } = string.Empty;

        [BsonElement("SerialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [BsonElement("Description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("QuantityInStock")]
        public int QuantityInStock { get; set; }

        [BsonElement("Price")]
        public decimal Price { get; set; }

        [BsonElement("OriginalPrice")]
        [BsonIgnoreIfNull]
        public decimal? OriginalPrice { get; set; }

        [BsonElement("DiscountedPrice")]
        [BsonIgnoreIfNull]
        public decimal? DiscountedPrice { get; set; }

        [BsonElement("WarrantyStatus")]
        public bool WarrantyStatus { get; set; }

        [BsonElement("DistributorInformation")]
        public string DistributorInformation { get; set; } = string.Empty;

        [BsonElement("Genre")]
        public string Genre { get; set; } = string.Empty;

        [BsonElement("ImagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [BsonElement("Author")]
        [BsonIgnoreIfNull]
        public string? Author { get; set; }

        [BsonElement("Popularity")]
        public int Popularity { get; set; } = 0;

        // Computed properties
        public string Category => Genre;
        public string ImageUrl => ImagePath;

        public decimal FinalPrice => DiscountedPrice ?? Price;
        
        public bool HasDiscount => DiscountedPrice.HasValue && DiscountedPrice < Price;
        
        public int DiscountPercentage => HasDiscount 
            ? (int)Math.Round((1 - (DiscountedPrice!.Value / Price)) * 100) 
            : 0;
    }
}