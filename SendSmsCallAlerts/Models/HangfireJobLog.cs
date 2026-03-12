using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class HangfireJobLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}