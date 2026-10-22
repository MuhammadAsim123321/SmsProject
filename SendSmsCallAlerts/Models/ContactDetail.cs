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
        public string Name { get; set; }

        // LEGACY: Kept for backward compatibility - computed property
        [NotMapped]
        public string? Address
        {
            get
            {
                var parts = new[] { StreetAddress, CityState, ZipCode }.Where(p => !string.IsNullOrWhiteSpace(p));
                return parts.Any() ? string.Join(", ", parts) : null;
            }
        }

        // NEW: Split address fields
        public string? StreetAddress { get; set; }
        public string? CityState { get; set; }
        public string? ZipCode { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        // Per-contact service category. Kept separate from Scheduler.templateOrAudioId
        // so updating one contact cannot change the category/template of shared schedulers.
        public int? CategoryId { get; set; }

        //public string? FormNo { get; set; }

        // NEW: Technician assignment
        public int? AssignedTechnicianId { get; set; }

        [ForeignKey("AssignedTechnicianId")]
        public virtual Technician? AssignedTechnician { get; set; }

        // Navigation property for Table 2
        public virtual ICollection<JobSchedule> JobSchedules { get; set; }
    }
}