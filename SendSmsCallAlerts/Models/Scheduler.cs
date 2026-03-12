using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class Scheduler
    {
        [Key]
        public int Id { get; set; }
        public string name { get; set; }
        public string schedulerFor { get; set; }
        public int templateOrAudioId { get; set; }
        public string onceOrRepeat { get; set; }
        public DateTime executionDateAndTime { get; set; }
        public DateTime createdOn { get; set; }
        public DateTime executionTime { get; set; }
        public string status { get; set; }
        public string fromNum { get; set; }
        public string toNum { get; set; }
        public string smsBody { get; set; }
        public DateTime? JobBookDate { get; set; }

        public bool? IsPaused { get; set; } = false;

        public int? TimeToRunId { get; set; }

        [ForeignKey("TimeToRunId")]
        public virtual TimeToRun TimeToRun { get; set; }

        public int? RunFromId { get; set; }

        [ForeignKey("RunFromId")]
        public virtual RunFrom RunFrom { get; set; }



    }
}