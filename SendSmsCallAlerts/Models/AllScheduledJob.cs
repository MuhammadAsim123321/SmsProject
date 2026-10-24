using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    /// <summary>The message plan of a job: which scheduler template runs for which appointment.</summary>
    public class AllScheduledJob
    {
        [Key]
        public int Id { get; set; }

        public int JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public virtual Job Job { get; set; } = null!;

        public int SchedulerId { get; set; }
        [ForeignKey(nameof(SchedulerId))]
        public virtual Scheduler Scheduler { get; set; } = null!;

        public DateTime AssignedDate { get; set; } = DateTime.Now;   // overwritten with business time by JobService
        public bool IsPaused { get; set; } = false;
        public DateTime? LastExecutedAt { get; set; }
        public DateTime? CustomDate { get; set; }
        public int? CustomHours { get; set; }
    }
}