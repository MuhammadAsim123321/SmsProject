namespace SendSmsCallAlerts.Models
{
    public class SmsDetailsVm
    {
        public int Id { get; set; }
        public string fromNum { get; set; }
        public string toNum { get; set; }
        public string smsBody { get; set; }
        public string smsDirection { get; set; }
        public string? IsReadYesOrNo { get; set; }
        public string? dt { get; set; }
        public DateTime createdOn { get; set; }
        public string? imgPath { get; set; }

        public List<string> links { get; set; }
        public int attchmentsCount { get; set; }

        public string name { get; set; }
        public string address { get; set; }
        public string email { get; set; }
        public string category { get; set; }
        public string scheduler { get; set; }
        public DateTime? JobBookDate { get; set; }
        public int SchedulerId { get; set; }
        public string? jobBookDatesList { get; set; }
        public string jobBookTimesList { get; set; }
    }
}