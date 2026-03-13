using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Models;
using System.Collections.Generic;

namespace SendSmsCallAlerts.Data
{
    public class ApplicationDbContext : DbContext
    {
        //////////////////// Packages
        ///
        //Microsoft.EntityFrameworkCore
        //Microsoft.EntityFrameworkCore.SqlServer
        //Microsoft.EntityFrameworkCore.Tools
        //Microsoft.VisualStudio.Web.CodeGeneration.Design

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<SmsDetail> SmsDetails { get; set; }
        public DbSet<MmsLinks> MmsLinkss { get; set; }

        public DbSet<SendSmsCallAlerts.Models.ForwardNum> ForwardNum { get; set; } = default!;

        public DbSet<IvrOptions> IvrOption { get; set; }

        public DbSet<ContactDetail> ContactDetail { get; set; }
        public DbSet<JobSchedule> JobSchedule { get; set; }
        public DbSet<OptionInstructions> OptionInstruction { get; set; }
        public DbSet<SendSmsCallAlerts.Models.IntroSms> IntroSms { get; set; } = default!;
        public DbSet<SendSmsCallAlerts.Models.IntroSmsHistory> IntroSmsHistory { get; set; } = default!;
        public DbSet<SendSmsCallAlerts.Models.User> User { get; set; } = default!;
        public DbSet<SendSmsCallAlerts.Models.SmsTemplate> SmsTemplates { get; set; } = default!;
        public DbSet<SendSmsCallAlerts.Models.Scheduler> Schedulers { get; set; } = default!;
        public DbSet<AllScheduledJob> AllScheduledJobs { get; set; }
        public DbSet<HangfireJobLog> HangfireJobLogs { get; set; }
        public DbSet<OptOut> OptOuts { get; set; }
        public DbSet<TimeToRun> TimeToRun { get; set; }
        public DbSet<RunFrom> RunFrom { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TimeToRun>().HasData(
                new TimeToRun { Id = 1, Name = "Now", HourCount = 0 },
                new TimeToRun { Id = 2, Name = "24 Hour", HourCount = 24 },
                new TimeToRun { Id = 3, Name = "48 Hour", HourCount = 48 },
                new TimeToRun { Id = 4, Name = "3 days", HourCount = 72 },
                new TimeToRun { Id = 5, Name = "4 days", HourCount = 96 },
                new TimeToRun { Id = 6, Name = "5 days", HourCount = 120 },
                new TimeToRun { Id = 7, Name = "6 days", HourCount = 144 },
                new TimeToRun { Id = 8, Name = "7 days", HourCount = 168 },
                new TimeToRun { Id = 9, Name = "1 Month", HourCount = 720 },
                new TimeToRun { Id = 10, Name = "6 Months", HourCount = 4320 },
                new TimeToRun { Id = 11, Name = "1 Year", HourCount = 8760 },
                new TimeToRun { Id = 12, Name = "Enter Custom Days and Hours", HourCount = -1 }
                 
            );

            modelBuilder.Entity<RunFrom>().HasData(
                new RunFrom { Id = 1, RunFromName = "Today Date" },
                new RunFrom { Id = 2, RunFromName = "Custom Date" },
                new RunFrom { Id = 3, RunFromName = "Job Book Date" },
                new RunFrom { Id = 4, RunFromName = "Job Created Date" }
            );
        }


    }
}