using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Jobs
{
    public class HangfireStartUp: IHostedService
    {
        private readonly IServiceProvider serviceProvider;

        public HangfireStartUp(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            RecurringJob.AddOrUpdate<SmsJob>("SmsJob.RegisterAllJobs", j => j.RegisterAllJobs(), Cron.Minutely);
            RecurringJob.AddOrUpdate<AutomatedSmsJob>("automated-sms-minutely", j => j.RunAsync(), Cron.Minutely);
            RecurringJob.AddOrUpdate<SmsDispatchJob>("sms-dispatcher", j => j.RunAsync(), Cron.Minutely);
            using (var scope = serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<AutomatedSmsService>();
                await service.EnsureNextOccurrencesAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

    }
}
