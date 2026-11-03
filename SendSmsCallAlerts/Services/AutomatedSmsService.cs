//using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SendSmsCallAlerts.Services
{
    public class AutomatedSmsItem
    {
        public string RecipientType { get; set; } = "";
        public int RecipientId { get; set; }
        public string RecipientName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Body { get; set; } = "";
        public int? ContactDetailId { get; set; }
        public string? SkipReason { get; set; }
        public string Status { get; set; } = "";
        public int? JobId { get; set; }
        public List<int>? JobIds { get; set; }   // technician summary: job ids in SMS number order
        public DateTime? JobDate { get; set; }
        public List<TechnicianJobList>? JobList { get; set; }
    }

    public class AutomatedSmsService
    {
        private readonly int graceHours;

        private readonly ApplicationDbContext db;
        private readonly IConfiguration config;
        private readonly ILogger<AutomatedSmsService> logger;
        private readonly IAppClock clock;
        private readonly ISmsGateway gateway;
        private readonly string companyName;
        private readonly int maxJobsChars;
        private readonly GoogleDistanceService distance;
        private readonly string officePhone;

        public AutomatedSmsService(ApplicationDbContext db, IConfiguration config, ILogger<AutomatedSmsService> logger,
    IAppClock clock, ISmsGateway gateway, GoogleDistanceService distance)
        {
            this.db = db;
            this.config = config;
            this.logger = logger;
            this.clock = clock;
            this.gateway = gateway;
            companyName = config["AutomatedSms:CompanyName"] ?? "Our Team";
            maxJobsChars = config.GetValue<int?>("AutomatedSms:MaxJobsListCharacters") ?? 1400;
            graceHours = config.GetValue<int?>("AutomatedSms:GraceHours") ?? 3;
            this.distance = distance;
            var office = (config["AutomatedSms:OfficePhone"] ?? "").Trim();
            officePhone = office.Length > 0 ? office : "the office";
        }

        // ---------- time helpers (kept public: other code may call them) ----------
        public DateTime BusinessNow() => clock.BusinessNow;
        public DateTime ToBusiness(DateTime utc) => clock.ToBusiness(utc);

        public async Task EnsureNextOccurrenceAsync(AutomatedSmsSetting setting, CancellationToken ct = default)
        {
            if (!setting.IsActive) return;
            var staleRunningBefore = DateTime.UtcNow.AddMinutes(-10);
            await db.AutomatedSmsOccurrences
                .Where(o => o.SettingId == setting.Id &&
                            o.Status == AutomatedSmsOccurrenceStatus.Running &&
                            (o.StartedAt == null || o.StartedAt < staleRunningBefore))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(o => o.Status, AutomatedSmsOccurrenceStatus.Pending)
                    .SetProperty(o => o.StartedAt, (DateTime?)null)
                    .SetProperty(o => o.Note, "Recovered after an interrupted run."), ct);
            var pending = await db.AutomatedSmsOccurrences.AnyAsync(o =>
                o.SettingId == setting.Id &&
                (o.Status == AutomatedSmsOccurrenceStatus.Pending || o.Status == AutomatedSmsOccurrenceStatus.Running), ct);
            if (pending) return;

            var localNow = BusinessNow();
            var scheduled = NextScheduledLocal(setting, localNow);
            var occurrence = new AutomatedSmsOccurrence
            {
                SettingId = setting.Id,
                ScheduledForLocal = scheduled,
                ScheduledForUtc = clock.ToUtc(scheduled),
                OccurrenceKey = BuildOccurrenceKey(setting, scheduled),
                Status = AutomatedSmsOccurrenceStatus.Pending,
                CreatedOn = DateTime.UtcNow
            };
            db.AutomatedSmsOccurrences.Add(occurrence);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.Entry(occurrence).State = EntityState.Detached;
            }
        }

        public async Task EnsureNextOccurrencesAsync(CancellationToken ct = default)
        {
            foreach (var setting in await db.AutomatedSmsSettings.Where(s => s.IsActive).ToListAsync(ct))
                await EnsureNextOccurrenceAsync(setting, ct);
        }

        public async Task CancelPendingOccurrencesAsync(int settingId, string note, CancellationToken ct = default)
        {
            var pending = await db.AutomatedSmsOccurrences
                .Where(o => o.SettingId == settingId && o.Status == AutomatedSmsOccurrenceStatus.Pending)
                .ToListAsync(ct);
            foreach (var occurrence in pending)
            {
                occurrence.Status = AutomatedSmsOccurrenceStatus.Cancelled;
                occurrence.Note = note;
                occurrence.CompletedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<AutomatedSmsItem>> PreviewAsync(AutomatedSmsSetting setting, CancellationToken ct = default) =>
            await BuildItemsAsync(setting, BusinessNow(), ct);

        private DateTime NextScheduledLocal(AutomatedSmsSetting setting, DateTime nowLocal)
        {
            if (setting.Frequency == SmsFrequencies.Daily)
            {
                var today = nowLocal.Date + setting.SendTime;
                return today > nowLocal ? today : today.AddDays(1);
            }

            var day = setting.DayOfWeek ?? (int)nowLocal.DayOfWeek;
            var daysAhead = (day - (int)nowLocal.DayOfWeek + 7) % 7;
            var candidate = nowLocal.Date.AddDays(daysAhead) + setting.SendTime;
            return candidate > nowLocal ? candidate : candidate.AddDays(7);
        }

        private static string BuildOccurrenceKey(AutomatedSmsSetting setting, DateTime scheduled)
        {
            if (setting.Frequency == SmsFrequencies.Daily)
                return $"{setting.Id}-{scheduled:yyyyMMdd}";
            var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(scheduled);
            return $"{setting.Id}-{System.Globalization.ISOWeek.GetYear(scheduled)}-W{isoWeek:00}";
        }

        public async Task ProcessDueAsync(CancellationToken ct = default)
        {
            await EnsureNextOccurrencesAsync(ct);
            var resetBefore = DateTime.UtcNow.AddMinutes(-10);
            await db.AutomatedSmsOccurrences
                .Where(o => o.Status == AutomatedSmsOccurrenceStatus.Running &&
                            o.StartedAt < resetBefore)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(o => o.Status, AutomatedSmsOccurrenceStatus.Pending)
                    .SetProperty(o => o.StartedAt, (DateTime?)null), ct);

            var now = DateTime.UtcNow;
            var dueIds = await db.AutomatedSmsOccurrences.AsNoTracking()
                .Where(o => o.Status == AutomatedSmsOccurrenceStatus.Pending && o.ScheduledForUtc <= now)
                .OrderBy(o => o.ScheduledForUtc).Select(o => o.Id).Take(100).ToListAsync(ct);

            foreach (var id in dueIds)
            {
                var claimed = await db.AutomatedSmsOccurrences
                    .Where(o => o.Id == id && o.Status == AutomatedSmsOccurrenceStatus.Pending)
                    .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, AutomatedSmsOccurrenceStatus.Running)
                        .SetProperty(o => o.StartedAt, now), ct);
                if (claimed == 0) continue;

                try
                {
                    var occurrence = await db.AutomatedSmsOccurrences
                        .Include(o => o.Setting).SingleAsync(o => o.Id == id, ct);
                    var setting = occurrence.Setting;
                    if (now > occurrence.ScheduledForUtc.AddHours(graceHours))
                    {
                        occurrence.Status = AutomatedSmsOccurrenceStatus.Missed;
                        occurrence.Note = "Not run within grace period (app was down?)";
                    }
                    else
                    {
                        var items = await RunAsync(setting, occurrence.OccurrenceKey, false, BusinessNow(), ct);
                        occurrence.TotalRecipients = items.Count;
                        occurrence.QueuedCount = items.Count(i => i.SkipReason == null);
                        occurrence.SkippedCount = items.Count(i => i.SkipReason != null);
                        occurrence.Status = items.Count == 0
                            ? AutomatedSmsOccurrenceStatus.NothingToSend
                            : AutomatedSmsOccurrenceStatus.Completed;
                        occurrence.Note = items.Count == 0 ? "No recipients matched the notification." : null;
                        setting.LastRunAt = now;
                    }
                    occurrence.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    await EnsureNextOccurrenceAsync(setting, ct);
                }
                catch (Exception ex)
                {
                    var failed = await db.AutomatedSmsOccurrences.AsNoTracking()
                        .Include(o => o.Setting).FirstOrDefaultAsync(o => o.Id == id, ct);
                    if (failed != null)
                    {
                        await db.AutomatedSmsOccurrences.Where(o => o.Id == id).ExecuteUpdateAsync(u => u
                            .SetProperty(o => o.Status, AutomatedSmsOccurrenceStatus.Error)
                            .SetProperty(o => o.CompletedAt, DateTime.UtcNow)
                            .SetProperty(o => o.Note, Truncate(ex.Message, 500)), ct);
                        await EnsureNextOccurrenceAsync(failed.Setting, ct);
                        logger.LogError(ex, "Automated SMS occurrence {OccurrenceId}, setting {SettingId} ({Name}) failed",
                            id, failed.SettingId, failed.Setting.Name);
                    }
                    db.ChangeTracker.Clear();
                }
            }
        }

        // ---------- one run of one setting (real or dry-run) ----------
        public async Task<List<AutomatedSmsItem>> RunAsync(
    AutomatedSmsSetting s, string occurrenceKey, bool dryRun, DateTime nowLocal, CancellationToken ct = default)
        {
            var items = await BuildItemsAsync(s, nowLocal, ct);
            if (!dryRun)   // lists of past days are not needed any more
                await db.TechnicianJobLists.Where(l => l.JobDate < nowLocal.Date).ExecuteDeleteAsync(ct);

            foreach (var item in items)
            {
                // guard row: the unique index makes sure one occurrence is queued only once
                var guard = new AutomatedSmsLog
                {
                    SettingId = s.Id,
                    OccurrenceKey = occurrenceKey,
                    RecipientType = item.RecipientType,
                    RecipientId = item.RecipientId,
                    PhoneNumber = item.Phone,
                    Status = dryRun ? AutomatedSmsStatus.Test
                           : item.SkipReason != null ? AutomatedSmsStatus.Skipped
                           : AutomatedSmsStatus.Queued,
                    ErrorMessage = item.SkipReason
                };
                db.AutomatedSmsLogs.Add(guard);

                SmsLog? sms = null;
                if (!dryRun)   // dry run ("Run Now") never writes delivery rows
                {
                    sms = new SmsLog
                    {
                        Source = SmsLogSources.Automated,
                        SettingName = s.Name,
                        NotificationType = s.NotificationType,
                        RecipientType = item.RecipientType,
                        RecipientId = item.RecipientId,
                        RecipientName = item.RecipientName,
                        RecipientNumber = item.Phone ?? "",
                        MessageContent = item.Body,
                        JobId = item.JobId,
                        AutomatedLog = guard,
                        Status = item.SkipReason != null ? SmsLogStatus.Skipped : SmsLogStatus.Pending,
                        FailureReason = item.SkipReason,
                        QueuedAt = DateTime.UtcNow
                    };
                    db.SmsLogs.Add(sms);
                }

                try { await db.SaveChangesAsync(ct); }     // guard + SmsLogs row in ONE transaction
                catch (DbUpdateException ex)
                {
                    if (sms != null) db.Entry(sms).State = EntityState.Detached;
                    db.Entry(guard).State = EntityState.Detached;
                    var already = await db.AutomatedSmsLogs.AnyAsync(g => g.SettingId == s.Id &&
                        g.OccurrenceKey == occurrenceKey && g.RecipientType == item.RecipientType &&
                        g.RecipientId == item.RecipientId, ct);
                    if (!already) logger.LogError(ex, "Could not queue SMS for {Type} {Id}", item.RecipientType, item.RecipientId);
                    item.Status = already ? "Already handled" : "Error: could not queue";
                    continue;
                }
                if (!dryRun && item.SkipReason == null && item.JobList != null)
                    await SaveJobListAsync(item.RecipientId, item.JobList, ct);

                item.Status = item.SkipReason != null ? "Skipped: " + item.SkipReason
                            : dryRun ? "Test (not sent)"
                            : SmsLogStatus.Pending;

                if (!dryRun && item.SkipReason == null && item.JobIds is { Count: > 0 } && item.JobDate.HasValue)
                    await SaveTechnicianJobListAsync(item.RecipientId, item.JobDate.Value, item.JobIds, ct);
            }
            return items;
        }

        // ---------- who gets what ----------
        private async Task<List<AutomatedSmsItem>> BuildItemsAsync(AutomatedSmsSetting s, DateTime nowLocal, CancellationToken ct)
        {
            switch (s.NotificationType)
            {
                case NotificationTypes.TechnicianAvailabilityRequest: return await BuildWeeklyAvailabilityAsync(s, nowLocal, ct);
                case NotificationTypes.TechnicianDailyJobSummary: return await BuildDailySummaryAsync(s, nowLocal, ct);
                case NotificationTypes.CustomerJobReminder: return await BuildCustomerReminderAsync(s, nowLocal, ct);
                default: return new List<AutomatedSmsItem>();
            }
        }

        private async Task<List<AutomatedSmsItem>> BuildWeeklyAvailabilityAsync(AutomatedSmsSetting s, DateTime nowLocal, CancellationToken ct)
        {
            int toMonday = ((int)DayOfWeek.Monday - (int)nowLocal.DayOfWeek + 7) % 7;
            if (toMonday == 0) toMonday = 7;
            var weekStart = nowLocal.Date.AddDays(toMonday);
            var weekEnd = weekStart.AddDays(6);

            var techs = await db.Technicians.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
            var items = new List<AutomatedSmsItem>();
            foreach (var t in techs)
            {
                var full = TechFullName(t);
                var vars = new Dictionary<string, string>
                {
                    ["technicianFirstName"] = TechFirstName(t),
                    ["technicianName"] = full,
                    ["weekStart"] = weekStart.ToString("MMM d"),
                    ["weekEnd"] = weekEnd.ToString("MMM d"),
                    ["companyName"] = companyName
                };
                items.Add(MakeItem(RecipientTypes.Technician, t.Id, full, t.ContactNumber, Render(s.TemplateBody, vars), null));
            }
            return items;
        }

        private async Task<List<AutomatedSmsItem>> BuildDailySummaryAsync(AutomatedSmsSetting s, DateTime nowLocal, CancellationToken ct)
        {
            var jobs = await TomorrowJobsAsync(nowLocal, ct);

            var tomorrow = nowLocal.Date.AddDays(1);

            var byTech = jobs.Where(j => j.TechnicianId != null).GroupBy(j => j.TechnicianId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(j => j.StartAt).ThenBy(j => j.Id).ToList());

            var techIds = byTech.Keys.ToList();

            var techs = await db.Technicians.AsNoTracking().Where(t => techIds.Contains(t.Id)).ToListAsync(ct);

            var items = new List<AutomatedSmsItem>();

            foreach (var t in techs)
            {
                var tJobs = byTech[t.Id];

                var miles = await ChainMilesAsync(t, tJobs);

                var lines = tJobs.Select((j, i) => JobLine(i + 1, j, miles[i])).ToList();

                var (jobsText, shown) = JoinJobLines(lines);

                var full = TechFullName(t);

                var vars = new Dictionary<string, string>
                {
                    ["technicianFirstName"] = TechFirstName(t),
                    ["technicianName"] = full,
                    ["jobCount"] = shown.ToString(),
                    ["jobDate"] = tomorrow.ToString("dd-MMM-yyyy"),
                    ["jobsList"] = jobsText,
                    ["replyExample"] = ReplyExample(shown),
                    ["officePhone"] = officePhone,
                    ["companyName"] = companyName,
                    ["jobConfirmationCode"] = GenerateJobConfirmationCode(shown)
                };

                var item = MakeItem(RecipientTypes.Technician, t.Id, full, t.ContactNumber, Tidy(Render(s.TemplateBody, vars)), null);

                item.JobIds = tJobs.Take(shown).Select(j => j.Id).ToList();

                item.JobDate = tomorrow;

                items.Add(item);

            }

            return items;
        }

        private async Task<List<AutomatedSmsItem>> BuildCustomerReminderAsync(AutomatedSmsSetting s, DateTime nowLocal, CancellationToken ct)
        {
            var jobs = await TomorrowJobsAsync(nowLocal, ct, confirmedOnly: true);
            var allTechs = await db.Technicians.AsNoTracking().ToListAsync(ct);
            var techNames = allTechs.ToDictionary(t => t.Id, t => TechFullName(t));
            var optOut = (await db.OptOuts.Select(o => o.PhoneNumber).ToListAsync(ct))
                         .Select(Last10).Where(x => x.Length > 0).ToHashSet();
            var tomorrow = nowLocal.Date.AddDays(1);

            var items = new List<AutomatedSmsItem>();
            foreach (var g in jobs.Where(j => j.ContactDetail != null).GroupBy(j => j.ContactDetail!.Id)) // one SMS per customer
            {
                var contact = g.First().ContactDetail!;
                var list = g.OrderBy(j => j.StartAt).ToList();

                var vars = new Dictionary<string, string>
                {
                    ["customerName"] = CustomerFullName(contact),
                    ["category"] = string.Join(", ", list.Select(j => j.Category?.keyword ?? "").Where(x => x != "").Distinct()),
                    ["jobDate"] = tomorrow.ToString("dd-MMM-yyyy"),
                    ["jobTime"] = string.Join(", ", list.Select(j => j.StartAt.ToString("h:mm tt"))),
                    ["technicianName"] = string.Join(", ", list
                        .Where(j => j.TechnicianId != null && techNames.ContainsKey(j.TechnicianId.Value))
                        .Select(j => techNames[j.TechnicianId!.Value]).Distinct()),
                    ["companyName"] = companyName,
                    ["officePhone"] = officePhone
                };
                var item = MakeItem(RecipientTypes.Customer, contact.Id, CustomerFullName(contact),
                    contact.PhoneNumber, Tidy(Render(s.TemplateBody, vars)), contact.Id);

                item.JobId = list.First().Id;

                if (item.SkipReason == null && optOut.Contains(Last10(contact.PhoneNumber)))
                    item.SkipReason = "Customer opted out";
                items.Add(item);
            }
            return items;
        }

        // ---------- helpers ----------
        /// <summary>Tomorrow's jobs that are not cancelled (Scheduled / Confirmed).</summary>
        private async Task<List<Job>> TomorrowJobsAsync(DateTime nowLocal, CancellationToken ct, bool confirmedOnly = false)
        {
            var start = nowLocal.Date.AddDays(1);
            var end = start.AddDays(1);
            return await db.Jobs.AsNoTracking()
                .Include(j => j.ContactDetail)
                .Include(j => j.Category)
                .Where(j => j.StartAt >= start && j.StartAt < end
                            && (confirmedOnly ? j.Status == JobStatus.Confirmed : j.Status != JobStatus.Cancelled))
                .OrderBy(j => j.StartAt).ThenBy(j => j.Id)
                .ToListAsync(ct);
        }

        private async Task SaveJobListAsync(int technicianId, List<TechnicianJobList> rows, CancellationToken ct)
        {
            try
            {
                await db.TechnicianJobLists.Where(l => l.TechnicianId == technicianId).ExecuteDeleteAsync(ct);
                foreach (var r in rows) r.SentAtUtc = DateTime.UtcNow;
                db.TechnicianJobLists.AddRange(rows);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                foreach (var r in rows) db.Entry(r).State = EntityState.Detached;
                logger.LogError(ex, "Could not save the job list of technician {Id}", technicianId);
            }
        }

        private static string Addr(ContactDetail c) => c.Address ?? "";

        private static string FirstWord(string full) =>
            full.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? full;

        public static string TechFullName(Technician t)
        {
            var combined = $"{t.FirstName} {t.LastName}".Trim();
            return combined.Length > 0 ? combined : (t.FullName ?? "").Trim();
        }

        private static string GenerateJobConfirmationCode(int jobCount)
        {
            return jobCount switch
            {
                0 => "",
                1 => "Reply C to confirm your job.",
                2 => "Reply C 1 2 to confirm jobs 1 and 2.",
                3 => "Reply C 1 2 3 to confirm jobs 1, 2 and 3.",
                4 => "Reply C 1 2 3 4 to confirm jobs 1, 2, 3 and 4.",
                5 => "Reply C 1 2 3 4 5 to confirm jobs 1, 2, 3, 4 and 5.",
                _ => $"Reply C with space-separated job numbers (1-{jobCount}) to confirm."
            };
        }

        public static string TechFirstName(Technician t)
        {
            var first = (t.FirstName ?? "").Trim();
            return first.Length > 0 ? first : FirstWord((t.FullName ?? "").Trim());
        }

        public static string CustomerFullName(ContactDetail c) => (c.Name ?? "").Trim();

        private AutomatedSmsItem MakeItem(string type, int id, string name, string? rawPhone, string body, int? contactId)
        {
            var phone = gateway.NormalizePhone(rawPhone);
            return new AutomatedSmsItem
            {
                RecipientType = type,
                RecipientId = id,
                RecipientName = name,
                Phone = phone,
                Body = body,
                ContactDetailId = contactId,
                SkipReason = phone.Length == 0 ? "No phone number" : null
            };
        }

        private static string Render(string template, Dictionary<string, string> vars)
        {
            foreach (var kv in vars) template = template.Replace("{" + kv.Key + "}", kv.Value ?? "");
            return template;
        }

        private string JoinJobs(List<string> lines)
        {
            const string sep = "\n\n";
            var text = string.Join(sep, lines);
            if (text.Length <= maxJobsChars) return text;
            var cut = text.Substring(0, maxJobsChars);
            var idx = cut.LastIndexOf(sep);
            if (idx > 0) cut = cut.Substring(0, idx);
            return cut + "\n\n...call the office for the full schedule.";
        }

        private static string Digits(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());
        private static string Last10(string? s) { var d = Digits(s); return d.Length > 10 ? d.Substring(d.Length - 10) : d; }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        // ---------- NEW METHODS FOR TASK 2/3 ----------

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // miles chain: technician ZIP -> job 1 ZIP -> job 2 ZIP -> ... ; missing ZIP or Google failure = no miles for that line
        private async Task<double?[]> ChainMilesAsync(Technician t, List<Job> jobs)
        {
            var result = new double?[jobs.Count];
            string? origin = t.ZipCode;
            for (int i = 0; i < jobs.Count; i++)
            {
                var dest = jobs[i].ContactDetail?.ZipCode;
                if (!string.IsNullOrWhiteSpace(origin) && !string.IsNullOrWhiteSpace(dest))
                    result[i] = await distance.GetDistanceInMilesOrNull(origin!, dest!);
                origin = dest;
            }
            return result;
        }

        // "(1) 8:00 AM - Carpet Repair - Orange CA - $700 - 35 miles"; empty parts are left out
        private static string JobLine(int n, Job j, double? miles)
        {
            var parts = new List<string> { j.StartAt.ToString("h:mm tt", Inv) };
            var cat = CleanText(j.Category?.keyword); if (cat.Length > 0) parts.Add(cat);
            var city = CleanText(j.ContactDetail?.CityState); if (city.Length > 0) parts.Add(city);
            if (j.QuotedAmount.HasValue && j.QuotedAmount.Value > 0) parts.Add(FormatMoney(j.QuotedAmount.Value));
            if (miles.HasValue) parts.Add(FormatMiles(miles.Value));
            return $"({n}) " + string.Join(" - ", parts);
        }

        private static string FormatMoney(decimal a) => "$" + (a % 1 == 0 ? a.ToString("N0", Inv) : a.ToString("N2", Inv));

        private static string FormatMiles(double m)
        {
            var r = (int)Math.Round(m, MidpointRounding.AwayFromZero);
            return r <= 0 ? "under 1 mile" : r == 1 ? "1 mile" : $"{r} miles";
        }

        private static string CleanText(string? s) => Regex.Replace((s ?? "").Trim(), @"\s+", " ");

        private static string Tidy(string text)
        {
            text = (text ?? "").Replace("\r\n", "\n");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @" ?\n ?", "\n");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text.Trim();
        }

        // "C 123" for 3 jobs, "C 1" for 1 job
        private static string ReplyExample(int count) =>
            count <= 1 ? "C 1" : count <= 9 ? "C " + string.Concat(Enumerable.Range(1, count)) : "C 1,2,3";

        // one line per job, "\n" between lines; returns how many lines fit in maxJobsChars (only those get numbers)
        private (string Text, int Shown) JoinJobLines(List<string> lines)
        {
            var text = string.Join("\n", lines);
            if (text.Length <= maxJobsChars) return (text, lines.Count);
            var kept = new List<string>(); var len = 0;
            foreach (var l in lines)
            {
                var add = (kept.Count == 0 ? 0 : 1) + l.Length;
                if (len + add > maxJobsChars) break;
                kept.Add(l); len += add;
            }
            return (string.Join("\n", kept) + "\n...call the office for the rest.", kept.Count);
        }

        // replaces the technician's list with the one just queued; deletes lists of past days
        private async Task SaveTechnicianJobListAsync(int technicianId, DateTime jobDate, List<int> jobIds, CancellationToken ct)
        {
            try
            {
                var starts = await db.Jobs.AsNoTracking().Where(j => jobIds.Contains(j.Id))
                    .ToDictionaryAsync(j => j.Id, j => j.StartAt, ct);
                var today = BusinessNow().Date;
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await db.TechnicianJobLists.Where(l => l.TechnicianId == technicianId || l.JobDate < today).ExecuteDeleteAsync(ct);
                for (int i = 0; i < jobIds.Count; i++)
                    if (starts.TryGetValue(jobIds[i], out var startAt))
                        db.TechnicianJobLists.Add(new TechnicianJobList
                        { TechnicianId = technicianId, JobDate = jobDate, Position = i + 1, JobId = jobIds[i], JobStartAt = startAt });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                foreach (var e in db.ChangeTracker.Entries<TechnicianJobList>().ToList()) e.State = EntityState.Detached;
                logger.LogError(ex, "Could not save job list for technician {TechnicianId}", technicianId);
            }
        }
    }
}
