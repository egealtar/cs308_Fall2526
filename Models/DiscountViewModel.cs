using System.ComponentModel.DataAnnotations;

namespace CS308Main.Models
{
    public class DiscountViewModel
    {
        [Required(ErrorMessage = "Please select at least one product")]
        public List<string> ProductIds { get; set; } = new List<string>();

        [Required(ErrorMessage = "Discount percentage is required")]
        [Range(1, 99, ErrorMessage = "Discount must be between 1% and 99%")]
        public decimal DiscountPercentage { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);
    }
}