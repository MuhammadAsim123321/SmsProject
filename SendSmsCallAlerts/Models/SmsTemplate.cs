using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class SmsTemplate
    {
        [Key]
        public int Id { get; set; }
        public string name { get; set; }
        public string templateBody { get; set; }
        public DateTime createdOn { get; set; }
        public string imgPath { get; set; }
    }
}
