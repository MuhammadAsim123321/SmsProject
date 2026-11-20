using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters.")]
        [RegularExpression(@"^[A-Za-z0-9_.]+$", ErrorMessage = "Username may contain only letters, digits, underscores, and dots.")]
        public string Username { get; set; } = "";
        public string Password { get; set; }

        public string? Role { get; set; }
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        public string? Name { get; set; }
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? UsernameNormalized { get; set; }
        public string? EmailNormalized { get; set; }
    }
}
