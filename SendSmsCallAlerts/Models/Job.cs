using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public static class JobStatus
    {
        public const string Scheduled = "Scheduled";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        /// <summary>Never stored. Calculated: not cancelled and the end time has passed.</summary>
        public const string Completed = "Completed";

        public static readonly string[] Editable = { Scheduled, Confirmed, Cancelled };

        public static bool IsEditable(string? status) => status != null && Array.IndexOf(Editable, status) >= 0;
    }

    /// <summary>
    /// One appointment for one customer: category, technician, start and end (same day).
    /// StartAt / EndAt are business wall-clock time (AppSettings:TmZn).
    /// The messages that run for this appointment are its AllScheduledJob rows.
    /// </summary>
    public class Job
    {
        [Key]
        public int Id { get; set; }

        public int ContactDetailId { get; set; }
        [ForeignKey(nameof(ContactDetailId))]
        public virtual ContactDetail? ContactDetail { get; set; }

        /// <summary>IvrOptions.Id (IvrOptions is also the service category list).</summary>
        public int? CategoryId { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public virtual IvrOptions? Category { get; set; }

        public int? TechnicianId { get; set; }
        [ForeignKey(nameof(TechnicianId))]
        public virtual Technician? Technician { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        /// <summary>Driving miles customer ZIP to technician ZIP, calculated on the server.</summary>
        public double? DistanceMiles { get; set; }
        /// <summary>Amount quoted to the customer (USD). Optional.</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal? QuotedAmount { get; set; }
        /// <summary>Only asked for the "Carpet Repair" category. true = Yes, false = No, null = not applicable.</summary>
        public bool? HasDonorCarpet { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = JobStatus.Scheduled;

        /// <summary>The incoming message the job was created from (optional; survives message deletion).</summary>
        public int? SourceSmsId { get; set; }
        [ForeignKey(nameof(SourceSmsId))]
        public virtual SmsDetail? SourceSms { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;   // UTC
        public DateTime? UpdatedOn { get; set; }                      // UTC

        /// <summary>Message plans (scheduler + pause + last sent). One today, more later.</summary>
        public virtual ICollection<AllScheduledJob> ScheduledJobs { get; set; } = new List<AllScheduledJob>();

        public string GetEffectiveStatus(DateTime businessNow)
        {
            if (Status == JobStatus.Cancelled) return JobStatus.Cancelled;
            return EndAt < businessNow ? JobStatus.Completed : Status;
        }
    }
}