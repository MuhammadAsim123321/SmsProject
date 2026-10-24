using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class SmsDetail
    {
        [Key]
        public int Id { get; set; }
        public string fromNum { get; set; }
        public string toNum { get; set; }
        public string smsBody { get; set; }
        public string smsDirection { get; set; }
        public string? IsReadYesOrNo { get; set; }
        public string? dt { get; set; }
        public DateTime createdOn { get; set; }
        public string? imgPath { get; set; }

        // link to the customer (set by the webhook and by JobService)
        public int? ContactDetailId { get; set; }

        [ForeignKey("ContactDetailId")]
        public virtual ContactDetail? ContactDetail { get; set; }

        // the message plan (AllScheduledJob) that sent this outgoing message; FK is set up in ApplicationDbContext
        public int? ScheduledJobId { get; set; }
    }
}