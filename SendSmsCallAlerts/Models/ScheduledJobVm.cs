namespace SendSmsCallAlerts.Models
{
    public class ScheduledJobVm
    {
        public int JobId { get; set; }
        public int ContactId { get; set; }
        public string PhoneNumber { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string Category { get; set; }
        public string? Scheduler { get; set; }
        public DateTime? JobBookDate { get; set; }
        public bool IsPaused { get; set; }
    }
}