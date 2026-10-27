using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public static class AutomatedSmsOccurrenceStatus
    {
        public const string Pending = "Pending";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string NothingToSend = "NothingToSend";
        public const string Missed = "Missed";
        public const string Error = "Error";
        public const string Cancelled = "Cancelled";
    }

    public class AutomatedSmsOccurrence
    {
        [Key]
        public long Id { get; set; }
        public int SettingId { get; set; }
        [ForeignKey(nameof(SettingId))]
        public virtual AutomatedSmsSetting Setting { get; set; } = null!;
        public DateTime ScheduledForLocal { get; set; }
        public DateTime ScheduledForUtc { get; set; }
        [Required, StringLength(60)]
        public string OccurrenceKey { get; set; } = "";
        [Required, StringLength(20)]
        public string Status { get; set; } = AutomatedSmsOccurrenceStatus.Pending;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalRecipients { get; set; }
        public int QueuedCount { get; set; }
        public int SkippedCount { get; set; }
        [StringLength(500)]
        public string? Note { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
