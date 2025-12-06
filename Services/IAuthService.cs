using CS308Main.Models;

namespace CS308Main.Services
{
    public interface IAuthService
    {
        Task<User?> RegisterAsync(RegisterViewModel model);
        Task<User?> LoginAsync(string email, string password);
        Task<User?> GetUserByIdAsync(string userId);
        Task<User?> GetUserByEmailAsync(string email);
        string HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash);
    }
}