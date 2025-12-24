using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;
using MongoDB.Driver;

namespace CS308Main.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountController> _logger;

        private readonly IMongoCollection<ShoppingCart> _carts;

      public AccountController(IAuthService authService, ILogger<AccountController> logger,IMongoDatabase database)
        {
             _authService = authService;
             _logger = logger;
             _carts = database.GetCollection<ShoppingCart>("ShoppingCarts");
        }


        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if email already exists
            var existingUser = await _authService.GetUserByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "This email is already registered");
                return View(model);
            }

            // Validate role
            var validRoles = new[] { "Customer", "DeliveryAdmin", "ProductManager", "SalesManager" };
            if (!validRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Invalid role selected");
                return View(model);
            }

            // Create user with selected role
            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = _authService.HashPassword(model.Password),
                TaxId = model.TaxId ?? "",
                HomeAddress = model.HomeAddress,
                Role = model.Role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _authService.RegisterAsync(user);

            _logger.LogInformation($"New user registered: {user.Email} with role {user.Role}");

            TempData["Success"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.LoginAsync(model.Email, model.Password);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }
            
            await MergeGuestCartAsync(user.Id);


            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(1)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation($"User {user.Email} logged in with role {user.Role}");

            TempData["Success"] = $"Welcome back, {user.Name}!";

            // Redirect based on role
            return user.Role switch
            {
                "DeliveryAdmin" => RedirectToAction("Index", "Delivery"),
                "ProductManager" => RedirectToAction("Index", "Stock"),
                "SalesManager" => RedirectToAction("AllOrders", "Order"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out");
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _authService.GetUserByIdAsync(userId ?? "");

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(user);
        }

        private async Task MergeGuestCartAsync(string userId)
{
    const string guestUserId = "guest_user";

    // guest_user için sepet var mı?
    var guestCart = await _carts.Find(c => c.UserId == guestUserId).FirstOrDefaultAsync();
    if (guestCart == null || guestCart.Items == null || !guestCart.Items.Any())
    {
        return; // merge edilecek bir şey yok
    }

    // Kullanıcının zaten bir sepeti var mı?
    var userCart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();

    if (userCart == null)
    {
        // Direkt guest sepetini bu kullanıcıya taşı
        guestCart.UserId = userId;
        await _carts.ReplaceOneAsync(c => c.Id == guestCart.Id, guestCart);
    }
    else
    {
        // İki sepeti birleştir
        foreach (var guestItem in guestCart.Items)
        {
            var existing = userCart.Items
                .FirstOrDefault(i => i.ProductId == guestItem.ProductId);

            if (existing == null)
            {
                userCart.Items.Add(guestItem);
            }
            else
            {
                existing.Quantity += guestItem.Quantity;
            }
        }

        await _carts.ReplaceOneAsync(c => c.Id == userCart.Id, userCart);

        await _carts.DeleteOneAsync(c => c.UserId == guestUserId);
    }

    _logger.LogInformation($"Merged guest cart into user {userId}");
}

    }
}