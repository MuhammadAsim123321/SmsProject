using Microsoft.AspNetCore.Mvc;

namespace SendSmsCallAlerts.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        public string fromNum { get; set; }
        public string toNum { get; set; }
        public string smsBody { get; set; }
        public string smsDirection { get; set; }
        public string IsReadYesOrNo { get; set; }
        public string dt { get; set; }
        public DateTime createdOn { get; set; }
        public FileStreamResult fl { get; set; }

    }
}
