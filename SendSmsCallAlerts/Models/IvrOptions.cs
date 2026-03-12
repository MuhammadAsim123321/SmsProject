using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class IvrOptions
    {
        [Key]
        public int Id { get; set; }
        public int Num { get; set; }
        public string keyword { get; set; }
        public int UserId { get; set; }
        public string TwilioNum { get; set; }

    }
}
