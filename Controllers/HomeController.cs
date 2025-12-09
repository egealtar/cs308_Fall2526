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

        public HomeController(ILogger<HomeController> logger, IMongoDatabase database)
        {
            _logger = logger;
            _products = database.GetCollection<Product>("Products");
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

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(p => p.Name, new BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(p => p.Description, new BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(p => p.Author, new BsonRegularExpression(search, "i"))
                );
                filter = filter & searchFilter;
            }

            // Get products
            var query = _products.Find(filter);

            // Sorting
            query = sort switch
            {
                "price_asc" => query.SortBy(p => p.Price),
                "price_desc" => query.SortByDescending(p => p.Price),
                "name_asc" => query.SortBy(p => p.Name),
                "name_desc" => query.SortByDescending(p => p.Name),
                "popularity"     => query.SortByDescending(p => p.Popularity),
                _ => query.SortBy(p => p.Name)
            };

            var products = await query.ToListAsync();

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