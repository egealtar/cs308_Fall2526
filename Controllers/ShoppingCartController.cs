using Microsoft.AspNetCore.Mvc;
using CS308Main.Models;
using System.Linq;

namespace CS308Main.Controllers
{
    public class ShoppingCartController : Controller
    {
        // Şimdilik DEMO cart – Mongo yerine statik veri kullanıyoruz
        private static ShoppingCart _cart = new ShoppingCart
        {
            UserId = "test-user",
            Items = new List<CartItem>
            {
                new CartItem
                {
                    Id = "1",
                    ProductId = "101",
                    ProductName = "Sample Product A",
                    Price = 100,
                    UnitPrice = 100,
                    Quantity = 5,
                    QuantityInCart = 1,
                    AvailableQuantity = 5,
                    ImagePath = "/images/sample1.jpg"
                },
                new CartItem
                {
                    Id = "2",
                    ProductId = "102",
                    ProductName = "Sample Product B",
                    Price = 150,
                    UnitPrice = 150,
                    Quantity = 10,
                    QuantityInCart = 2,
                    AvailableQuantity = 10,
                    ImagePath = "/images/sample2.jpg"
                }
            }
        };

        // GET: /ShoppingCart
        public IActionResult Index()
        {
            // DEBUG için:
            // ViewBag.DebugMessage = "ShoppingCart Index çalıştı.";
            return View(_cart);
        }

        [HttpPost]
        public IActionResult UpdateQuantity(string cartItemId, int change)
        {
            var item = _cart.Items.FirstOrDefault(x => x.Id == cartItemId);

            if (item != null)
            {
                int newQty = item.QuantityInCart + change;

                if (newQty < 1)
                    newQty = 1;

                if (newQty > item.AvailableQuantity)
                {
                    newQty = item.AvailableQuantity;
                    TempData[$"QuantityError_{item.Id}"] = true;
                }

                item.QuantityInCart = newQty;
                _cart.UpdatedAt = DateTime.UtcNow;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveFromCart(string cartItemId)
        {
            var item = _cart.Items.FirstOrDefault(i => i.Id == cartItemId);
            if (item != null)
            {
                _cart.Items.Remove(item);
                _cart.UpdatedAt = DateTime.UtcNow;
            }

            return RedirectToAction("Index");
        }
    }
}
