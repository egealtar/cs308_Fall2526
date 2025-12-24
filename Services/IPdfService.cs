using CS308Main.Models;

namespace CS308Main.Services
{
    public interface IPdfService
    {
        Task<string> GenerateInvoicePdfAsync(Invoice invoice, User user);
    }
}