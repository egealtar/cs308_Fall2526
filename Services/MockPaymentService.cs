namespace CS308Main.Services
{
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

            // Check if all characters are digits
            if (!cardNumber.All(char.IsDigit))
            {
                return false;
            }

            // Check length (13-19 digits)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
            {
                return false;
            }

            // Luhn Algorithm
            int sum = 0;
            bool alternate = false;

            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(cardNumber[i].ToString());

                if (alternate)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                alternate = !alternate;
            }

            return (sum % 10 == 0);
        }

        public PaymentResult ProcessPayment(string cardNumber, decimal amount)
        {
            _logger.LogInformation($"Processing payment of ${amount} with card ending in {cardNumber.Substring(cardNumber.Length - 4)}");

            // Simulate payment processing
            if (!ValidateCardNumber(cardNumber))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Invalid card number",
                    TransactionId = null
                };
            }

            // Simulate successful payment
            var transactionId = Guid.NewGuid().ToString();
            
            _logger.LogInformation($"Payment successful. Transaction ID: {transactionId}");

            return new PaymentResult
            {
                Success = true,
                Message = "Payment processed successfully",
                TransactionId = transactionId
            };
        }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
    }
}