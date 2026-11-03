using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    public record StaffRow(int UserId, string Name, string Username, string Phone,
    int Sessions, int LoggedInSeconds, int ActiveSeconds, DateTime? LastSeenUtc, bool Online,
    int PageViews, int Actions, int ManualLogouts, int SystemLogouts,
    int Inbound, int Outbound, int Pending, int Answered, int ReplyUnknown,
    int OnTime, int Gold, int Orange, int Red, string AvgText, int TechnicianMessages);
    public record SessionRow(long Id, string LoginLocal, string? LogoutLocal, string State, int LoggedInSeconds, int ActiveSeconds, string? Ip, string? Agent);
    public record PeriodRow(string Label, int Sessions, int LoggedInSeconds, int ActiveSeconds, int PageViews, int Actions, int Inbound, int Answered);
    public record ActivityRow(string TimeLocal, string Type, string Description, string? Target, bool Success);
    public record BreakdownRow(string Description, int Count);
    public record StaffDetail(StaffRow Row, List<SessionRow> Sessions, List<PeriodRow> Periods, List<ActivityRow> Activities, List<BreakdownRow> Breakdown);


    /// <summary>
    /// Staff stats for the admin. Uses ResponseRules (same first-reply rule and buckets as the Dashboard and the grid).
    /// Received  = customer messages sent to the staff member's own Twilio# (matched by last 10 digits).
    /// Handled   = customer messages whose FIRST real reply was sent by this staff member (SmsDetails.SentByUserId).
    /// Technician messages are counted separately and never enter response times.
    /// </summary>
    public class StaffActivityService
    {
        private const string HiddenSuperUser = "Snowbro";
        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;
        private readonly TimeSpan _timeout;

        public StaffActivityService(ApplicationDbContext db, IAppClock clock, IConfiguration config)
        {
            _db = db;
            _clock = clock;
            _timeout = TimeSpan.FromMinutes(config.GetValue<int?>("StaffActivity:SessionTimeoutMinutes") ?? 20);
        }

        private record IncomingEvent(int Id, DateTime CreatedUtc, int? OwnerId, bool Shared, bool IsTechnician, int? ReplyBy, double? Minutes);

        public static string Pct(int n, int total) => total <= 0 ? "0%" : Math.Round(100.0 * n / total, 1) + "%";

        /// <summary>Sessions with no logout and no sign of life for longer than the timeout = System logout (time = last activity).</summary>
        private async Task CloseStaleSessionsAsync()
        {
            var cutoff = _clock.UtcNow - _timeout;
            await _db.UserLoginSessions
                .Where(s => s.LogoutAt == null && s.LastActivityAt < cutoff)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LogoutAt, s => s.LastActivityAt)
                    .SetProperty(s => s.LogoutType, LogoutTypes.System));
        }

        public static string Duration(int seconds)
        {
            if (seconds <= 0) return "0m";
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m";
        }

        private (DateTime FromUtc, DateTime ToUtc) UtcRange(DateTime fromDay, DateTime toDay) =>
            (_clock.ToUtc(fromDay.Date), _clock.ToUtc(toDay.Date.AddDays(1)));

        private static int Span(UserLoginSession s) =>
            (int)Math.Max(0, ((s.LogoutAt ?? s.LastActivityAt) - s.LoginAt).TotalSeconds);

        private string Local(DateTime utc) => _clock.ToBusiness(utc).ToString("dd MMM yyyy hh:mm tt");

        private async Task<List<User>> LoadStaffAsync() =>
            (await _db.User.AsNoTracking().Where(u => u.Username != HiddenSuperUser).ToListAsync())
                .Where(u => !string.IsNullOrWhiteSpace(u.Role) && AppRoles.Normalize(u.Role) == AppRoles.Staff)
                .OrderBy(u => u.Name ?? u.Username).ToList();

        private async Task<List<IncomingEvent>> LoadEventsAsync(DateTime fromUtc, DateTime toUtc, List<User> staff, HashSet<string> technicianNumbers)
        {
            var incoming = await _db.SmsDetails.AsNoTracking()
                .Where(s => s.smsDirection == "Incoming" && s.createdOn >= fromUtc && s.createdOn < toUtc)
                .Select(s => new { s.Id, s.fromNum, s.toNum, s.createdOn }).ToListAsync();
            if (incoming.Count == 0) return new List<IncomingEvent>();

            var earliest = incoming.Min(s => s.createdOn);
            var replies = await _db.SmsDetails.AsNoTracking()
                .Where(ResponseRules.IsReply)
                .Where(s => s.createdOn > earliest)
                .Select(s => new { s.toNum, s.createdOn, s.SentByUserId }).ToListAsync();
            var byNumber = replies.GroupBy(r => r.toNum ?? "")
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.createdOn).ToList());

            var owners = staff.Where(u => DashboardService.Last10(u.Phone).Length > 0)
                .GroupBy(u => DashboardService.Last10(u.Phone))
                .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToList());

            var list = new List<IncomingEvent>();
            foreach (var s in incoming)
            {
                var from = s.fromNum ?? "";
                bool tech = technicianNumbers.Contains(DashboardService.Last10(from));

                int? owner = null; bool shared = false;
                if (owners.TryGetValue(DashboardService.Last10(s.toNum), out var ids))
                {
                    if (ids.Count == 1) owner = ids[0]; else shared = true;   // two staff on one number: nobody "owns" it
                }

                int? by = null; double? minutes = null;
                if (!tech && byNumber.TryGetValue(from, out var rl))
                {
                    var hit = rl.FirstOrDefault(r => r.createdOn > s.createdOn);   // same rule as the grid / Dashboard
                    if (hit != null) { minutes = (hit.createdOn - s.createdOn).TotalMinutes; by = hit.SentByUserId; }
                }
                list.Add(new IncomingEvent(s.Id, s.createdOn, owner, shared, tech, by, minutes));
            }
            return list;
        }

        private async Task<HashSet<string>> LoadTechnicianNumbersAsync() =>
            (await _db.Technicians.AsNoTracking().Select(t => t.ContactNumber).ToListAsync())
                .Select(DashboardService.Last10).Where(n => n.Length > 0).ToHashSet();

        public async Task<List<StaffRow>> GetStaffRowsAsync(DateTime fromDay, DateTime toDay, int? onlyUserId = null)
        {
            await CloseStaleSessionsAsync();

            var (fromUtc, toUtc) = UtcRange(fromDay, toDay);
            var now = _clock.UtcNow;

            var staff = await LoadStaffAsync();
            if (staff.Count == 0) return new List<StaffRow>();
            var staffIds = staff.Select(u => u.Id).ToList();

            var tech = await LoadTechnicianNumbersAsync();
            var events = await LoadEventsAsync(fromUtc, toUtc, staff, tech);

            var sessions = await _db.UserLoginSessions.AsNoTracking()
                .Where(s => staffIds.Contains(s.UserId) && s.LoginAt >= fromUtc && s.LoginAt < toUtc).ToListAsync();

            var cutoff = now - _timeout;
            var online = (await _db.UserLoginSessions.AsNoTracking()
                .Where(s => s.LogoutAt == null && s.LastActivityAt >= cutoff).Select(s => s.UserId).Distinct().ToListAsync()).ToHashSet();

            var counts = (await _db.UserActivityLogs.AsNoTracking()
                    .Where(a => staffIds.Contains(a.UserId) && a.CreatedOn >= fromUtc && a.CreatedOn < toUtc
                                && (a.Type == ActivityTypes.PageView || a.Type == ActivityTypes.Action))
                    .GroupBy(a => new { a.UserId, a.Type })
                    .Select(g => new { g.Key.UserId, g.Key.Type, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => (x.UserId, x.Type), x => x.Count);

            var sent = (await _db.SmsDetails.AsNoTracking()
                    .Where(ResponseRules.IsReply)
                    .Where(s => s.createdOn >= fromUtc && s.createdOn < toUtc && s.SentByUserId != null)
                    .Select(s => new { s.SentByUserId, s.toNum }).ToListAsync())
                .Where(s => !tech.Contains(DashboardService.Last10(s.toNum)))
                .GroupBy(s => s.SentByUserId!.Value).ToDictionary(g => g.Key, g => g.Count());

            var rows = new List<StaffRow>();
            foreach (var u in staff.Where(u => onlyUserId == null || u.Id == onlyUserId))
            {
                var mine = sessions.Where(s => s.UserId == u.Id).ToList();
                var ownedAll = events.Where(e => e.OwnerId == u.Id).ToList();
                var owned = ownedAll.Where(e => !e.IsTechnician).ToList();
                var answered = events.Where(e => !e.IsTechnician && e.ReplyBy == u.Id).ToList();
                var mins = answered.Where(e => e.Minutes.HasValue).Select(e => e.Minutes!.Value).ToList();
                int Bucket(string b) => answered.Count(e => ResponseRules.BucketFor(e.Minutes) == b);
                counts.TryGetValue((u.Id, ActivityTypes.PageView), out var pv);
                counts.TryGetValue((u.Id, ActivityTypes.Action), out var ac);
                sent.TryGetValue(u.Id, out var sentCount);

                rows.Add(new StaffRow(u.Id, u.Name ?? u.Username, u.Username, u.Phone ?? "",
                    mine.Count, mine.Sum(Span), mine.Sum(s => s.ActiveSeconds),
                    mine.Count == 0 ? null : mine.Max(s => s.LogoutAt ?? s.LastActivityAt), online.Contains(u.Id),
                    pv, ac,
                    mine.Count(s => s.LogoutAt != null && s.LogoutType != LogoutTypes.System),
                    mine.Count(s => s.LogoutType == LogoutTypes.System),
                    owned.Count,                                                        // Inbound
                    sentCount,                                                          // Outbound
                    owned.Count(e => e.Minutes == null),                                // Pending
                    answered.Count,                                                     // Answered
                    owned.Count(e => e.Minutes != null && e.ReplyBy == null),           // replier unknown (sent before tracking)
                    Bucket(ResponseRules.OnTime), Bucket(ResponseRules.Gold),
                    Bucket(ResponseRules.Orange), Bucket(ResponseRules.Red),
                    mins.Count == 0 ? "—" : ResponseRules.FormatWaiting(mins.Average()),
                    ownedAll.Count(e => e.IsTechnician)));
            }
            return rows;
        }

        /// <summary>Ids of the Incoming SMS rows behind a number on the staff page. kind: received | pending | handled | late.</summary>
        public async Task<HashSet<int>> DrillDownIdsAsync(DateTime fromDay, DateTime toDay, int staffId, string? kind)
        {
            var (fromUtc, toUtc) = UtcRange(fromDay, toDay);
            var staff = await LoadStaffAsync();
            var tech = await LoadTechnicianNumbersAsync();
            var q = (await LoadEventsAsync(fromUtc, toUtc, staff, tech)).Where(e => !e.IsTechnician);

            q = (kind ?? "").Trim().ToLowerInvariant() switch
            {
                "pending" => q.Where(e => e.OwnerId == staffId && e.Minutes == null),
                "handled" => q.Where(e => e.ReplyBy == staffId),
                "late" => q.Where(e => e.ReplyBy == staffId && e.Minutes >= ResponseRules.GoldFromMinutes),
                // DrillDownIdsAsync: add 4 cases to the switch (keep the existing ones)
                "ontime" => q.Where(e => e.ReplyBy == staffId && ResponseRules.BucketFor(e.Minutes) == ResponseRules.OnTime),
                "gold" => q.Where(e => e.ReplyBy == staffId && ResponseRules.BucketFor(e.Minutes) == ResponseRules.Gold),
                "orange" => q.Where(e => e.ReplyBy == staffId && ResponseRules.BucketFor(e.Minutes) == ResponseRules.Orange),
                "red" => q.Where(e => e.ReplyBy == staffId && ResponseRules.BucketFor(e.Minutes) == ResponseRules.Red),
                _ => q.Where(e => e.OwnerId == staffId)
            };
            return q.Select(e => e.Id).ToHashSet();
        }

        public async Task<StaffDetail?> GetDetailAsync(int userId, DateTime fromDay, DateTime toDay, string? group = null)
        {
            var row = (await GetStaffRowsAsync(fromDay, toDay, userId)).FirstOrDefault();
            if (row == null) return null;

            var (fromUtc, toUtc) = UtcRange(fromDay, toDay);
            var now = _clock.UtcNow;
            var g = (group ?? "day").ToLowerInvariant();
            if (g != "week" && g != "month") g = "day";

            var sessions = await _db.UserLoginSessions.AsNoTracking()
                .Where(s => s.UserId == userId && s.LoginAt >= fromUtc && s.LoginAt < toUtc)
                .OrderByDescending(s => s.LoginAt).ToListAsync();
            var activity = await _db.UserActivityLogs.AsNoTracking()
                .Where(a => a.UserId == userId && a.CreatedOn >= fromUtc && a.CreatedOn < toUtc)
                .OrderByDescending(a => a.CreatedOn)
                .Select(a => new { a.CreatedOn, a.Type, a.Description, a.Target, a.Success }).ToListAsync();

            var staff = await LoadStaffAsync();
            var tech = await LoadTechnicianNumbersAsync();
            var events = (await LoadEventsAsync(fromUtc, toUtc, staff, tech)).Where(e => !e.IsTechnician).ToList();

            string State(UserLoginSession s) =>
                s.LogoutAt != null ? (s.LogoutType == LogoutTypes.System ? "System logout (timed out / browser closed)" : "Manual logout")
                : now - s.LastActivityAt <= _timeout ? "Online"
                : "System logout (timed out / browser closed)";

            var sessionRows = sessions.Select(s => new SessionRow(s.Id, Local(s.LoginAt),
                s.LogoutAt.HasValue ? Local(s.LogoutAt.Value) : null, State(s), Span(s), s.ActiveSeconds,
                s.IpAddress, s.UserAgent)).ToList();

            // Day / Week (Monday) / Month grouping
            (DateTime Key, string Label) PeriodOf(DateTime d)
            {
                d = d.Date;
                if (g == "month") return (new DateTime(d.Year, d.Month, 1), d.ToString("MMM yyyy"));
                if (g == "week") { var ws = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); return (ws, "Week of " + ws.ToString("dd MMM yyyy")); }
                return (d, d.ToString("ddd dd MMM yyyy"));
            }
            var labels = new Dictionary<DateTime, string>();
            var periods = new SortedDictionary<DateTime, int[]>(Comparer<DateTime>.Create((a, b) => b.CompareTo(a)));
            int[] Slot(DateTime businessDay)
            {
                var p = PeriodOf(businessDay);
                labels[p.Key] = p.Label;
                if (!periods.TryGetValue(p.Key, out var v)) periods[p.Key] = v = new int[7];
                return v;
            }
            foreach (var s in sessions) { var v = Slot(_clock.ToBusiness(s.LoginAt)); v[0]++; v[1] += Span(s); v[2] += s.ActiveSeconds; }
            foreach (var a in activity)
            {
                var v = Slot(_clock.ToBusiness(a.CreatedOn));
                if (a.Type == ActivityTypes.PageView) v[3]++; else if (a.Type == ActivityTypes.Action) v[4]++;
            }
            foreach (var e in events.Where(e => e.OwnerId == userId || e.ReplyBy == userId))
            {
                var v = Slot(_clock.ToBusiness(e.CreatedUtc));
                if (e.OwnerId == userId) v[5]++;
                if (e.ReplyBy == userId) v[6]++;
            }
            var periodRows = periods.Select(p => new PeriodRow(labels[p.Key], p.Value[0], p.Value[1], p.Value[2], p.Value[3], p.Value[4], p.Value[5], p.Value[6])).ToList();

            var breakdown = activity.Where(a => a.Type == ActivityTypes.Action)
                .GroupBy(a => a.Description).Select(x => new BreakdownRow(x.Key, x.Count()))
                .OrderByDescending(b => b.Count).ToList();

            var recent = activity.Take(300).Select(a => new ActivityRow(
                _clock.ToBusiness(a.CreatedOn).ToString("dd MMM hh:mm:ss tt"), a.Type, a.Description, a.Target, a.Success)).ToList();

            return new StaffDetail(row, sessionRows, periodRows, recent, breakdown);
        }
    }

    /// <summary>Daily Hangfire job: deletes tracking rows older than StaffActivity:RetentionMonths (default 12).</summary>
    public class ActivityRetentionJob
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        public ActivityRetentionJob(ApplicationDbContext db, IConfiguration config) { _db = db; _config = config; }

        public async Task<int> RunAsync()
        {
            var months = Math.Max(1, _config.GetValue<int?>("StaffActivity:RetentionMonths") ?? 12);
            var cutoff = DateTime.UtcNow.AddMonths(-months);
            var logs = await _db.UserActivityLogs.Where(a => a.CreatedOn < cutoff).ExecuteDeleteAsync();
            var sessions = await _db.UserLoginSessions.Where(s => s.LoginAt < cutoff).ExecuteDeleteAsync();
            return logs + sessions;
        }
    }
}