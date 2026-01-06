using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using CS308Main.Models;
using CS308Main.Services;
using System.Security.Claims;

namespace CS308Main.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IMongoCollection<User> _users;
        private readonly IAuthService _authService;
        private readonly ILogger<AdminController> _logger;

        private readonly string[] _validRoles = new[]
        {
            "Customer",
            "SalesManager",
            "ProductManager",
            "SupportAgent",
            "Admin"
        };

        public AdminController(
            IMongoDatabase database,
            IAuthService authService,
            ILogger<AdminController> logger)
        {
            _users = database.GetCollection<User>("Users");
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm = null, string? roleFilter = null)
        {
            var filterBuilder = Builders<User>.Filter;
            var filter = filterBuilder.Empty;

            // Search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(u => u.Name, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
                    filterBuilder.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"))
                );
                filter = filter & searchFilter;
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(roleFilter) && _validRoles.Contains(roleFilter))
            {
                filter = filter & filterBuilder.Eq(u => u.Role, roleFilter);
            }

            var users = await _users.Find(filter)
                .SortBy(u => u.Name)
                .ToListAsync();

            var userViewModels = users.Select(u => new UserManagementViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                TaxId = u.TaxId,
                HomeAddress = u.HomeAddress
            }).ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.ValidRoles = _validRoles;
            ViewBag.RoleCounts = _validRoles.ToDictionary(
                r => r,
                r => users.Count(u => u.Role == r)
            );

            return View(userViewModels);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new UserManagementViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                TaxId = user.TaxId,
                HomeAddress = user.HomeAddress
            };

            ViewBag.ValidRoles = _validRoles;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(ChangeRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid request";
                return RedirectToAction("Index");
            }

            if (!_validRoles.Contains(model.NewRole))
            {
                TempData["Error"] = "Invalid role selected";
                return RedirectToAction("Index");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _authService.GetUserByIdAsync(model.UserId);

            if (user == null)
            {
                TempData["Error"] = "User not found";
                return RedirectToAction("Index");
            }

            // Prevent admin from removing their own admin role
            if (user.Id == currentUserId && model.NewRole != "Admin")
            {
                TempData["Error"] = "You cannot remove your own Admin role";
                return RedirectToAction("Index");
            }

            var oldRole = user.Role;
            user.Role = model.NewRole;

            var update = Builders<User>.Update
                .Set(u => u.Role, model.NewRole);

            await _users.UpdateOneAsync(u => u.Id == model.UserId, update);

            _logger.LogInformation($"Admin {currentUserId} changed user {user.Email} role from {oldRole} to {model.NewRole}");

            TempData["Success"] = $"User role changed from {oldRole} to {model.NewRole}";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _authService.GetUserByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] = "User not found";
                return RedirectToAction("Index");
            }

            // Prevent admin from deactivating themselves
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot deactivate your own account";
                return RedirectToAction("Index");
            }

            var newStatus = !user.IsActive;
            var update = Builders<User>.Update
                .Set(u => u.IsActive, newStatus);

            await _users.UpdateOneAsync(u => u.Id == userId, update);

            _logger.LogInformation($"Admin {currentUserId} {(newStatus ? "activated" : "deactivated")} user {user.Email}");

            TempData["Success"] = $"User account has been {(newStatus ? "activated" : "deactivated")}";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var allUsers = await _users.Find(_ => true).ToListAsync();

            var stats = new
            {
                TotalUsers = allUsers.Count,
                ActiveUsers = allUsers.Count(u => u.IsActive),
                InactiveUsers = allUsers.Count(u => !u.IsActive),
                RoleDistribution = _validRoles.ToDictionary(
                    r => r,
                    r => allUsers.Count(u => u.Role == r)
                ),
                RecentRegistrations = allUsers
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(10)
                    .Select(u => new
                    {
                        u.Name,
                        u.Email,
                        u.Role,
                        u.CreatedAt
                    })
                    .ToList()
            };

            ViewBag.Stats = stats;
            return View();
        }
    }
}

