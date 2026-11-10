namespace SendSmsCallAlerts.Models
{
    public class TechnicianMessageVm
    {
        public int SmsId { get; set; }
        public int? TechnicianId { get; set; }
        public string TechnicianName { get; set; } = "";
        public string TechnicianPhone { get; set; } = "";
        public string MappingStatus { get; set; } = "";
        public string FromNumber { get; set; } = "";
        public string ToNumber { get; set; } = "";
        public string Direction { get; set; } = "";
        public string MessageBody { get; set; } = "";
        public DateTime CreatedOnBusiness { get; set; }
        public int AttachmentCount { get; set; }
    }
}
