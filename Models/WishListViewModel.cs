namespace CS308Main.Models
{
    public class WishListViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int QuantityInStock { get; set; }
        public DateTime AddedAt { get; set; }
        public bool IsAvailable { get; set; }
        public bool HasDiscount { get; set; }
    }
}