using MongoDB.Driver;
using CS308Main.Models;
using System.Security.Cryptography;
using System.Text;

namespace CS308Main.Services
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(User user);
        Task<User?> LoginAsync(string email, string password);
        Task<User?> GetUserByIdAsync(string id);
        Task<User?> GetUserByEmailAsync(string email);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    public class AuthService : IAuthService
    {
        private readonly IMongoCollection<User> _users;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IMongoDatabase database, ILogger<AuthService> logger)
        {
            _users = database.GetCollection<User>("Users");
            _logger = logger;
        }

        public async Task<User> RegisterAsync(User user)
        {
            // Ensure the user object has all required fields
            if (string.IsNullOrEmpty(user.Role))
            {
                user.Role = "Customer"; // Default fallback
                _logger.LogWarning($"User {user.Email} registered without role, defaulting to Customer");
            }

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            await _users.InsertOneAsync(user);
            
            _logger.LogInformation($"User {user.Email} registered successfully with role: {user.Role}");
            
            return user;
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning($"Login attempt failed: User not found for email {email}");
                return null;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning($"Login attempt failed: User {email} is not active");
                return null;
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning($"Login attempt failed: Invalid password for {email}");
                return null;
            }

            _logger.LogInformation($"User {email} logged in successfully");
            return user;
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        }

        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        public bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == hash;
        }
    }
}