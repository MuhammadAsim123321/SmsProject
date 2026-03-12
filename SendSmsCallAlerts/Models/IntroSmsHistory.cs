using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class IntroSmsHistory
    {
        [Key]
        public int Id { get; set; }
        public string FromNum { get; set; }
        public string ToNum { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
