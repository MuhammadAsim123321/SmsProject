namespace SendSmsCallAlerts.Models
{
    public class SchedulerViewModel
    {
        public int Id { get; set; }
        public string name { get; set; }
        public string schedulerFor { get; set; }
        public int templateOrAudioId { get; set; }
        public string onceOrRepeat { get; set; }
        public DateTime executionDateAndTime { get; set; }
        public DateTime executionTime { get; set; }
        public string executionTimeSt { get; set; }
        public DateTime createdOn { get; set; }

        public string templateOrAudioName { get; set; }
        public string executionDateAndTimeSt { get; set; }
        public string status { get; set; }
        public string fromSms { get; set; }
        public string toSms { get; set; }
        public string smsBody { get; set; }

        public DateTime? JobBookDate { get; set; }
        public bool? IsPaused { get; set; }

    }
}
