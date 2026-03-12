using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class ContactDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public string? Address { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        //public string? FormNo { get; set; }

        // Navigation property for Table 2
        public virtual ICollection<JobSchedule> JobSchedules { get; set; }
    }
}