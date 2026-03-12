using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class JobSchedule
    {
        [Key]
        public int Id { get; set; }

        public string Category { get; set; }

        public string? Scheduler { get; set; } // ✅ Make nullable

        //public string JobBook { get; set; }
        public DateTime? JobBookDate { get; set; }

        public bool IsPaused { get; set; } = false; // ✅ NEW: For pause feature


        //public string Detail { get; set; }

        // Foreign Key linking to Table 1
        [Required]
        public int ContactDetailId { get; set; }

        [ForeignKey("ContactDetailId")]
        public virtual ContactDetail ContactDetail { get; set; }
    }
}