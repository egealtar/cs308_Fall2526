using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CS308Main.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("_id")]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Author")]
        public string Author { get; set; } = string.Empty;

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

        [BsonElement("DiscountedPrice")]
        public decimal? DiscountedPrice { get; set; }  // Nullable - bazı ürünlerde yok

        [BsonElement("WarrantyStatus")]
        public bool WarrantyStatus { get; set; }

        [BsonElement("DistributorInformation")]
        public string DistributorInformation { get; set; } = string.Empty;

        [BsonElement("Genre")]
        public string Genre { get; set; } = string.Empty;

        [BsonElement("OriginalPrice")]
        public decimal OriginalPrice { get; set; }

        [BsonElement("ImagePath")]
        public string ImagePath { get; set; } = string.Empty;

        // COMPUTED PROPERTIES (VIEW İÇİN)
        [BsonIgnore]
        public string Category => Genre;

        [BsonIgnore]
        public string ImageUrl => ImagePath;

        [BsonIgnore]
        public decimal FinalPrice => DiscountedPrice ?? Price;  // İndirimli fiyat varsa onu, yoksa normal fiyatı

        [BsonIgnore]
        public bool HasDiscount => DiscountedPrice.HasValue && DiscountedPrice.Value < Price;

        [BsonIgnore]
        public decimal DiscountPercentage
        {
            get
            {
                if (HasDiscount && Price > 0)
                {
                    return Math.Round(((Price - DiscountedPrice!.Value) / Price) * 100, 0);
                }
                return 0;
            }
        }
    }
}