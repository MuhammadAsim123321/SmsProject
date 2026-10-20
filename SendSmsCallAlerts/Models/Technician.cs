using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class Technician
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        public string ContactNumber { get; set; }

        [StringLength(200)]
        public string? StreetAddress { get; set; }

        [StringLength(100)]
        public string? CityState { get; set; }

        [StringLength(10)]
        public string? ZipCode { get; set; }

        // Comma-separated list of IvrOption IDs (e.g. "1,3,5")
        public string? SelectedCategoryIds { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        // Navigation property for assigned contacts
        public virtual ICollection<ContactDetail>? AssignedContacts { get; set; }
    }
}
