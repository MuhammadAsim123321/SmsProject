using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System.Text.RegularExpressions;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Technician replies like "C 123", "C123", "C 1,2,3", "c 1 3", "C1", "confirm 1 2".
    /// Numbers are positions in the latest daily SMS (TechnicianJobLists).
    /// Returns true only when the sender is a technician AND the message is a confirm command,
    /// so every other message keeps its normal handling.
    /// </summary>
    public class TechnicianReplyService
    {
        // NOTE: replace with an existing SmsLogSources constant if you have a suitable one.
        private const string LogSource = "Technician Reply";

        private static readonly Regex ConfirmRx = new(
            @"^\s*(?:confirm(?:ed)?|c)\s*(?<rest>[\d\s,;.&+\-]*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ApplicationDbContext db;
        private readonly ISmsGateway gateway;
        private readonly IAppClock clock;
        private readonly SmsLogService smsLog;
        private readonly ILogger<TechnicianReplyService> logger;
        private readonly string officePhone;

        public TechnicianReplyService(ApplicationDbContext db, ISmsGateway gateway, IAppClock clock,
            SmsLogService smsLog, IConfiguration config, ILogger<TechnicianReplyService> logger)
        {
            this.db = db; this.gateway = gateway; this.clock = clock; this.smsLog = smsLog; this.logger = logger;
            var office = (config["AutomatedSms:OfficePhone"] ?? "").Trim();
            officePhone = office.Length > 0 ? office : "the office";
        }

        public async Task<bool> TryHandleAsync(SmsDetail sms, CancellationToken ct = default)
        {
            var body = Regex.Replace((sms.smsBody ?? "").Trim(), @"\s+and\s+", ",", RegexOptions.IgnoreCase);
            var m = ConfirmRx.Match(body);
            if (!m.Success) return false;

            var from = gateway.NormalizePhone(sms.fromNum);
            if (from.Length == 0) return false;

            var techs = await db.Technicians.AsNoTracking().ToListAsync(ct);
            var tech = techs.FirstOrDefault(t => gateway.NormalizePhone(t.ContactNumber) == from);
            if (tech == null) return false;   // not a technician: customer logic continues

            var today = clock.BusinessNow.Date;
            var list = await db.TechnicianJobLists.Include(l => l.Job)
                .Where(l => l.TechnicianId == tech.Id && l.JobDate >= today)
                .OrderBy(l => l.Position).ToListAsync(ct);

            string reply;
            if (list.Count == 0)
            {
                reply = $"No job list to confirm right now. Please call {officePhone}.";
            }
            else
            {
                var (positions, invalid) = ParseNumbers(m.Groups["rest"].Value, list.Count);
                if (positions.Count == 0 && invalid.Count == 0)
                {
                    reply = "Please include the job numbers, e.g. 'C 123' or 'C 1 3'.";
                }
                else
                {
                    var confirmed = new List<int>();
                    var problems = new List<string>();

                    foreach (var pos in positions.OrderBy(x => x))
                    {
                        var row = list.FirstOrDefault(l => l.Position == pos);
                        var job = row?.Job;
                        if (row == null || job == null) problems.Add($"{pos} (not found)");
                        else if (job.Status == JobStatus.Cancelled) problems.Add($"{pos} (cancelled)");
                        else if (job.TechnicianId != tech.Id || job.StartAt != row.JobStartAt) problems.Add($"{pos} (changed)");
                        else if (job.Status == JobStatus.Confirmed) confirmed.Add(pos);
                        else if (job.Status == JobStatus.Scheduled)
                        {
                            job.Status = JobStatus.Confirmed;
                            job.UpdatedOn = clock.UtcNow;
                            confirmed.Add(pos);
                        }
                        else problems.Add($"{pos} (not available)");
                    }
                    await db.SaveChangesAsync(ct);

                    var parts = new List<string>();
                    if (confirmed.Count > 0) parts.Add($"Confirmed: {string.Join(", ", confirmed)}.");
                    if (problems.Count > 0) parts.Add($"Could not confirm: {string.Join(", ", problems)}. Please call {officePhone}.");
                    if (invalid.Count > 0) parts.Add($"Not recognised: {string.Join(", ", invalid)}. Job numbers are 1-{list.Count}.");
                    reply = string.Join(" ", parts);
                }
            }

            try
            {
                var send = await gateway.SendAsync(tech.ContactNumber, reply);
                await smsLog.RecordImmediateAsync(LogSource, from, reply, send.Success, send.Simulated, send.Error,
                    recipientName: AutomatedSmsService.TechFullName(tech), recipientType: RecipientTypes.Technician,
                    recipientId: tech.Id, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Technician reply SMS failed for technician {Id}", tech.Id);
            }
            return true;
        }

        /// <summary>
        /// "123" -> 1,2,3 | "1,2,3" | "1 3" | "12" -> job 12 only when the list has 12+ jobs, otherwise 1 and 2.
        /// A token that cannot be read is returned in 'bad'.
        /// </summary>
        private static (List<int> ok, List<string> bad) ParseNumbers(string rest, int count)
        {
            var ok = new List<int>();
            var bad = new List<string>();
            void Add(int n) { if (!ok.Contains(n)) ok.Add(n); }

            foreach (Match t in Regex.Matches(rest, @"\d+"))
            {
                var tok = t.Value;
                if (int.TryParse(tok, out var whole) && whole >= 1 && whole <= count) { Add(whole); continue; }
                var digits = tok.Select(ch => ch - '0').ToList();
                if (digits.All(d => d >= 1 && d <= count)) digits.ForEach(Add);
                else bad.Add(tok);
            }
            return (ok, bad);
        }
    }
}