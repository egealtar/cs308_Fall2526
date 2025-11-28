using System.Threading.Tasks;
using CS308Main.Models;

namespace CS308Main.Services
{
    public enum MockPaymentStatus
    {
        Approved,
        Declined
    }

    public interface IMockPaymentService
    {
        Task<MockPaymentStatus> AuthorizeAsync(CardPaymentInput input);
    }
}