using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class ContactDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        // computed from the three fields below, not stored
        [NotMapped]
        public string? Address
        {
            get
            {
                var parts = new[] { StreetAddress, CityState, ZipCode }.Where(p => !string.IsNullOrWhiteSpace(p));
                return parts.Any() ? string.Join(", ", parts) : null;
            }
        }

        public string? StreetAddress { get; set; }
        public string? CityState { get; set; }
        public string? ZipCode { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        // the customer's appointments (category and technician now live on Job)
        public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}