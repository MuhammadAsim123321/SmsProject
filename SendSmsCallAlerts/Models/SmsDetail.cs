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

        public int? TechnicianId { get; set; }
        [ForeignKey(nameof(TechnicianId))]
        public virtual Technician? Technician { get; set; }
        // the message plan (AllScheduledJob) that sent this outgoing message; FK is set up in ApplicationDbContext
        public int? ScheduledJobId { get; set; }

        /// <summary>User (staff or admin) who sent this reply from Conversations. Null = old row / automated.</summary>
        public int? SentByUserId { get; set; }

        /// <summary>
        /// For outgoing (reply) messages only: the Id of the incoming SmsDetail this message
        /// was sent in response to. Null for automated messages and old rows without the link.
        /// Self-referential FK within the same table.
        /// </summary>
        public int? ReplyToSmsId { get; set; }
    }
}