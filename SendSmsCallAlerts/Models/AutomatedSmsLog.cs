using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    /// <summary>
    /// One row per (setting, occurrence, recipient). The unique index on these columns
    /// is what guarantees the same SMS is never sent twice for the same occurrence.
    /// </summary>
    public class AutomatedSmsLog
    {
        [Key]
        public long Id { get; set; }

        public int SettingId { get; set; }

        [ForeignKey(nameof(SettingId))]
        public virtual AutomatedSmsSetting Setting { get; set; }

        /// <summary>Business date of the run, e.g. "2026-10-25". Test runs use "TEST-yyyyMMddHHmmss".</summary>
        [Required, StringLength(50)]
        public string OccurrenceKey { get; set; }

        [Required, StringLength(20)]
        public string RecipientType { get; set; }

        /// <summary>TechnicianId or ContactDetailId depending on RecipientType.</summary>
        public int RecipientId { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        /// <summary>Row written to SmsDetails for this message (if any).</summary>
        public int? SmsDetailId { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        public DateTime? SentAt { get; set; }          // UTC
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}