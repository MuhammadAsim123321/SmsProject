//using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

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
    }

    public class AutomatedSmsService
    {
        private const int GraceHours = 3;

        private readonly ApplicationDbContext db;
        private readonly IConfiguration config;
        private readonly ILogger<AutomatedSmsService> logger;
        private readonly IAppClock clock;
        private readonly ISmsGateway gateway;
        private readonly string companyName;
        private readonly int maxJobsChars;

        public AutomatedSmsService(ApplicationDbContext db, IConfiguration config, ILogger<AutomatedSmsService> logger,
            IAppClock clock, ISmsGateway gateway)
        {
            this.db = db;
            this.config = config;
            this.logger = logger;
            this.clock = clock;
            this.gateway = gateway;
            companyName = config["AutomatedSms:CompanyName"] ?? "Our Team";
            maxJobsChars = config.GetValue<int?>("AutomatedSms:MaxJobsListCharacters") ?? 1400;
        }

        // ---------- time helpers (kept public: other code may call them) ----------
        public DateTime BusinessNow() => clock.BusinessNow;
        public DateTime ToBusiness(DateTime utc) => clock.ToBusiness(utc);

        private bool IsDue(AutomatedSmsSetting s, DateTime nowLocal)
        {
            var scheduled = nowLocal.Date + s.SendTime;
            if (nowLocal < scheduled || nowLocal >= scheduled.AddHours(GraceHours)) return false;
            if (s.Frequency == SmsFrequencies.Weekly && s.DayOfWeek != (int)nowLocal.DayOfWeek) return false;
            if (s.LastRunAt.HasValue && ToBusiness(s.LastRunAt.Value) >= scheduled) return false;
            return true;
        }

        // ---------- called by Hangfire every minute ----------
        public async Task ProcessDueAsync(CancellationToken ct = default)
        {
            var nowLocal = BusinessNow();
            var settings = await db.AutomatedSmsSettings.Where(s => s.IsActive).ToListAsync(ct);

            foreach (var s in settings)
            {
                if (!IsDue(s, nowLocal)) continue;
                try
                {
                    await RunAsync(s, nowLocal.ToString("yyyy-MM-dd"), false, nowLocal, ct);
                    s.LastRunAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Automated SMS setting {Id} failed", s.Id);
                }
            }
        }

        // ---------- one run of one setting (real or dry-run) ----------
        public async Task<List<AutomatedSmsItem>> RunAsync(
            AutomatedSmsSetting s, string occurrenceKey, bool dryRun, DateTime nowLocal, CancellationToken ct = default)
        {
            var items = await BuildItemsAsync(s, nowLocal, ct);

            foreach (var item in items)
            {
                var log = new AutomatedSmsLog
                {
                    SettingId = s.Id,
                    OccurrenceKey = occurrenceKey,
                    RecipientType = item.RecipientType,
                    RecipientId = item.RecipientId,
                    PhoneNumber = item.Phone,
                    Status = AutomatedSmsStatus.Sending
                };
                db.AutomatedSmsLogs.Add(log);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException)
                {
                    db.Entry(log).State = EntityState.Detached;
                    item.Status = "Already handled";
                    continue;
                }

                if (item.SkipReason != null)
                {
                    log.Status = AutomatedSmsStatus.Skipped;
                    log.ErrorMessage = item.SkipReason;
                    item.Status = "Skipped: " + item.SkipReason;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                if (dryRun)
                {
                    log.Status = AutomatedSmsStatus.Test;
                    item.Status = "Test (not sent)";
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                // Real or simulated send is decided by AppSettings:SmsMode inside the gateway.
                var send = await gateway.SendAsync(item.Phone, item.Body);
                bool delivered = send.Success;
                if (send.Success)
                {
                    log.Status = send.Simulated ? AutomatedSmsStatus.NoTwilio : AutomatedSmsStatus.Sent;
                    log.SentAt = DateTime.UtcNow;
                }
                else
                {
                    log.Status = AutomatedSmsStatus.Failed;
                    log.ErrorMessage = Truncate(send.Error ?? "Send failed", 1000);
                }
                item.Status = log.Status;

                if (delivered)
                {
                    var sms = new SmsDetail
                    {
                        fromNum = send.From,
                        toNum = item.Phone,
                        smsBody = item.Body,
                        smsDirection = send.Simulated ? "OutgoingTest" : "Outgoing",
                        IsReadYesOrNo = "",
                        imgPath = "",
                        createdOn = DateTime.UtcNow,
                        ContactDetailId = item.ContactDetailId
                    };
                    db.SmsDetails.Add(sms);
                    await db.SaveChangesAsync(ct);
                    log.SmsDetailId = sms.Id;
                }
                await db.SaveChangesAsync(ct);
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

            var byTech = jobs.Where(j => j.TechnicianId != null).GroupBy(j => j.TechnicianId!.Value).ToList();
            var techIds = byTech.Select(g => g.Key).ToList();
            var techs = await db.Technicians.AsNoTracking().Where(t => techIds.Contains(t.Id)).ToListAsync(ct);

            var items = new List<AutomatedSmsItem>();
            foreach (var t in techs) // technicians with no jobs tomorrow are not included
            {
                var tJobs = byTech.First(g => g.Key == t.Id).ToList();
                var full = TechFullName(t);
                var lines = tJobs.Select((j, i) =>
                    $"{i + 1}) {j.StartAt:h:mm tt} - {j.Category?.keyword ?? ""}\n" +
                    $"   {(j.ContactDetail != null ? CustomerFullName(j.ContactDetail) : "")}\n" +
                    $"   {(j.ContactDetail != null ? Addr(j.ContactDetail) : "")}").ToList();

                var vars = new Dictionary<string, string>
                {
                    ["technicianFirstName"] = TechFirstName(t),
                    ["technicianName"] = full,
                    ["jobCount"] = tJobs.Count.ToString(),
                    ["jobDate"] = tomorrow.ToString("dd-MMM-yyyy"),
                    ["jobsList"] = JoinJobs(lines),
                    ["companyName"] = companyName
                };
                items.Add(MakeItem(RecipientTypes.Technician, t.Id, full, t.ContactNumber, Render(s.TemplateBody, vars), null));
            }
            return items;
        }

        private async Task<List<AutomatedSmsItem>> BuildCustomerReminderAsync(AutomatedSmsSetting s, DateTime nowLocal, CancellationToken ct)
        {
            var jobs = await TomorrowJobsAsync(nowLocal, ct);
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
                    ["companyName"] = companyName
                };
                var item = MakeItem(RecipientTypes.Customer, contact.Id, CustomerFullName(contact),
                                    contact.PhoneNumber, Render(s.TemplateBody, vars), contact.Id);

                if (item.SkipReason == null && optOut.Contains(Last10(contact.PhoneNumber)))
                    item.SkipReason = "Customer opted out";
                items.Add(item);
            }
            return items;
        }

        // ---------- helpers ----------
        /// <summary>Tomorrow's jobs that are not cancelled (Scheduled / Confirmed).</summary>
        private async Task<List<Job>> TomorrowJobsAsync(DateTime nowLocal, CancellationToken ct)
        {
            var start = nowLocal.Date.AddDays(1);
            var end = start.AddDays(1);
            return await db.Jobs.AsNoTracking()
                .Include(j => j.ContactDetail)
                .Include(j => j.Category)
                .Where(j => j.Status != JobStatus.Cancelled && j.StartAt >= start && j.StartAt < end)
                .OrderBy(j => j.StartAt)
                .ToListAsync(ct);
        }

        private static string Addr(ContactDetail c) => c.Address ?? "";

        private static string FirstWord(string full) =>
            full.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? full;

        public static string TechFullName(Technician t)
        {
            var combined = $"{t.FirstName} {t.LastName}".Trim();
            return combined.Length > 0 ? combined : (t.FullName ?? "").Trim();
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
    }
}