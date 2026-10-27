using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using Job = SendSmsCallAlerts.Models.Job;

namespace SendSmsCallAlerts.Jobs
{
    /// <summary>
    /// Runs every minute (Hangfire). For every active message plan (AllScheduledJob) it decides
    /// whether the plan is due in the last window and sends through ISmsGateway.
    /// State is kept per plan (LastExecutedAt). It never writes to Scheduler or to the job's times.
    /// </summary>
    public class SmsJob
    {
        private const int LOOK_BACK_MINUTES = 1;      // first ever run
        private const int MAX_CATCH_UP_HOURS = 24;    // after downtime, never go further back than this
        private const int KEEP_LOG_DAYS = 2;

        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;
        private readonly ISmsGateway _gateway;
        private readonly ILogger<SmsJob> _logger;
        private readonly SmsLogService _smsLog;

        public SmsJob(ApplicationDbContext db, IAppClock clock, ISmsGateway gateway,
            ILogger<SmsJob> logger, SmsLogService smsLog)
        {
            _db = db;
            _clock = clock;
            _gateway = gateway;
            _logger = logger;
            _smsLog = smsLog;
        }

        public async Task RegisterAllJobs()
        {
            DateTime now = _clock.BusinessNow;
            DateTime windowStart = await GetWindowStartAsync(now);
            await LogExecutionAsync(now);

            var plans = await GetActivePlansAsync();
            if (plans.Count == 0) return;

            foreach (var plan in plans)
            {
                var slot = GetDueSlot(plan, windowStart, now);
                if (slot == null) continue;
                await SendAsync(plan, now);
            }

            await _db.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ window
        private async Task<DateTime> GetWindowStartAsync(DateTime now)
        {
            var last = await _db.HangfireJobLogs.OrderByDescending(x => x.ExecutedAt).FirstOrDefaultAsync();
            var earliest = now.AddHours(-MAX_CATCH_UP_HOURS);
            if (last == null) return now.AddMinutes(-LOOK_BACK_MINUTES);
            return last.ExecutedAt < earliest ? earliest : last.ExecutedAt;
        }

        private async Task LogExecutionAsync(DateTime now)
        {
            _db.HangfireJobLogs.Add(new HangfireJobLog { ExecutedAt = now });
            await _db.SaveChangesAsync();

            var cutoff = now.AddDays(-KEEP_LOG_DAYS);
            await _db.HangfireJobLogs.Where(x => x.ExecutedAt < cutoff).ExecuteDeleteAsync();
        }

        // ------------------------------------------------------------------ which plans
        private async Task<List<AllScheduledJob>> GetActivePlansAsync()
        {
            var optOut = (await _db.OptOuts.Select(o => o.PhoneNumber).ToListAsync())
                .Select(Last10).Where(x => x.Length > 0).ToHashSet();

            var plans = await _db.AllScheduledJobs
                .Include(p => p.Scheduler).ThenInclude(s => s.TimeToRun)
                .Include(p => p.Job).ThenInclude(j => j.ContactDetail)
                .Include(p => p.Job).ThenInclude(j => j.Category)
                .Include(p => p.Job).ThenInclude(j => j.Technician)
                .Include(p => p.Job).ThenInclude(j => j.SourceSms)
                .Where(p => !p.IsPaused
                            && p.Scheduler.status.ToLower() == "active"
                            && p.Scheduler.IsPaused != true
                            && p.Job.Status != JobStatus.Cancelled)
                .ToListAsync();

            return plans
                .Where(p => p.Job.ContactDetail != null && !optOut.Contains(Last10(p.Job.ContactDetail.PhoneNumber)))
                .ToList();
        }

        // ------------------------------------------------------------------ when is it due
        /// <summary>
        /// Once   : first due moment = run-from date + hours. Sent once (LastExecutedAt stays empty until then).
        /// Repeat : same time of day, every day, from the first due moment until the job's date (inclusive).
        ///          If the first due moment is already after the job's date it behaves like Once.
        /// Run-from: 1 = plan assigned date, 2 = custom date, 3 = job start, 4 = job end.
        /// </summary>
        private DateTime? GetDueSlot(AllScheduledJob plan, DateTime windowStart, DateTime now)
        {
            var scheduler = plan.Scheduler;
            var job = plan.Job;

            int hours = scheduler.TimeToRun?.HourCount ?? 0;
            if (hours == JobService.TimeToRunCustomMarker) hours = plan.CustomHours ?? 0;

            DateTime? runFrom = scheduler.RunFromId switch
            {
                1 => plan.AssignedDate,
                2 => plan.CustomDate ?? plan.AssignedDate,
                3 => job.StartAt,
                4 => job.EndAt,
                _ => null
            };
            if (runFrom == null) return null;

            var first = runFrom.Value.AddHours(hours);
            bool repeat = string.Equals(scheduler.onceOrRepeat, "Repeat", StringComparison.OrdinalIgnoreCase);
            var lastDay = job.EndAt.Date;

            if (!repeat || first.Date > lastDay)
            {
                if (plan.LastExecutedAt != null) return null;
                return InWindow(first, windowStart, now) ? first : null;
            }

            var today = now.Date;
            if (today < first.Date || today > lastDay) return null;

            var slot = today + first.TimeOfDay;
            if (!InWindow(slot, windowStart, now)) return null;
            if (plan.LastExecutedAt != null && plan.LastExecutedAt.Value >= slot) return null;
            return slot;
        }

        private static bool InWindow(DateTime t, DateTime start, DateTime end) => t >= start && t <= end;

        // ------------------------------------------------------------------ send
        private async Task SendAsync(AllScheduledJob plan, DateTime now)
        {
            var contact = plan.Job.ContactDetail!;
            var scheduler = plan.Scheduler;
            var body = BuildMessage(plan, contact);

            var images = await _db.MmsLinkss.Where(m => m.SchedulerId == scheduler.Id && m.SmsId == 0).ToListAsync();
            var mediaUrls = images.Select(i => new Uri(i.Location)).ToList();

            var send = await _gateway.SendAsync(contact.PhoneNumber ?? "", body, mediaUrls, plan.Job.SourceSms?.toNum);

            if (send.Success)
            {
                int smsId = await SaveSmsLogAsync(
                    contact.PhoneNumber ?? "",
                    send.From,
                    body,
                    send.Simulated ? "OutgoingTest" : "Outgoing",
                    contact.Id,
                    plan.Id);
                foreach (var img in images)
                {
                    _db.MmsLinkss.Add(new MmsLinks
                    {
                        SmsId = smsId,
                        FileName = img.FileName,
                        Location = img.Location,
                        SchedulerId = img.SchedulerId
                    });
                }
                plan.LastExecutedAt = now;
            }
            else
            {
                _logger.LogWarning("Plan {PlanId} send failed: {Error}", plan.Id, send.Error);
                await SaveSmsLogAsync(contact.PhoneNumber ?? "", send.From, body + "\n[Error]: " + send.Error,
                    "OutgoingFailed", contact.Id, plan.Id);
            }
        }

        private string BuildMessage(AllScheduledJob plan, ContactDetail contact)
        {
            var job = plan.Job;
            string template = plan.Scheduler.smsBody ?? "";
            string customerName = contact.Name ?? "";

            string message = template
                .Replace("{name}", customerName)
                .Replace("{email}", contact.Email ?? "")
                .Replace("{address}", contact.Address ?? "")
                .Replace("{jobBookDate}", job.StartAt.ToString("dd-MMM-yyyy"))
                .Replace("{jobBookedTime}", job.StartAt.ToString("hh:mm tt"))
                .Replace("{jobCompletedDate}", job.EndAt.ToString("dd-MMM-yyyy HH:mm"))
                .Replace("{category}", job.Category?.keyword ?? "")
                .Replace("{technician}", job.Technician?.FullName ?? "");

            if (!template.Contains("{name}") && customerName.Length > 0)
                message = customerName + " " + message;

            return message;
        }

        private async Task<int> SaveSmsLogAsync(string toNum, string fromNum, string body, string direction, int contactId, int planId)
        {
            var sms = new SmsDetail
            {
                toNum = toNum,
                fromNum = fromNum,
                smsBody = body,
                smsDirection = direction,
                createdOn = _clock.UtcNow,
                IsReadYesOrNo = "",
                imgPath = "",
                ContactDetailId = contactId,
                ScheduledJobId = planId
            };
            _db.SmsDetails.Add(sms);
            await _db.SaveChangesAsync();
            return sms.Id;
        }

        private static string Last10(string? s)
        {
            var d = new string((s ?? "").Where(char.IsDigit).ToArray());
            return d.Length > 10 ? d.Substring(d.Length - 10) : d;
        }
    }
}