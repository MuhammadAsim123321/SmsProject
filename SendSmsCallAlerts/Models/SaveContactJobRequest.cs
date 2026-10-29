namespace SendSmsCallAlerts.Models
{
    // ---------------- what the modal sends (JSON) ----------------

    public class SaveContactJobRequest
    {
        /// <summary>The CUSTOMER's phone number (incoming row: fromNum, outgoing row: toNum).</summary>
        public string PhoneNumber { get; set; } = "";
        public string? SmsDirection { get; set; }
        /// <summary>The message row the user clicked (stored as the job's source for new jobs).</summary>
        public int? SourceSmsId { get; set; }

        public ContactSection Contact { get; set; } = new();
        public JobSection? Job { get; set; }
        public SchedulerSection? Scheduler { get; set; }
    }

    public class ContactSection
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? StreetAddress { get; set; }
        public string? CityState { get; set; }
        public string? ZipCode { get; set; }
    }

    public class JobSection
    {
        /// <summary>Null = create a new job. Set = edit this job.</summary>
        public int? JobId { get; set; }
        public int? CategoryId { get; set; }
        public int? TechnicianId { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        /// <summary>Scheduled / Confirmed / Cancelled</summary>
        public string? Status { get; set; }
        /// <summary>Quoted amount in USD (max 2 decimals). Null = not quoted.</summary>
        public decimal? QuotedAmount { get; set; }
        /// <summary>Donor-carpet answer. Only used when the category asks the question.</summary>
        public bool? HasDonorCarpet { get; set; }
    }

    public class SchedulerSection
    {
        /// <summary>0/null = no message plan for this job.</summary>
        public int? SchedulerId { get; set; }
        public DateTime? CustomDate { get; set; }
        /// <summary>Total hours (days x 24 + hours) when the template uses "custom days and hours".</summary>
        public int? CustomHours { get; set; }
    }

    // ---------------- what the server returns ----------------

    public class SaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? JobId { get; set; }
        /// <summary>fieldKey -> message. Keys: contactName, contactEmail, contactZip, jobCategory,
        /// jobTechnician, jobStartAt, jobEndAt, jobStatus, schedulerTemplate, schedulerCustomDate, schedulerCustomHours</summary>
        public Dictionary<string, string> Errors { get; set; } = new();
    }

    public class ContactView
    {
        public bool Found { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string StreetAddress { get; set; } = "";
        public string CityState { get; set; } = "";
        public string ZipCode { get; set; } = "";
        /// <summary>The job whose SourceSmsId matches the message that was clicked. Null when no job is linked yet.</summary>
        public JobView? LinkedJob { get; set; }
        /// <summary>All jobs for this customer (newest first). Used by the history popup.</summary>
        public List<JobView> Jobs { get; set; } = new();
    }

    public class JobView
    {
        public int Id { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int? TechnicianId { get; set; }
        public string TechnicianName { get; set; } = "";
        /// <summary>yyyy-MM-ddTHH:mm (fits datetime-local inputs)</summary>
        public string StartAt { get; set; } = "";
        public string EndAt { get; set; } = "";
        public double? DistanceMiles { get; set; }
        public decimal? QuotedAmount { get; set; }
        public bool? HasDonorCarpet { get; set; }
        /// <summary>Stored value: Scheduled / Confirmed / Cancelled</summary>
        public string Status { get; set; } = "";
        /// <summary>Includes the calculated "Completed"</summary>
        public string EffectiveStatus { get; set; } = "";
        public List<PlanView> Plans { get; set; } = new();
    }

    public class PlanView
    {
        public int Id { get; set; }
        public int? SchedulerId { get; set; }
        public string SchedulerName { get; set; } = "";
        public string? CustomDate { get; set; }   // yyyy-MM-dd
        public int? CustomHours { get; set; }
        public bool IsPaused { get; set; }
        public DateTime? LastExecutedAt { get; set; }
    }
}