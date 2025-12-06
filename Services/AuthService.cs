using CS308Main.Models;
using CS308Main.Data;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace CS308Main.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMongoCollection<User> _users;

        public AuthService(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("Users");
        }

        public async Task<User?> RegisterAsync(RegisterViewModel model)
        {
            // Check if user already exists
            var existingUser = await _users.Find(u => u.Email == model.Email).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return null; // User already exists
            }

            // Create new user
            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                TaxId = model.TaxId,
                HomeAddress = model.HomeAddress,
                Role = "Customer",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _users.InsertOneAsync(user);
            return user;
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            
            if (user == null || !VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            if (!user.IsActive)
            {
                return null;
            }

            return user;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        }

        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == passwordHash;
        }
    }
}