using CS308Main.Models;

namespace CS308Main.Services
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
    }

    public class MockPaymentService
    {
        private readonly ILogger<MockPaymentService> _logger;

        public MockPaymentService(ILogger<MockPaymentService> logger)
        {
            _logger = logger;
        }

        public bool ValidateCardNumber(string cardNumber)
        {
            // Remove spaces and dashes
            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

            // Sadece digit mi ve uzunluk uygun mu kontrol et
            if (string.IsNullOrEmpty(cardNumber))
                return false;

            if (!cardNumber.All(char.IsDigit))
                return false;

            if (cardNumber.Length < 13 || cardNumber.Length > 19)
                return false;

            // HER KART GEÇERLİ!
            _logger.LogInformation($"Card accepted: {cardNumber.Substring(0, 4)}****");
            return true;
        }

        public PaymentResult ProcessPayment(string cardNumber, decimal amount)
        {
            // Simulate payment processing
            _logger.LogInformation($"Processing payment of ${amount} with card ending in {cardNumber.Substring(cardNumber.Length - 4)}");

            // Mock validation (always succeeds for demo)
            if (amount <= 0)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Invalid amount"
                };
            }

            // Simulate successful payment
            var transactionId = $"TXN-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";

            _logger.LogInformation($"Payment successful. Transaction ID: {transactionId}");

            return new PaymentResult
            {
                Success = true,
                Message = "Payment processed successfully",
                TransactionId = transactionId
            };
        }

        // Test card numbers for demo purposes
        public static List<string> GetTestCardNumbers()
        {
            return new List<string>
            {
                "4532015112830366", // Visa
                "5425233430109903", // Mastercard
                "374245455400126",  // Amex
                "6011111111111117", // Discover
                "1234567890123456", // Any number will work now
                "0000000000000000"  // Even this works!
            };
        }
    }
}