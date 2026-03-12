namespace SendSmsCallAlerts.Models
{
    public class SmsUpdateDto
    {
        public string fromNum { get; set; }
        public string? toNum { get; set; }
        public string? smsDirection { get; set; }
        public string name { get; set; }
        public string? email { get; set; }
        public string? address { get; set; }
        public string category { get; set; }
        public string? scheduler { get; set; }
        public DateTime? JobBookDate { get; set; }
        public DateTime? JobCompletedDate { get; set; }
        public string? schedName { get; set; }
        public string? schedFrom { get; set; }
        public string? schedTo { get; set; }
        public string? schedBody { get; set; }
        public string? onceOrRepeat { get; set; }
        public DateTime? executionDateAndTime { get; set; }
        public string? executionTime { get; set; }
        public int? schedulerId { get; set; }
        public DateTime? CustomDate { get; set; }
    }
}