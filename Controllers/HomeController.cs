using CS308Main.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Diagnostics;

namespace CS308Main.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<Rating> _ratings;

        public HomeController(ILogger<HomeController> logger, IMongoDatabase database)
        {
            _logger = logger;
            _products = database.GetCollection<Product>("Products");
            _ratings = database.GetCollection<Rating>("Ratings");
        }

        public async Task<IActionResult> Index(string? genre = null, string? search = null, string? sort = null)
        {
            var filterBuilder = Builders<Product>.Filter;
            var filter = filterBuilder.Empty;

            // Genre filter (URL'de genre parametresi kullan)
            if (!string.IsNullOrEmpty(genre))
            {
                filter = filter & filterBuilder.Eq(p => p.Genre, genre);
            }

            // Search filter - only search by Name and Description
            if (!string.IsNullOrEmpty(search))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(p => p.Name, new BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(p => p.Description, new BsonRegularExpression(search, "i"))
                );
                filter = filter & searchFilter;
            }

            // Get products
            var query = _products.Find(filter);

            // Sorting
            List<Product> products;
            if (sort == "popularity")
            {
                // For popularity, we need to fetch all products first, then sort in memory
                products = await query.ToListAsync();
                
                // Get popularity data for all products based on ratings
                var productIds = products.Select(p => p.Id).ToList();
                var ratings = await _ratings.Find(r => productIds.Contains(r.ProductId)).ToListAsync();
                
                // Calculate popularity score for each product based on ratings only
                var popularityScores = products.Select(product =>
                {
                    var productRatings = ratings.Where(r => r.ProductId == product.Id).ToList();
                    
                    var ratingCount = productRatings.Count;
                    var averageRating = ratingCount > 0 ? productRatings.Average(r => r.Score) : 0;
                    
                    // Popularity score = (rating count * 2) + (average rating * 10)
                    // This gives more weight to products with both high ratings and many ratings
                    var popularityScore = (ratingCount * 2) + (averageRating * 10);
                    
                    return new { Product = product, Score = popularityScore };
                }).OrderByDescending(x => x.Score).Select(x => x.Product).ToList();
                
                products = popularityScores;
            }
            else
            {
                // Standard sorting
                query = sort switch
                {
                    "price_asc" => query.SortBy(p => p.Price),
                    "price_desc" => query.SortByDescending(p => p.Price),
                    "name_asc" => query.SortBy(p => p.Name),
                    "name_desc" => query.SortByDescending(p => p.Name),
                    _ => query.SortBy(p => p.Name)
                };
                products = await query.ToListAsync();
            }

            // Get all genres (categories)
            var allProducts = await _products.Find(filterBuilder.Empty).ToListAsync();
            ViewBag.Categories = allProducts
                .Select(p => p.Genre)
                .Distinct()
                .Where(g => !string.IsNullOrEmpty(g))
                .OrderBy(g => g)
                .ToList();

            ViewBag.CurrentGenre = genre;  // genre parametresini gönder
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSort = sort;

            _logger.LogInformation($"Loaded {products.Count} products");

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}