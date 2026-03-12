using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class MmsLinks
    {
        [Key]
        public int Id { get; set; }
        public int SmsId { get; set; }
        public string? FileName { get; set; }
        public string Location { get; set; }
        public int? SchedulerId { get; set; }
    }
}
