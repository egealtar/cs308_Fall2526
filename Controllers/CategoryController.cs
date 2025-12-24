using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "ProductManager")]
    public class CategoryController : Controller
    {
        private readonly IMongoCollection<Category> _categories;
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(IMongoDatabase database, ILogger<CategoryController> logger)
        {
            _categories = database.GetCollection<Category>("Categories");
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _categories
                .Find(_ => true)
                .SortBy(c => c.Name)
                .ToListAsync();

            // Get product count for each category
            var categoryStats = new List<CategoryStats>();
            foreach (var category in categories)
            {
                var productCount = await _products.CountDocumentsAsync(
                    Builders<Product>.Filter.Eq(p => p.Genre, category.Name));
                
                categoryStats.Add(new CategoryStats
                {
                    Category = category,
                    ProductCount = (int)productCount
                });
            }

            return View(categoryStats);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (!ModelState.IsValid)
            {
                return View(category);
            }

            // Check if category already exists
            var existing = await _categories.Find(c => c.Name == category.Name).FirstOrDefaultAsync();
            if (existing != null)
            {
                ModelState.AddModelError("Name", "Category with this name already exists");
                return View(category);
            }

            await _categories.InsertOneAsync(category);
            _logger.LogInformation($"Category '{category.Name}' created by Product Manager");
            TempData["Success"] = $"Category '{category.Name}' created successfully!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var category = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            // Check if another category with the same name exists
            var existing = await _categories.Find(c => c.Name == category.Name && c.Id != id).FirstOrDefaultAsync();
            if (existing != null)
            {
                ModelState.AddModelError("Name", "Category with this name already exists");
                return View(category);
            }

            var oldCategory = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
            var oldName = oldCategory?.Name;

            var update = Builders<Category>.Update
                .Set(c => c.Name, category.Name)
                .Set(c => c.Description, category.Description);

            await _categories.UpdateOneAsync(c => c.Id == id, update);

            // Update all products with the old category name to the new name
            if (oldName != null && oldName != category.Name)
            {
                var productUpdate = Builders<Product>.Update.Set(p => p.Genre, category.Name);
                await _products.UpdateManyAsync(
                    Builders<Product>.Filter.Eq(p => p.Genre, oldName),
                    productUpdate);
            }

            _logger.LogInformation($"Category '{category.Name}' updated by Product Manager");
            TempData["Success"] = $"Category '{category.Name}' updated successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var category = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (category == null)
            {
                TempData["Error"] = "Category not found";
                return RedirectToAction("Index");
            }

            // Check if any products use this category
            var productCount = await _products.CountDocumentsAsync(
                Builders<Product>.Filter.Eq(p => p.Genre, category.Name));

            if (productCount > 0)
            {
                TempData["Error"] = $"Cannot delete category '{category.Name}' because it is used by {productCount} product(s).";
                return RedirectToAction("Index");
            }

            await _categories.DeleteOneAsync(c => c.Id == id);
            _logger.LogInformation($"Category '{category.Name}' deleted by Product Manager");
            TempData["Success"] = $"Category '{category.Name}' deleted successfully!";
            return RedirectToAction("Index");
        }
    }

    public class CategoryStats
    {
        public Category Category { get; set; } = new Category();
        public int ProductCount { get; set; }
    }
}