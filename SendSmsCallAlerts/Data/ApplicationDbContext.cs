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
        
        // NEW: Technician DbSet
        public DbSet<Technician> Technicians { get; set; }
        public DbSet<TechnicianAvailability> TechnicianAvailabilities { get; set; }
        public DbSet<AutomatedSmsSetting> AutomatedSmsSettings { get; set; }
        public DbSet<AutomatedSmsLog> AutomatedSmsLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TechnicianAvailability>(e =>
            {
                e.HasIndex(a => new { a.TechnicianId, a.DayOfWeek }).IsUnique()
                 .HasDatabaseName("UQ_TechnicianAvailability_Tech_Day");
                e.HasOne(a => a.Technician).WithMany(t => t.Availabilities)
                 .HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AutomatedSmsSetting>(e =>
            {
                e.HasIndex(s => s.Name).IsUnique().HasDatabaseName("UQ_AutomatedSmsSettings_Name");

                e.HasData(
                    new AutomatedSmsSetting
                    {
                        Id = 1,
                        Name = "Technician Weekly Availability Request",
                        NotificationType = NotificationTypes.TechnicianAvailabilityRequest,
                        RecipientType = RecipientTypes.Technician,
                        Frequency = SmsFrequencies.Weekly,
                        DayOfWeek = 0, // Sunday
                        SendTime = new TimeSpan(17, 0, 0),
                        TemplateBody = "Hi {technicianFirstName}, please reply with your availability for the week of {weekStart} to {weekEnd}.\n\nExample: MON 9AM-5PM, TUE OFF\n\nThank you, {companyName}",
                        IsActive = true,
                        CreatedOn = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new AutomatedSmsSetting
                    {
                        Id = 2,
                        Name = "Technician Daily Job Summary",
                        NotificationType = NotificationTypes.TechnicianDailyJobSummary,
                        RecipientType = RecipientTypes.Technician,
                        Frequency = SmsFrequencies.Daily,
                        SendTime = new TimeSpan(17, 0, 0),
                        TemplateBody = "Hi {technicianFirstName}, you have {jobCount} job(s) tomorrow ({jobDate}):\n\n{jobsList}\n\n- {companyName}",
                        IsActive = true,
                        CreatedOn = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new AutomatedSmsSetting
                    {
                        Id = 3,
                        Name = "Customer Next-Day Job Reminder",
                        NotificationType = NotificationTypes.CustomerJobReminder,
                        RecipientType = RecipientTypes.Customer,
                        Frequency = SmsFrequencies.Daily,
                        SendTime = new TimeSpan(17, 0, 0),
                        TemplateBody = "Hi {customerName}, this is a reminder from {companyName}: your {category} appointment is scheduled for tomorrow, {jobDate}, at {jobTime}. Your technician will be {technicianName}. Reply STOP to opt out.",
                        IsActive = true,
                        CreatedOn = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
                    });
            });

            modelBuilder.Entity<AutomatedSmsLog>(e =>
            {
                e.HasIndex(l => new { l.SettingId, l.OccurrenceKey, l.RecipientType, l.RecipientId })
                 .IsUnique().HasDatabaseName("UQ_AutomatedSmsLogs_Occurrence");

                e.HasOne(l => l.Setting).WithMany(s => s.Logs)
                 .HasForeignKey(l => l.SettingId).OnDelete(DeleteBehavior.Cascade);
            });
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

            // NEW: Seed IVR Category Options
            modelBuilder.Entity<IvrOptions>().HasData(
                new IvrOptions { Id = 1, Num = 1, keyword = "HVAC Services", UserId = 0, TwilioNum = "" },
                new IvrOptions { Id = 2, Num = 2, keyword = "Plumbing Repair", UserId = 0, TwilioNum = "" },
                new IvrOptions { Id = 3, Num = 3, keyword = "Electrical Work", UserId = 0, TwilioNum = "" },
                new IvrOptions { Id = 4, Num = 4, keyword = "Appliance Repair", UserId = 0, TwilioNum = "" },
                new IvrOptions { Id = 5, Num = 5, keyword = "General Maintenance", UserId = 0, TwilioNum = "" }
            );
        }


    }
}