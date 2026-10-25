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
        // NEW: Split address fields
        public string? streetAddress { get; set; }
        public string? cityState { get; set; }
        public string? zipCode { get; set; }
        public string email { get; set; }
        public string category { get; set; }
        public string scheduler { get; set; }
        public DateTime? JobBookDate { get; set; }
        public int SchedulerId { get; set; }
        public string? jobBookDatesList { get; set; }
        public string jobBookTimesList { get; set; }

        // Task 4: Response time tracking
        public DateTime? RespondedOn { get; set; }
        public double? WaitingMinutes { get; set; }

        /// <summary>
        /// "gold" (10–14m59s) | "orange" (15–19m59s) | "red" (20m+) | "" based on WaitingMinutes for incoming messages
        /// </summary>
        public string WaitingColor
        {
            get
            {
                if (smsDirection != "Incoming" || WaitingMinutes == null) return "";
                if (WaitingMinutes >= 20) return "red";
                if (WaitingMinutes >= 15) return "orange";
                if (WaitingMinutes >= 10) return "gold"; // 10–14 min 59 sec
                return "";
            }
        }
    }
}
