using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using CS308Main.Models;

namespace CS308Main.Controllers
{
    public class HomeController : Controller
    {
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IMongoDatabase database, ILogger<HomeController> logger)
        {
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? genre = null, string? search = null, string? sort = null)
        {
            // Redirect non-customer roles to their task-focused interfaces
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("SupportAgent"))
                {
                    return RedirectToAction("Index", "SupportChat");
                }
                if (User.IsInRole("SalesManager"))
                {
                    return RedirectToAction("AllOrders", "Order");
                }
                if (User.IsInRole("ProductManager"))
                {
                    return RedirectToAction("Index", "Stock");
                }
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                // If authenticated but not Customer, redirect to appropriate page
                if (!User.IsInRole("Customer"))
                {
                    return RedirectToAction("Index", "Home"); // Fallback, but shouldn't happen
                }
            }

            var filterBuilder = Builders<Product>.Filter;
            var filter = filterBuilder.Empty;

            // Genre filter
            if (!string.IsNullOrEmpty(genre))
            {
                filter = filter & filterBuilder.Eq(p => p.Genre, genre);
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
                filter = filter & searchFilter;
            }

            // Get all genres for filter dropdown
            var allProducts = await _products.Find(filterBuilder.Empty).ToListAsync();
            var genres = allProducts.Select(p => p.Genre).Distinct().OrderBy(g => g).ToList();

            // Query products
            var query = _products.Find(filter);

            // Sorting
            query = sort switch
            {
                "price_asc" => query.SortBy(p => p.Price),
                "price_desc" => query.SortByDescending(p => p.Price),
                "name_asc" => query.SortBy(p => p.Name),
                "name_desc" => query.SortByDescending(p => p.Name),
                _ => query.SortBy(p => p.Name)
            };

            var products = await query.ToListAsync();

            ViewBag.Genres = genres;
            ViewBag.CurrentGenre = genre;
            ViewBag.SearchQuery = search;
            ViewBag.SortOption = sort;

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}