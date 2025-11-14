
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CS308Main.Data;
using CS308Main.Models; // Product modelinin burada olduğunu varsayıyorum
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic; // IEnumerable için eklendi

namespace CS308Main.Controllers
{
    // Yetkilendirme rolünü "StockManager" olarak değiştirdim, 
    // isterseniz "SalesManager" olarak da kullanabilirsiniz.
    [Authorize(Roles = "StockManager")] 
    public class StockController : Controller
    {
        // Order yerine Product (Ürün) repository'si kullanıyoruz
        private readonly IMongoDBRepository<Product> _productRepository;
        private readonly ILogger<StockController> _logger;

        public StockController(
            IMongoDBRepository<Product> productRepository,
            ILogger<StockController> logger)
        {
            _productRepository = productRepository;
            _logger = logger;
        }

        // STOK LİSTELEME (INDEX)
        // DeliveryController'daki 'status' filtresine benzer 
        // bir 'stockLevel' filtresi ekledim (Tümü, Az Kalan, Tükendi).
        [HttpGet]
        public async Task<IActionResult> Index(string stockLevel = "All", int lowStockThreshold = 10)
        {
            try
            {
                var allProducts = await _productRepository.GetAllAsync();

                IEnumerable<Product> filtered = allProducts;

                if (string.Equals(stockLevel, "LowStock", StringComparison.OrdinalIgnoreCase))
                {
                    // Stok adedi 0'dan büyük VE eşik değerden (örn: 10) az olanlar
                    filtered = allProducts.Where(p => p.StockQuantity > 0 && p.StockQuantity <= lowStockThreshold);
                }
                else if (string.Equals(stockLevel, "OutOfStock", StringComparison.OrdinalIgnoreCase))
                {
                    // Stok adedi 0 olanlar
                    filtered = allProducts.Where(p => p.StockQuantity == 0);
                }
                // "All" ise filtreleme yapılmaz, tümü listelenir.

                var finalList = filtered
                                .OrderBy(p => p.Name) // Stokları isme göre sıralayalım
                                .ToList();

                ViewBag.SelectedStockLevel = stockLevel;
                ViewBag.LowStockThreshold = lowStockThreshold; // View'da göstermek için
                return View(finalList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products for stock management");
                // Hata durumunda boş bir liste döndür
                return View(Enumerable.Empty<Product>()); 
            }
        }

        // STOK GÜNCELLEME
        // DeliveryController'daki UpdateStatus'a karşılık gelir.
        // Bu metod, bir ürünün stok miktarını doğrudan belirler (örn: 50 yapar).
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
                // Arkadaşının yaptığı gibi (performanslı olmasa da)
                // FindByIdAsync yerine GetAllAsync kullan:
                var allProducts = await _productRepository.GetAllAsync();
                var product = allProducts.FirstOrDefault(p => p.Id == productId);
                
                // NOT: Tıpkı DeliveryController'daki gibi, verimlilik açısından
                // _productRepository.FindByIdAsync(productId) kullanmak daha iyidir.

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                var oldQuantity = product.StockQuantity;
                product.StockQuantity = newQuantity; 
                // product.UpdatedAt = DateTime.UtcNow; // Modelde varsa

                // ReplaceOneAsync yerine UpdateAsync kullan
                await _productRepository.UpdateAsync(productId, product);
                
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
        
        // NOT: DeliveryController'da MarkAsShipped gibi özel metotlar var
        // çünkü siparişin "durumları" (workflow) bellidir.
        // Stokta ise "durum" yoktur, "miktar" vardır. 
        // Bu nedenle tek bir UpdateStock metodu genellikle yeterlidir.
        // İhtiyaç duyarsanız "MarkAsOutOfStock(string productId)" gibi
        // UpdateStock(productId, 0) işlemini çağıran yardımcı metotlar ekleyebilirsiniz.
    }
}
