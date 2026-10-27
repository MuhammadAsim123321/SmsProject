using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    /// <summary>Delivery status shown in the UI (Task 9).</summary>
    public static class SmsLogStatus
    {
        public const string Pending = "Pending";             // SMS is in queue (also while waiting for a retry)
        public const string MessageSent = "Message Sent";    // accepted by the gateway (or simulated in Test mode)
        public const string SmsFailed = "SMS Failed";        // gave up after all attempts, or expired
        public const string Skipped = "Skipped";             // never queued: opted out / no phone number

        public static readonly string[] All = { Pending, MessageSent, SmsFailed, Skipped };
    }

    /// <summary>Where the message came from.</summary>
    public static class SmsLogSources
    {
        public const string Automated = "Automated";   // AutomatedSmsSetting rules (technician / customer notifications)
        public const string Manual = "Manual";         // typed by a user on the Send SMS page
        public const string JobPlan = "JobPlan";       // message plan of a job (SmsJob)
    }

    /// <summary>
    /// One row per SMS that the system tried to send. This is the single place to answer
    /// "was it sent, when, and if not, why". AutomatedSmsLogs stays as the "already handled
    /// this occurrence" guard; SmsLogs is the delivery record.
    /// </summary>
    [Table("SmsLogs")]
    public class SmsLog
    {
        [Key]
        public long Id { get; set; }

        // ----- fields required by Task 8 -----
        [Required, StringLength(30)]
        public string RecipientNumber { get; set; } = "";

        [Required]
        public string MessageContent { get; set; } = "";

        [Required, StringLength(20)]
        public string Status { get; set; } = SmsLogStatus.Pending;

        /// <summary>UTC. Null until the message is actually sent.</summary>
        public DateTime? SentDate { get; set; }

        [StringLength(1000)]
        public string? FailureReason { get; set; }

        /// <summary>Related appointment (for customer reminders: the first job of that customer tomorrow).</summary>
        public int? JobId { get; set; }

        // ----- extra fields that make the log useful -----
        [Required, StringLength(20)]
        public string Source { get; set; } = SmsLogSources.Automated;

        [StringLength(150)]
        public string? SettingName { get; set; }          // snapshot, survives deleting the setting

        [StringLength(50)]
        public string? NotificationType { get; set; }

        [StringLength(20)]
        public string? RecipientType { get; set; }        // Technician / Customer

        public int? RecipientId { get; set; }             // TechnicianId or ContactDetailId

        [StringLength(150)]
        public string? RecipientName { get; set; }

        /// <summary>True when SmsMode = Test: nothing really left the server.</summary>
        public bool IsSimulated { get; set; }

        public int AttemptCount { get; set; }
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;   // UTC, reset when someone clicks Retry
        public DateTime? LastAttemptAt { get; set; }               // UTC
        public DateTime? NextAttemptAt { get; set; }               // UTC, null = try as soon as possible

        /// <summary>Row written to SmsDetails once the message was sent (this is what the conversation shows).</summary>
        public int? SmsDetailId { get; set; }

        public long? AutomatedSmsLogId { get; set; }

        [ForeignKey(nameof(AutomatedSmsLogId))]
        public virtual AutomatedSmsLog? AutomatedLog { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }
    }
}