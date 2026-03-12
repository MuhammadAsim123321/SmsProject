namespace SendSmsCallAlerts.Models
{
    public class OptInstVM
    {
        public int Id { get; set; }
        public string OptionNumber { get; set; }
        public int InstructionOrder { get; set; }
        public string Instruction { get; set; }
        public string TwilioNumber { get; set; }
    }
}
