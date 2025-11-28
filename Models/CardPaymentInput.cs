using System;
using System.ComponentModel.DataAnnotations;

namespace CS308Main.Models
{
    public class CardPaymentInput
    {
        [Required]
        public string OrderId { get; set; } = string.Empty;

        [Display(Name = "Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Card number is required.")]
        [Display(Name = "Card number")]
        [StringLength(19, MinimumLength = 12, ErrorMessage = "Card number length is not valid.")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name on card is required.")]
        [Display(Name = "Name on card")]
        [StringLength(80)]
        public string CardHolder { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiration month is required.")]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
        public int ExpMonth { get; set; }

        [Required(ErrorMessage = "Expiration year is required.")]
        [Range(2024, 2100, ErrorMessage = "Year must be 2024 or later.")]
        public int ExpYear { get; set; }

        [Required(ErrorMessage = "CVV is required.")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
        public string Cvv { get; set; } = string.Empty;
    }
}