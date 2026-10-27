//using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Models;
using Job = SendSmsCallAlerts.Models.Job;

namespace SendSmsCallAlerts.Data
{
    public class ApplicationDbContext : DbContext
    {
        //////////////////// Packages
        //Microsoft.EntityFrameworkCore
        //Microsoft.EntityFrameworkCore.SqlServer
        //Microsoft.EntityFrameworkCore.Tools
        //Microsoft.VisualStudio.Web.CodeGeneration.Design

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<SmsDetail> SmsDetails { get; set; }
        public DbSet<MmsLinks> MmsLinkss { get; set; }
        public DbSet<ForwardNum> ForwardNum { get; set; } = default!;
        public DbSet<IvrOptions> IvrOption { get; set; }
        public DbSet<ContactDetail> ContactDetail { get; set; }
        public DbSet<OptionInstructions> OptionInstruction { get; set; }
        public DbSet<IntroSms> IntroSms { get; set; } = default!;
        public DbSet<IntroSmsHistory> IntroSmsHistory { get; set; } = default!;
        public DbSet<User> User { get; set; } = default!;
        public DbSet<SmsTemplate> SmsTemplates { get; set; } = default!;
        public DbSet<Scheduler> Schedulers { get; set; } = default!;
        public DbSet<HangfireJobLog> HangfireJobLogs { get; set; }
        public DbSet<OptOut> OptOuts { get; set; }
        public DbSet<TimeToRun> TimeToRun { get; set; }
        public DbSet<RunFrom> RunFrom { get; set; }

        // NEW: the appointment, and the message plan that runs for it
        public DbSet<Job> Jobs { get; set; }
        public DbSet<AllScheduledJob> AllScheduledJobs { get; set; }

        public DbSet<Technician> Technicians { get; set; }
        public DbSet<TechnicianAvailability> TechnicianAvailabilities { get; set; }
        public DbSet<AutomatedSmsSetting> AutomatedSmsSettings { get; set; }
        public DbSet<AutomatedSmsLog> AutomatedSmsLogs { get; set; }
        public DbSet<AutomatedSmsOccurrence> AutomatedSmsOccurrences { get; set; }

        // Tasks 7-9: delivery record of every SMS (sent / pending / failed)
        public DbSet<SmsLog> SmsLogs { get; set; }

        // REMOVED: DbSet<JobSchedule> JobSchedule (legacy table, 0 rows)

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------------- customer ----------------
            modelBuilder.Entity<ContactDetail>(e =>
            {
                e.Property(c => c.PhoneNumber).HasMaxLength(20);
                e.HasIndex(c => c.PhoneNumber).IsUnique()
                 .HasFilter("[PhoneNumber] IS NOT NULL")
                 .HasDatabaseName("UQ_ContactDetail_Phone");
            });

            // ---------------- job (appointment) ----------------
            modelBuilder.Entity<Job>(e =>
            {
                e.HasOne(j => j.ContactDetail).WithMany(c => c.Jobs)
                 .HasForeignKey(j => j.ContactDetailId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(j => j.Category).WithMany()
                 .HasForeignKey(j => j.CategoryId).OnDelete(DeleteBehavior.SetNull);

                e.HasOne(j => j.Technician).WithMany()
                 .HasForeignKey(j => j.TechnicianId).OnDelete(DeleteBehavior.Restrict);

                e.HasOne(j => j.SourceSms).WithMany()
                 .HasForeignKey(j => j.SourceSmsId).OnDelete(DeleteBehavior.SetNull);

                e.Property(j => j.Status).HasMaxLength(20).IsRequired();
                e.HasIndex(j => new { j.TechnicianId, j.StartAt, j.EndAt }).HasDatabaseName("IX_Jobs_Technician_Time");
                e.HasIndex(j => j.ContactDetailId);
            });

            // ---------------- message plan of a job ----------------
            modelBuilder.Entity<AllScheduledJob>(e =>
            {
                e.HasOne(a => a.Job).WithMany(j => j.ScheduledJobs)
                 .HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(a => a.Scheduler).WithMany()
                 .HasForeignKey(a => a.SchedulerId).OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(a => new { a.JobId, a.SchedulerId }).IsUnique()
                 .HasDatabaseName("UQ_AllScheduledJobs_Job_Scheduler");
            });

            // outgoing messages -> the plan that sent them (previously a column without a foreign key)
            modelBuilder.Entity<SmsDetail>(e =>
            {
                e.HasOne<AllScheduledJob>().WithMany()
                 .HasForeignKey(s => s.ScheduledJobId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(s => s.ScheduledJobId);
            });

            // ---------------- technician ----------------
            modelBuilder.Entity<TechnicianAvailability>(e =>
            {
                e.HasIndex(a => new { a.TechnicianId, a.DayOfWeek }).IsUnique()
                 .HasDatabaseName("UQ_TechnicianAvailability_Tech_Day");
                e.HasOne(a => a.Technician).WithMany(t => t.Availabilities)
                 .HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------- automated SMS ----------------
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

            modelBuilder.Entity<AutomatedSmsOccurrence>(e =>
            {
                e.HasIndex(o => new { o.SettingId, o.ScheduledForLocal })
                    .IsUnique().HasDatabaseName("UQ_AutomatedSmsOccurrences_Setting_Local");
                e.HasIndex(o => new { o.Status, o.ScheduledForUtc })
                    .HasDatabaseName("IX_AutomatedSmsOccurrences_Status_Utc");
                e.HasOne(o => o.Setting).WithMany(s => s.Occurrences)
                    .HasForeignKey(o => o.SettingId).OnDelete(DeleteBehavior.Cascade);
            });

            // ---------------- SMS delivery log (Tasks 7-9) ----------------
            modelBuilder.Entity<SmsLog>(e =>
            {
                // keep the history when a job or an occurrence row is deleted
                e.HasOne<Job>().WithMany()
                 .HasForeignKey(l => l.JobId).OnDelete(DeleteBehavior.SetNull);

                e.HasOne(l => l.AutomatedLog).WithMany()
                 .HasForeignKey(l => l.AutomatedSmsLogId).OnDelete(DeleteBehavior.SetNull);

                // the dispatcher's query: Status = Pending and NextAttemptAt due
                e.HasIndex(l => new { l.Status, l.NextAttemptAt }).HasDatabaseName("IX_SmsLogs_Status_NextAttempt");
                e.HasIndex(l => l.RecipientNumber).HasDatabaseName("IX_SmsLogs_RecipientNumber");
                e.HasIndex(l => l.JobId).HasDatabaseName("IX_SmsLogs_JobId");
                e.HasIndex(l => l.CreatedOn).HasDatabaseName("IX_SmsLogs_CreatedOn");
                e.HasIndex(l => l.SmsDetailId).HasDatabaseName("IX_SmsLogs_SmsDetailId");
            });

            // ---------------- lookup seeds ----------------
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
                new RunFrom { Id = 4, RunFromName = "Job End Date" }   // was "Job Created Date" (code always used the end date)
            );

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