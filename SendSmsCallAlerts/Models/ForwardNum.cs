using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class ForwardNum
    {
        [Key]
        public int Id { get; set; }
        public string Number { get; set; }
        public string? Name { get; set; }
        public DateTime createdOn { get; set; }
    }
}
