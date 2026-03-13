using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SendSmsCallAlerts.Models
{
    public class AllScheduledJob
    {
        [Key]
        public int Id { get; set; }
        public int? ContactDetailId { get; set; }
        [ForeignKey("ContactDetailId")]
        public virtual ContactDetail ContactDetail { get; set; }

        public int? SchedulerId { get; set; }
        [ForeignKey("SchedulerId")]
        public virtual Scheduler Scheduler { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.Now;
        public DateTime JobBookDate { get; set; } = DateTime.Now;
        public DateTime JobCompletedDate { get; set; } = DateTime.Now;
        public bool IsPaused { get; set; } = false;
        public DateTime? LastExecutedAt { get; set; }
        public DateTime? CustomDate { get; set; }
        public int? CustomHours { get; set; }  // Days+Hours calculate


    }
}