using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;

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
        // ✅ NEW: Link to ContactDetail for unified data
        public int? ContactDetailId { get; set; }

        [ForeignKey("ContactDetailId")]
        public virtual ContactDetail? ContactDetail { get; set; }
        public int? ScheduledJobId { get; set; }

    }
}