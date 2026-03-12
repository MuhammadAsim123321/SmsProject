namespace SendSmsCallAlerts.Models
{
    public class Msgs
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Body { get; set; }
    }

    public class ApiResponse
    {
        public List<Msgs> Messages { get; set; }
    }
}
