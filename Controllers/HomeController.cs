using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CS308Main.Models;
using System.Security.Claims;
using CS308Main.Data;

namespace CS308Main.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logService;
        private readonly IMongoDBRepository<Product> _bookRepository;
        private readonly IMongoDBRepository<Order> _purchaseRepository;
        private readonly IMongoDBRepository<Category> _genreRepository;

        public HomeController(
            ILogger<HomeController> logService, 
            IMongoDBRepository<Product> bookRepository, 
            IMongoDBRepository<Order> purchaseRepository, 
            IMongoDBRepository<Category> genreRepository)
        {
            _logService = logService;
            _bookRepository = bookRepository;
            _purchaseRepository = purchaseRepository;
            _genreRepository = genreRepository;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
                ViewBag.UserRole = currentUserRole;

                var bookItems = await _bookRepository.GetAllAsync();
                
                // Eğer order yoksa boş dictionary
                var purchaseRecords = await _purchaseRepository.GetAllAsync();
                var bookRatings = new Dictionary<string, int>();
                
                if (purchaseRecords != null && purchaseRecords.Any())
                {
                    bookRatings = purchaseRecords
                        .SelectMany(order => order.Items)
                        .GroupBy(item => item.ProductId)
                        .Select(group => new { 
                            BookId = group.Key, 
                            TotalAmount = group.Sum(item => item.Quantity) 
                        })
                        .ToDictionary(item => item.BookId, item => item.TotalAmount);
                }

                ViewBag.ProductPopularity = bookRatings;

                // Categories - boş olsa bile hata vermesin
                var genreList = await _genreRepository.GetAllAsync();
                if (genreList != null && genreList.Any())
                {
                    ViewBag.Genres = genreList
                        .Select(genre => genre.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList();
                }
                else
                {
                    ViewBag.Genres = new List<string>();
                }

                foreach (var book in bookItems)
                {
                    if (string.IsNullOrEmpty(book.Author))
                    {
                        book.Author = "Unknown Manufacturer";
                    }
                }

                return View(bookItems);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "Error loading home page");
                // Boş liste dön, hata verme
                ViewBag.Genres = new List<string>();
                ViewBag.ProductPopularity = new Dictionary<string, int>();
                return View(new List<Product>());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
            });
        }
    }
}