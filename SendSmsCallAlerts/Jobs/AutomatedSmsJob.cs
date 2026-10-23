using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Jobs
{
    public class AutomatedSmsJob
    {
        private readonly AutomatedSmsService service;
        public AutomatedSmsJob(AutomatedSmsService service) { this.service = service; }
        public Task RunAsync() => service.ProcessDueAsync();
    }
}