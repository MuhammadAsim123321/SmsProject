using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public static class NotificationTypes
    {
        public const string TechnicianAvailabilityRequest = "TechnicianAvailabilityRequest";
        public const string TechnicianDailyJobSummary = "TechnicianDailyJobSummary";
        public const string CustomerJobReminder = "CustomerJobReminder";
    }

    public static class RecipientTypes
    {
        public const string Technician = "Technician";
        public const string Customer = "Customer";
    }

    public static class SmsFrequencies
    {
        public const string Daily = "Daily";
        public const string Weekly = "Weekly";
    }

    public static class AutomatedSmsStatus
    {
        public const string Sending = "Sending";   // claimed, not finished yet
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";   // e.g. opted out / no phone
        public const string Test = "Test";         // "Run Now" test send
        public const string NoTwilio = "NoTwilio"; // Twilio not configured, logged only
    }

    /// <summary>
    /// One notification rule stored in the database: what to send, to whom, and when.
    /// Time/text are editable from the admin page without code changes.
    /// </summary>
    public class AutomatedSmsSetting
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [Required, StringLength(50)]
        public string NotificationType { get; set; }   // see NotificationTypes

        [Required, StringLength(20)]
        public string RecipientType { get; set; }      // Technician / Customer

        [Required, StringLength(20)]
        public string Frequency { get; set; }          // Daily / Weekly

        /// <summary>0 = Sunday ... 6 = Saturday. Required when Frequency = Weekly.</summary>
        [Range(0, 6)]
        public int? DayOfWeek { get; set; }

        /// <summary>Local business time (AppSettings:TmZn), e.g. 17:00.</summary>
        public TimeSpan SendTime { get; set; }

        [Required]
        public string TemplateBody { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastRunAt { get; set; }       // UTC
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }

        public virtual ICollection<AutomatedSmsLog> Logs { get; set; } = new List<AutomatedSmsLog>();
    }
}