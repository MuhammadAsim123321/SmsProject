using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System.Text.RegularExpressions;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Understands a technician's reply to the daily job list: "C 123", "C123", "c 1,2,3", "C1", "confirm 1 and 3", "C 1-3".
    /// Numbers are positions in the latest list sent to that technician (TechnicianJobLists).
    /// Valid jobs become Job.Status = Confirmed (same status the modal uses). The technician gets a reply SMS.
    /// Returns false when the message is not a technician confirm command, so the normal customer flow continues.
    /// </summary>
    public class TechnicianReplyService
    {
        private static readonly Regex CommandRegex = new(
            @"^\s*(?:confirm(?:ed)?|c)(?![a-z])\s*[:.\-]?\s*(?<rest>.*?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly ApplicationDbContext db;
        private readonly ISmsGateway gateway;
        private readonly IAppClock clock;
        private readonly SmsLogService smsLog;
        private readonly ILogger<TechnicianReplyService> logger;
        private readonly string officePhone;

        public TechnicianReplyService(ApplicationDbContext db, ISmsGateway gateway, IAppClock clock,
            SmsLogService smsLog, IConfiguration config, ILogger<TechnicianReplyService> logger)
        {
            this.db = db;
            this.gateway = gateway;
            this.clock = clock;
            this.smsLog = smsLog;
            this.logger = logger;
            officePhone = (config["AutomatedSms:OfficePhone"] ?? "").Trim();
        }

        public async Task<bool> TryHandleAsync(SmsDetail sms, CancellationToken ct = default)
        {
            if (!LooksLikeCommand(sms.smsBody, out var rest)) return false;
            var from10 = Last10(sms.fromNum);
            if (from10.Length < 10) return false;

            bool handled = false;
            try
            {
                var techs = await db.Technicians.AsNoTracking()
                    .Select(t => new { t.Id, t.FirstName, t.LastName, t.ContactNumber })
                    .ToListAsync(ct);
                var tech = techs.FirstOrDefault(t => Last10(t.ContactNumber) == from10);
                if (tech == null) return false;          // not a technician: customer flow continues
                handled = true;

                var reply = await BuildReplyAsync(tech.Id, rest, ct);
                var name = $"{tech.FirstName} {tech.LastName}".Trim();

                var send = await gateway.SendAsync(sms.fromNum, reply, fallbackFrom: sms.toNum);
                await smsLog.RecordImmediateAsync(SmsLogSources.Automated, sms.fromNum, reply,
                    send.Success, send.Simulated, send.Error,
                    recipientName: name, recipientType: RecipientTypes.Technician, recipientId: tech.Id, ct: ct);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Technician reply handling failed for {From}", sms.fromNum);
                return handled;
            }
        }

        private async Task<string> BuildReplyAsync(int techId, string rest, CancellationToken ct)
        {
            var now = clock.BusinessNow;
            var rows = await db.TechnicianJobLists
                .Where(l => l.TechnicianId == techId && l.JobDate >= now.Date)
                .OrderBy(l => l.Position)
                .ToListAsync(ct);

            if (rows.Count == 0)
                return "There is no job list waiting for your confirmation." + CallOffice();

            var numbers = ExpandNumbers(rest, rows.Count);
            if (numbers.Count == 0)
            {
                var all = rows.Count <= 9 ? string.Concat(Enumerable.Range(1, rows.Count)) : "1,2,3";
                return $"Please include the job numbers. Example: 'C {all}'.";
            }

            var jobIds = rows.Select(r => r.JobId).ToList();
            var jobs = await db.Jobs.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);

            var confirmed = new List<int>();
            var invalid = new List<int>();
            var notes = new List<string>();

            foreach (var n in numbers)
            {
                var row = rows.FirstOrDefault(r => r.Position == n);
                if (row == null) { invalid.Add(n); continue; }
                if (!jobs.TryGetValue(row.JobId, out var job)) { notes.Add($"Job {n} no longer exists."); continue; }
                if (job.Status == JobStatus.Cancelled) { notes.Add($"Job {n} was cancelled."); continue; }
                if (job.TechnicianId != techId || job.StartAt != row.JobStartAt)
                { notes.Add($"Job {n} was changed after the list was sent."); continue; }
                if (job.StartAt < now) { notes.Add($"Job {n} has already started."); continue; }
                if (job.Status == JobStatus.Confirmed) { confirmed.Add(n); continue; }

                job.Status = JobStatus.Confirmed;
                job.UpdatedOn = clock.UtcNow;
                confirmed.Add(n);
                // LATER: if today's customer reminder already ran, queue this customer's reminder here (late confirmation).
            }
            await db.SaveChangesAsync(ct);

            var parts = new List<string>();
            if (confirmed.Count > 0)
                parts.Add($"Thank you. {(confirmed.Count > 1 ? "Jobs" : "Job")} {string.Join(", ", confirmed)} confirmed.");
            else
                parts.Add("Nothing was confirmed.");
            if (invalid.Count > 0) parts.Add($"Not in your list: {string.Join(", ", invalid)}.");
            parts.AddRange(notes);
            if (invalid.Count > 0 || notes.Count > 0 || confirmed.Count == 0)
            {
                var call = CallOffice();
                if (call.Length > 0) parts.Add(call.Trim());
            }
            return string.Join(" ", parts);
        }

        private string CallOffice() => officePhone.Length > 0 ? $" Questions? Call {officePhone}." : "";

        // ------------------------------------------------------------ parsing
        private static bool LooksLikeCommand(string? body, out string rest)
        {
            rest = "";
            var m = CommandRegex.Match(body ?? "");
            if (!m.Success) return false;

            var r = m.Groups["rest"].Value.ToLowerInvariant();
            r = Regex.Replace(r, @"\band\b", ",");
            r = Regex.Replace(r, @"([0-9]+)\s*-\s*([0-9]+)", ExpandRange);
            if (!Regex.IsMatch(r, @"^[0-9\s,;&+/]*$")) return false;   // e.g. "C 1 thanks" is not a command
            rest = r;
            return true;
        }

        private static string ExpandRange(Match m)
        {
            if (!int.TryParse(m.Groups[1].Value, out var a) || !int.TryParse(m.Groups[2].Value, out var b)
                || a < 1 || b < a || b - a > 20) return "0";
            return string.Join(",", Enumerable.Range(a, b - a + 1));
        }

        /// <summary>
        /// "123" = jobs 1,2,3. With 10 or more jobs a separated token such as "12" means job 12.
        /// </summary>
        private static List<int> ExpandNumbers(string rest, int listCount)
        {
            var result = new List<int>();
            foreach (var token in Regex.Split(rest, @"[\s,;&+/]+").Where(t => t.Length > 0))
            {
                IEnumerable<int> values;
                if (token.Length == 1) values = new[] { token[0] - '0' };
                else if (listCount >= 10 && int.TryParse(token, out var whole) && whole >= 1 && whole <= listCount)
                    values = new[] { whole };
                else values = token.Select(ch => ch - '0');

                foreach (var v in values) if (!result.Contains(v)) result.Add(v);
            }
            return result.Take(30).ToList();
        }

        private static string Last10(string? s)
        {
            var d = new string((s ?? "").Where(char.IsDigit).ToArray());
            return d.Length > 10 ? d.Substring(d.Length - 10) : d;
        }
    }
}