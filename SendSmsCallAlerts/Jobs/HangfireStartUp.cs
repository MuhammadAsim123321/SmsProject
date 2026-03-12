using Hangfire;
using SendSmsCallAlerts.Controllers;
using SendSmsCallAlerts.Data;

namespace SendSmsCallAlerts.Jobs
{
    public class HangfireStartUp: IHostedService
    {
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IServiceProvider serviceProvider;

        public HangfireStartUp(
            IBackgroundJobClient backgroundJobClient,
            IServiceProvider serviceProvider)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {

            JobManager jm = new JobManager();
            jm.DeleteAllJobs();

            backgroundJobClient.Enqueue(() => InvokeControllerAction());

            //InitializeAllJobs();

            return Task.CompletedTask;
        }

        public void InvokeControllerAction()
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var controller = new HangFireJobsController(dbContext); // Pass the DbContext to the controller
                controller.EnqueueJob();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

    }
}
