using Hangfire;
using Microsoft.AspNetCore.Mvc;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Jobs;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Controllers
{
    public class HangFireJobsController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        // CHANGED: the time zone now comes from AppSettings:TmZn through IAppClock (used inside SmsJob).
        // These two lines are kept (commented-out use) for the old scenario below.
        private readonly string TmZn = "Eastern Standard Time";
        //private readonly string TmZn = "Pakistan Standard Time";

        public HangFireJobsController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public void EnqueueJob()
        {
            //            RegisterAllJobs();

            // OLD (kept for the old scenario): SmsJob used to be created by hand with the DbContext.
            // It now gets DbContext, IAppClock and ISmsGateway from dependency injection, so this line no longer compiles.
            //RecurringJob.AddOrUpdate(() => new SmsJob(dbContext).RegisterAllJobs(), Cron.Minutely, TimeZoneInfo.FindSystemTimeZoneById(TmZn));

            // NEW: same recurring job id as before ("SmsJob.RegisterAllJobs"), resolved through DI.
            RecurringJob.AddOrUpdate<SmsJob>("SmsJob.RegisterAllJobs", j => j.RegisterAllJobs(), Cron.Minutely);
            RecurringJob.AddOrUpdate<AutomatedSmsJob>("automated-sms-minutely", j => j.RunAsync(), Cron.Minutely);
            RecurringJob.AddOrUpdate<SmsDispatchJob>("sms-dispatcher", j => j.RunAsync(), Cron.Minutely);

        }

        public IActionResult Index()
        {
            return View();
        }

        //private string GetHoursAndMinutes(Scheduler sch)
        //{
        //    return "" + sch.executionDateAndTime.Minute + " " + sch.executionDateAndTime.Hour + " * * *";

        //}
    }
}