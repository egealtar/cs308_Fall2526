using System;
using System.Linq;
using System.Threading.Tasks;
using CS308Main.Models;

namespace CS308Main.Services
{
    public class MockPaymentService : IMockPaymentService
    {
        public Task<MockPaymentStatus> AuthorizeAsync(CardPaymentInput input)
        {
            if (!IsPlausibleCard(input.CardNumber) || !PassesLuhn(input.CardNumber))
                return Task.FromResult(MockPaymentStatus.Declined);

            if (IsExpired(input.ExpMonth, input.ExpYear))
                return Task.FromResult(MockPaymentStatus.Declined);

            if (input.Amount <= 0)
                return Task.FromResult(MockPaymentStatus.Declined);

            if (input.Cvv == "000")
                return Task.FromResult(MockPaymentStatus.Declined);

            return Task.FromResult(MockPaymentStatus.Approved);
        }

        private bool IsPlausibleCard(string cardNumber)
        {
            var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
            return digits.Length >= 12 && digits.Length <= 19;
        }

        private bool IsExpired(int month, int year)
        {
            var lastDayOfMonth = new DateTime(year, month, 1)
                .AddMonths(1)
                .AddDays(-1);

            return lastDayOfMonth.Date < DateTime.UtcNow.Date;
        }

        private bool PassesLuhn(string cardNumber)
        {
            var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
            int sum = 0;
            bool doubleIt = false;

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }

            return sum % 10 == 0;
        }
    }
}