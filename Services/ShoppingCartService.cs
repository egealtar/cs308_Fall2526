using CS308Main.Models;
using MongoDB.Driver;

namespace CS308Main.Services
{
    public class ShoppingCartService
    {
        private readonly IMongoCollection<ShoppingCart> _carts;
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<ShoppingCartService> _logger;

        public ShoppingCartService(IMongoDatabase database, ILogger<ShoppingCartService> logger)
        {
            _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        public async Task<ShoppingCart?> GetCartAsync(string userId)
        {
            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
            
            if (cart == null)
            {
                return null;
            }

            // Update availability status for each item
            foreach (var item in cart.Items)
            {
                var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
                if (product != null)
                {
                    item.IsAvailable = product.QuantityInStock >= item.Quantity;
                }
            }

            return cart;
        }

        public async Task AddToCartAsync(string userId, string productId, int quantity)
        {
            var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
            
            if (product == null)
            {
                throw new Exception("Product not found");
            }

            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();

            if (cart == null)
            {
                // Create new cart
                cart = new ShoppingCart
                {
                    UserId = userId,
                    Items = new List<CartItem>
                    {
                        new CartItem
                        {
                            ProductId = productId,
                            Quantity = quantity,
                            IsAvailable = product.QuantityInStock >= quantity
                        }
                    }
                };

                await _carts.InsertOneAsync(cart);
            }
            else
            {
                // Update existing cart
                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    existingItem.IsAvailable = product.QuantityInStock >= existingItem.Quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        IsAvailable = product.QuantityInStock >= quantity
                    });
                }

                var update = Builders<ShoppingCart>.Update.Set(c => c.Items, cart.Items);
                await _carts.UpdateOneAsync(c => c.UserId == userId, update);
            }

            _logger.LogInformation($"Added {quantity} of product {productId} to cart for user {userId}");
        }

        public async Task UpdateQuantityAsync(string userId, string productId, int quantity)
        {
            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
            
            if (cart == null)
            {
                return;
            }

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            
            if (item != null)
            {
                var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
                
                if (product != null)
                {
                    item.Quantity = quantity;
                    item.IsAvailable = product.QuantityInStock >= quantity;

                    var update = Builders<ShoppingCart>.Update.Set(c => c.Items, cart.Items);
                    await _carts.UpdateOneAsync(c => c.UserId == userId, update);

                    _logger.LogInformation($"Updated quantity to {quantity} for product {productId} in cart for user {userId}");
                }
            }
        }

        public async Task RemoveFromCartAsync(string userId, string productId)
        {
            var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
            
            if (cart == null)
            {
                return;
            }

            cart.Items.RemoveAll(i => i.ProductId == productId);

            if (cart.Items.Any())
            {
                var update = Builders<ShoppingCart>.Update.Set(c => c.Items, cart.Items);
                await _carts.UpdateOneAsync(c => c.UserId == userId, update);
            }
            else
            {
                await _carts.DeleteOneAsync(c => c.UserId == userId);
            }

            _logger.LogInformation($"Removed product {productId} from cart for user {userId}");
        }

        public async Task ClearCartAsync(string userId)
        {
            await _carts.DeleteOneAsync(c => c.UserId == userId);
            _logger.LogInformation($"Cleared cart for user {userId}");
        }
    }
}