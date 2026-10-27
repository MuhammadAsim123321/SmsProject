using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    public class SmsLogRowVm
    {
        public long Id { get; set; }
        public string When { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string RecipientType { get; set; } = "";
        public string Number { get; set; } = "";
        public string Source { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Reason { get; set; }
        public int Attempts { get; set; }
        public bool Simulated { get; set; }
        public string Message { get; set; } = "";
        public int? JobId { get; set; }
        public string? SentAt { get; set; }
        public string? NextAttempt { get; set; }
        public string QueuedAt { get; set; } = "";
    }

    public class SmsLogsVm
    {
        public List<SmsLogRowVm> Rows { get; set; } = new();
        public Dictionary<string, int> Counts { get; set; } = new();
        public string? Status { get; set; }
        public string? SettingName { get; set; }
        public string? RecipientType { get; set; }
        public List<string> SettingNames { get; set; } = new();
        public string? Q { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int Pages { get; set; } = 1;
        public int Total { get; set; }
        public string Tab { get; set; } = "Scheduled";
        public string? OccurrenceKey { get; set; }
        public List<ScheduledOccurrenceVm> Scheduled { get; set; } = new();
        public List<RunOccurrenceVm> Runs { get; set; } = new();
        public List<TestRunVm> TestRuns { get; set; } = new();
        public int ScheduledCount { get; set; }
        public int QueueCount { get; set; }
        public int SentTodayCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ScheduledOccurrenceVm
    {
        public AutomatedSmsSetting Setting { get; set; } = null!;
        public AutomatedSmsOccurrence? Occurrence { get; set; }
        public List<AutomatedSmsItem> Preview { get; set; } = new();
    }

    public class RunOccurrenceVm
    {
        public AutomatedSmsOccurrence Occurrence { get; set; } = null!;
        public string SettingName { get; set; } = "";
        public int MessageCount { get; set; }
    }

    public class TestRunVm
    {
        public string OccurrenceKey { get; set; } = "";
        public string SettingName { get; set; } = "";
        public string When { get; set; } = "";
        public List<AutomatedSmsLog> Rows { get; set; } = new();
    }

    [SessionChecking]
    public class SmsLogsController : Controller
    {
        private const int PageSize = 50;
        private readonly ApplicationDbContext db;
        private readonly IAppClock clock;
        private readonly AutomatedSmsService service;

        public SmsLogsController(ApplicationDbContext db, IAppClock clock, AutomatedSmsService service)
        {
            this.db = db; this.clock = clock; this.service = service;
        }

        public async Task<IActionResult> Index(string tab = "Scheduled", string? status = null, string? settingName = null,
            string? recipientType = null, string? q = null, DateTime? from = null, DateTime? to = null,
            string? occurrenceKey = null, int page = 1)
        {
            tab = new[] { "Scheduled", "Queue", "History", "Runs", "Test runs" }
                .FirstOrDefault(x => x.Equals(tab, StringComparison.OrdinalIgnoreCase)) ?? "Scheduled";
            if (tab == "Scheduled")
                await service.EnsureNextOccurrencesAsync();

            var query = db.SmsLogs.AsNoTracking()
                .Where(l => l.Source == SmsLogSources.Automated);
            if (tab == "Queue") query = query.Where(l => l.Status == SmsLogStatus.Pending);
            if (tab == "History") query = query.Where(l => l.Status != SmsLogStatus.Pending);
            if (!string.IsNullOrWhiteSpace(occurrenceKey))
                query = query.Where(l => l.AutomatedLog != null && l.AutomatedLog.OccurrenceKey == occurrenceKey);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
            if (!string.IsNullOrWhiteSpace(settingName)) query = query.Where(l => l.SettingName == settingName);
            if (!string.IsNullOrWhiteSpace(recipientType)) query = query.Where(l => l.RecipientType == recipientType);
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(l => l.RecipientNumber.Contains(q)
                    || (l.RecipientName != null && l.RecipientName.Contains(q))
                    || l.MessageContent.Contains(q));
            }
            if (from.HasValue) { var f = clock.ToUtc(from.Value.Date); query = query.Where(l => l.CreatedOn >= f); }
            if (to.HasValue) { var t = clock.ToUtc(to.Value.Date.AddDays(1)); query = query.Where(l => l.CreatedOn < t); }

            var total = await query.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Min(Math.Max(1, page), pages);

            var logs = await query.OrderByDescending(l => l.CreatedOn)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            var automated = db.SmsLogs.AsNoTracking().Where(l => l.Source == SmsLogSources.Automated);
            var counts = await automated
                .Where(l => l.Source == SmsLogSources.Automated)
                .GroupBy(l => l.Status)
                .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
            var settingNames = await db.AutomatedSmsSettings.AsNoTracking()
                .OrderBy(s => s.Name).Select(s => s.Name).ToListAsync();
            var nowUtc = DateTime.UtcNow;
            var scheduled = await db.AutomatedSmsOccurrences.AsNoTracking()
                .Include(o => o.Setting).Where(o => o.Status == AutomatedSmsOccurrenceStatus.Pending ||
                                                     o.Status == AutomatedSmsOccurrenceStatus.Running)
                .OrderBy(o => o.ScheduledForUtc).ToListAsync();
            var scheduledRows = new List<ScheduledOccurrenceVm>();
            foreach (var setting in await db.AutomatedSmsSettings.AsNoTracking().OrderBy(s => s.IsActive ? 0 : 1).ThenBy(s => s.Id).ToListAsync())
            {
                var occurrence = scheduled.FirstOrDefault(o => o.SettingId == setting.Id);
                scheduledRows.Add(new ScheduledOccurrenceVm
                {
                    Setting = setting,
                    Occurrence = occurrence,
                    Preview = setting.IsActive ? await service.PreviewAsync(setting) : new List<AutomatedSmsItem>()
                });
            }
            var runRows = await db.AutomatedSmsOccurrences.AsNoTracking().Include(o => o.Setting)
                .Where(o => o.Status != AutomatedSmsOccurrenceStatus.Pending &&
                            o.Status != AutomatedSmsOccurrenceStatus.Running)
                .OrderByDescending(o => o.ScheduledForUtc).Take(200).ToListAsync();
            var runKeys = runRows.Select(o => o.OccurrenceKey).ToList();
            var messageCounts = await db.SmsLogs.AsNoTracking()
                .Where(l => l.Source == SmsLogSources.Automated && l.AutomatedLog != null &&
                            runKeys.Contains(l.AutomatedLog.OccurrenceKey))
                .GroupBy(l => l.AutomatedLog!.OccurrenceKey)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var tests = await db.AutomatedSmsLogs.AsNoTracking().Where(l => l.Status == AutomatedSmsStatus.Test)
                .OrderByDescending(l => l.CreatedOn).ToListAsync();
            var testRows = tests.GroupBy(l => l.OccurrenceKey).Select(g => new TestRunVm
            {
                OccurrenceKey = g.Key,
                SettingName = settingNames.Count > 0
                    ? db.AutomatedSmsSettings.Where(s => s.Id == g.First().SettingId).Select(s => s.Name).FirstOrDefault() ?? ""
                    : "",
                When = clock.ToBusiness(g.Max(x => x.CreatedOn)).ToString("dd-MMM-yy HH:mm:ss"),
                Rows = g.ToList()
            }).ToList();
            var todayStart = clock.ToUtc(clock.BusinessNow.Date);
            var sentToday = await automated.CountAsync(l => l.Status == SmsLogStatus.MessageSent && l.SentDate >= todayStart);
            var queueCount = await automated.CountAsync(l => l.Status == SmsLogStatus.Pending);

            string Fmt(DateTime utc) => clock.ToBusiness(utc).ToString("dd-MMM-yy HH:mm:ss");

            return View(new SmsLogsVm
            {
                Status = status,
                SettingName = settingName,
                RecipientType = recipientType,
                Q = q,
                From = from,
                To = to,
                Tab = tab,
                OccurrenceKey = occurrenceKey,
                Page = page,
                Pages = pages,
                Total = total,
                Counts = counts,
                SettingNames = settingNames,
                Scheduled = scheduledRows,
                Runs = runRows.Select(o => new RunOccurrenceVm
                {
                    Occurrence = o,
                    SettingName = o.Setting.Name,
                    MessageCount = messageCounts.TryGetValue(o.OccurrenceKey, out var count) ? count : 0
                }).ToList(),
                TestRuns = testRows,
                ScheduledCount = scheduled.Count(o => o.Status == AutomatedSmsOccurrenceStatus.Pending ||
                                                     o.Status == AutomatedSmsOccurrenceStatus.Running),
                QueueCount = queueCount,
                SentTodayCount = sentToday,
                FailedCount = counts.TryGetValue(SmsLogStatus.SmsFailed, out var failed) ? failed : 0,
                SkippedCount = counts.TryGetValue(SmsLogStatus.Skipped, out var skipped) ? skipped : 0,
                Rows = logs.Select(l => new SmsLogRowVm
                {
                    Id = l.Id,
                    When = Fmt(l.CreatedOn),
                    RecipientName = l.RecipientName ?? "",
                    RecipientType = l.RecipientType ?? "",
                    Number = l.RecipientNumber,
                    Type = l.SettingName ?? l.NotificationType ?? "",
                    Status = l.Status,
                    Reason = l.FailureReason,
                    Attempts = l.AttemptCount,
                    Simulated = l.IsSimulated,
                    Message = l.MessageContent,
                    JobId = l.JobId,
                    SentAt = l.SentDate.HasValue ? Fmt(l.SentDate.Value) : null,
                    NextAttempt = l.Status == SmsLogStatus.Pending && l.NextAttemptAt.HasValue
                                  && l.NextAttemptAt > DateTime.UtcNow ? Fmt(l.NextAttemptAt.Value) : null,
                    QueuedAt = Fmt(l.QueuedAt)
                }).ToList()
            });
        }

        /// <summary>Polled by the logs page so the status updates without a manual refresh.</summary>
        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            var automated = db.SmsLogs.AsNoTracking().Where(l => l.Source == SmsLogSources.Automated);
            var counts = await automated.GroupBy(l => l.Status)
                .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
            int Get(string k) => counts.TryGetValue(k, out var v) ? v : 0;
            var todayStart = clock.ToUtc(clock.BusinessNow.Date);
            return Json(new
            {
                scheduled = await db.AutomatedSmsOccurrences.CountAsync(o =>
                    o.Status == AutomatedSmsOccurrenceStatus.Pending ||
                    o.Status == AutomatedSmsOccurrenceStatus.Running),
                queue = Get(SmsLogStatus.Pending),
                sentToday = await automated.CountAsync(l => l.Status == SmsLogStatus.MessageSent && l.SentDate >= todayStart),
                sent = Get(SmsLogStatus.MessageSent),
                failed = Get(SmsLogStatus.SmsFailed),
                skipped = Get(SmsLogStatus.Skipped)
            });
        }

        /// <summary>Put a failed message back in the queue; the dispatcher picks it up within a minute.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Retry(long id)
        {
            var log = await db.SmsLogs.FirstOrDefaultAsync(l => l.Id == id);
            if (log == null || log.Source != SmsLogSources.Automated) return NotFound();
            if (log.Status != SmsLogStatus.SmsFailed)
            {
                TempData["Error"] = "Only failed messages can be retried.";
                return RedirectToAction(nameof(Index), new { status = SmsLogStatus.SmsFailed });
            }

            var now = DateTime.UtcNow;
            log.Status = SmsLogStatus.Pending;
            log.AttemptCount = 0;
            log.QueuedAt = now;
            log.NextAttemptAt = null;
            log.UpdatedOn = now;
            // Keep the last gateway error visible while the retry is pending.
            // The dispatcher replaces it with the next attempt's actual result.

            if (log.AutomatedSmsLogId != null)
            {
                var guard = await db.AutomatedSmsLogs.FirstOrDefaultAsync(g => g.Id == log.AutomatedSmsLogId);
                if (guard != null) { guard.Status = AutomatedSmsStatus.Queued; guard.ErrorMessage = null; }
            }
            await db.SaveChangesAsync();
            TempData["Success"] = "Message queued again.";
            return RedirectToAction(nameof(Index), new { status = SmsLogStatus.Pending });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearTestRuns()
        {
            var tests = await db.AutomatedSmsLogs.Where(l => l.Status == AutomatedSmsStatus.Test).ToListAsync();
            db.AutomatedSmsLogs.RemoveRange(tests);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { tab = "Test runs" });
        }

        [HttpGet]
        public async Task<IActionResult> OccurrenceMessages(string occurrenceKey)
        {
            if (string.IsNullOrWhiteSpace(occurrenceKey)) return BadRequest();

            var logs = await db.SmsLogs.AsNoTracking()
                .Where(l => l.Source == SmsLogSources.Automated &&
                            l.AutomatedLog != null &&
                            l.AutomatedLog.OccurrenceKey == occurrenceKey)
                .OrderBy(l => l.CreatedOn)
                .ToListAsync();
            var occurrence = await db.AutomatedSmsOccurrences.AsNoTracking()
                .Include(o => o.Setting)
                .FirstOrDefaultAsync(o => o.OccurrenceKey == occurrenceKey);
            var rows = logs.Select(l => new SmsLogRowVm
            {
                Id = l.Id,
                When = clock.ToBusiness(l.CreatedOn).ToString("dd-MMM-yy HH:mm:ss"),
                RecipientName = l.RecipientName ?? "",
                RecipientType = l.RecipientType ?? "",
                Number = l.RecipientNumber,
                Type = l.SettingName ?? l.NotificationType ?? "",
                Status = l.Status,
                Reason = l.FailureReason,
                Attempts = l.AttemptCount,
                Simulated = l.IsSimulated,
                Message = l.MessageContent,
                SentAt = l.SentDate.HasValue ? clock.ToBusiness(l.SentDate.Value).ToString("dd-MMM-yy HH:mm:ss") : null,
                QueuedAt = clock.ToBusiness(l.QueuedAt).ToString("dd-MMM-yy HH:mm:ss"),
                NextAttempt = l.NextAttemptAt.HasValue ? clock.ToBusiness(l.NextAttemptAt.Value).ToString("dd-MMM-yy HH:mm:ss") : null
            }).ToList();
            return View(new OccurrenceMessagesVm
            {
                OccurrenceKey = occurrenceKey,
                SettingName = occurrence?.Setting.Name ?? logs.FirstOrDefault()?.SettingName ?? "Automated SMS",
                Occurrence = occurrence,
                Rows = rows
            });
        }
    }

    public class OccurrenceMessagesVm
    {
        public string OccurrenceKey { get; set; } = "";
        public string SettingName { get; set; } = "";
        public AutomatedSmsOccurrence? Occurrence { get; set; }
        public List<SmsLogRowVm> Rows { get; set; } = new();
    }
}