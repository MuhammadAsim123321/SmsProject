using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class ProfileDetailsViewModel
    {
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? Email { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = "";

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [Required]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class ProfilePageViewModel
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = AppRoles.Staff;
        public string? Phone { get; set; }
        public ProfileDetailsViewModel Profile { get; set; } = new();
        public ChangePasswordViewModel Password { get; set; } = new();
        public bool PasswordOpen { get; set; }
    }
}