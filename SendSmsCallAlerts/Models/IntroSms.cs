using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class IntroSms
    {
        [Key]
        public int Id { get; set; }
        public string Message { get; set; }
        public string TwNum { get; set; }
    }
}
