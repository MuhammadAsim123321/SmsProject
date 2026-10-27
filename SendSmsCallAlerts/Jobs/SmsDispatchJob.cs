using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Jobs
{
    public class SmsDispatchJob
    {
        private readonly SmsDispatchService _svc;
        public SmsDispatchJob(SmsDispatchService svc) { _svc = svc; }
        public Task RunAsync() => _svc.ProcessPendingAsync();
    }
}