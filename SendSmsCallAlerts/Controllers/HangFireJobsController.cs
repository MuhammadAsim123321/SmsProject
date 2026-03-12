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
        private readonly string TmZn = "Eastern Standard Time";
        //private readonly string TmZn = "Pakistan Standard Time";

        public HangFireJobsController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public void EnqueueJob()
        {
            //            RegisterAllJobs();
            RecurringJob.AddOrUpdate(() => new SmsJob(dbContext).RegisterAllJobs(), Cron.Minutely, TimeZoneInfo.FindSystemTimeZoneById(TmZn));

        }

        public IActionResult Index()
        {
            JobManager jm = new JobManager();
            jm.DeleteAllJobs();

            return View();
        }

        //private string GetHoursAndMinutes(Scheduler sch)
        //{
        //    return "" + sch.executionDateAndTime.Minute + " " + sch.executionDateAndTime.Hour + " * * *";

        //}
    }
}
