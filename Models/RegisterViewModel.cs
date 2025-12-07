using System.ComponentModel.DataAnnotations;

namespace CS308Main.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [StringLength(20)]
        public string TaxId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Home address is required")]
        [StringLength(500)]
        public string HomeAddress { get; set; } = string.Empty;

        // YENİ: Role selection
        [Required(ErrorMessage = "Please select a role")]
        public string Role { get; set; } = "Customer";
    }
}