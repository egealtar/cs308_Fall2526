
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using CS308Main.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "StockManager,ProductManager")]
    public class StockController : Controller
    {
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<StockController> _logger;

        public StockController(IMongoDatabase database, ILogger<StockController> logger)
        {
            _products = database.GetCollection<Product>("Products");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string stockLevel = "All", int lowStockThreshold = 10)
        {
            try
            {
                var allProducts = await _products.Find(_ => true).ToListAsync();

                IEnumerable<Product> filtered = allProducts;

                if (string.Equals(stockLevel, "LowStock", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = allProducts.Where(p => p.QuantityInStock > 0 && p.QuantityInStock <= lowStockThreshold);
                }
                else if (string.Equals(stockLevel, "OutOfStock", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = allProducts.Where(p => p.QuantityInStock == 0);
                }

                var finalList = filtered
                                .OrderBy(p => p.Name)
                                .ToList();

                ViewBag.SelectedStockLevel = stockLevel;
                ViewBag.LowStockThreshold = lowStockThreshold;
                return View(finalList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products for stock management");
                return View(Enumerable.Empty<Product>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(string productId, int newQuantity)
        {
            if (string.IsNullOrEmpty(productId))
            {
                TempData["ErrorMessage"] = "Product ID is missing.";
                return RedirectToAction(nameof(Index));
            }

            if (newQuantity < 0)
            {
                TempData["ErrorMessage"] = "Stock quantity cannot be negative.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var product = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                var oldQuantity = product.QuantityInStock;
                product.QuantityInStock = newQuantity;

                var update = Builders<Product>.Update.Set(p => p.QuantityInStock, newQuantity);
                await _products.UpdateOneAsync(p => p.Id == productId, update);

                TempData["SuccessMessage"] = $"Stock updated for {product.Name}: {oldQuantity} → {newQuantity}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product stock");
                TempData["ErrorMessage"] = "Error updating product stock.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
