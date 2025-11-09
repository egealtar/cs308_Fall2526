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
            var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
            ViewBag.UserRole = currentUserRole;

            var bookItems = await _bookRepository.GetAllAsync();
            var purchaseRecords = await _purchaseRepository.GetAllAsync();

            var bookRatings = purchaseRecords
                .SelectMany(order => order.Items)
                .GroupBy(item => item.ProductId)
                .Select(group => new { 
                    BookId = group.Key, 
                    TotalAmount = group.Sum(item => item.Quantity) 
                })
                .ToDictionary(item => item.BookId, item => item.TotalAmount);

            ViewBag.ProductPopularity = bookRatings;

            var genreList = await _genreRepository.GetAllAsync();
            ViewBag.Genres = genreList
                .Select(genre => genre.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            foreach (var book in bookItems)
            {
                if (string.IsNullOrEmpty(book.Author))
                {
                    book.Author = "Unknown Author";
                }
            }

            return View(bookItems);
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