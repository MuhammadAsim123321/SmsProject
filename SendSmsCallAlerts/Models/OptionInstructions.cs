using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    public class OptionInstructions
    {
        [Key]
        public int Id { get; set; }
        public int IvrId { get; set; }
        public int InstructionOrder { get; set; }
        public string Instruction { get; set; }
        public int UserId { get; set; }
        public string TwilioNum { get; set; }
    }
}
