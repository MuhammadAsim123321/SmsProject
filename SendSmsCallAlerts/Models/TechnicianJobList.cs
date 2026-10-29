using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    /// <summary>
    /// One row per numbered line in the technician's daily SMS: "(2) 10:00 AM - ..." = Position 2 -> JobId.
    /// Only the LATEST list of each technician is kept. It is replaced every time the daily summary is queued,
    /// and lists of past days are deleted at the same time, so the table never grows.
    /// Used by TechnicianReplyService to understand replies such as "C 123".
    /// </summary>
    [Table("TechnicianJobLists")]
    public class TechnicianJobList
    {
        [Key]
        public long Id { get; set; }

        public int TechnicianId { get; set; }
        [ForeignKey(nameof(TechnicianId))]
        public virtual Technician? Technician { get; set; }

        /// <summary>Business date of the jobs in the list (the "tomorrow" of the day the SMS was built).</summary>
        public DateTime JobDate { get; set; }

        /// <summary>The number shown in the SMS, starting at 1.</summary>
        public int Position { get; set; }

        public int JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public virtual Job? Job { get; set; }

        /// <summary>Job.StartAt when the SMS was built. If the job was moved afterwards, a reply does not confirm it.</summary>
        public DateTime JobStartAt { get; set; }

        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    }
}