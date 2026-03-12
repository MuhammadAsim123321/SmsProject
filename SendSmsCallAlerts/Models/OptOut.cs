using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class OptOut
    {
        [Key]
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime ReceivedAt { get; set; }

    }
}