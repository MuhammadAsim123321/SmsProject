using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// ONE place that defines "was this customer message answered, and how fast".
    /// Used by the Dashboard AND the Incoming SMS grid, so the numbers can never disagree.
    /// </summary>
    public static class ResponseRules
    {
        // Waiting-time thresholds (minutes) - same as the grid's gold / orange / red text colours.
        public const double GoldFromMinutes = 10;
        public const double OrangeFromMinutes = 15;
        public const double RedFromMinutes = 20;

        public const string OnTime = "ontime";   // answered in under 10 min (no colour in the grid)
        public const string Gold = "gold";       // 10 - 14m59s
        public const string Orange = "orange";   // 15 - 19m59s
        public const string Red = "red";         // 20m+
        public const string Pending = "pending"; // no reply yet

        /// <summary>
        /// A "real reply" = a successfully sent manual message.
        /// NOT: OutgoingTest, OutgoingFailed, or a message sent by a job plan (ScheduledJobId set).
        /// Welcome / IVR option messages are only sent through the gateway (never saved to SmsDetails), so they never match.
        /// </summary>
        public static readonly Expression<Func<SmsDetail, bool>> IsReply =
            s => s.smsDirection == "Outgoing" && s.ScheduledJobId == null;

        public static string BucketFor(double? minutes)
        {
            if (minutes == null) return Pending;
            if (minutes >= RedFromMinutes) return Red;
            if (minutes >= OrangeFromMinutes) return Orange;
            if (minutes >= GoldFromMinutes) return Gold;
            return OnTime;
        }

        /// <summary>Grid text colour: only gold / orange / red are coloured.</summary>
        public static string ColorFor(double? minutes)
        {
            var bucket = BucketFor(minutes);
            return bucket is Gold or Orange or Red ? bucket : "";
        }

        public static string FormatWaiting(double totalMinutes)
        {
            var t = TimeSpan.FromMinutes(totalMinutes);
            return $"{t.Hours}h:{t.Minutes:00}m:{t.Seconds:00}s";
        }
    }

    public record DashboardRow(int Id, string From, string Body, DateTime CreatedUtc, DateTime? RespondedUtc, double? Minutes, string Bucket);
    public record SeriesPoint(string Label, string Key, int Responded, int Pending);
    public record AttentionRow(int Id, string Customer, string Phone, string Received, string Body, string Waiting, string Bucket);

    public record DashboardSummary(
        string From, string To, string TimeZone, string Granularity,
        int Total, int Responded, int Pending,
        string AvgText, string MedianText, double TimelyPercent,
        Dictionary<string, int> Buckets,
        List<SeriesPoint> Series,
        List<AttentionRow> Attention);

    public class DashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;

        public DashboardService(ApplicationDbContext db, IAppClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public static bool TryParseDay(string? s, out DateTime day) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

        /// <summary>Technician numbers are compared by their last 10 digits (stored formats differ).</summary>
        public static string Last10(string? phone)
        {
            var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
            return digits.Length > 10 ? digits[^10..] : digits;
        }

        /// <summary>Business days (inclusive) -> UTC range [fromUtc, toUtc).</summary>
        private (DateTime FromUtc, DateTime ToUtc) UtcRange(DateTime fromDay, DateTime toDay) =>
            (_clock.ToUtc(fromDay.Date), _clock.ToUtc(toDay.Date.AddDays(1)));

        // =====================================================================
        // Core: customer messages in range + their first real reply
        // =====================================================================
        public async Task<List<DashboardRow>> LoadRowsAsync(DateTime fromDay, DateTime toDay)
        {
            var (fromUtc, toUtc) = UtcRange(fromDay, toDay);

            // Technician messages ("C 123" confirmations) are not customer messages.
            var technicianNumbers = (await _db.Technicians.AsNoTracking().Select(t => t.ContactNumber).ToListAsync())
                .Select(Last10).Where(n => n.Length > 0).ToHashSet();

            var incoming = (await _db.SmsDetails.AsNoTracking()
                    .Where(s => s.smsDirection == "Incoming" && s.createdOn >= fromUtc && s.createdOn < toUtc)
                    .Select(s => new { s.Id, s.fromNum, s.smsBody, s.createdOn })
                    .ToListAsync())
                .Where(s => !technicianNumbers.Contains(Last10(s.fromNum)))
                .ToList();

            if (incoming.Count == 0) return new List<DashboardRow>();

            // Replies can arrive after the range ends, so there is no upper bound here.
            var earliest = incoming.Min(s => s.createdOn);
            var replies = await _db.SmsDetails.AsNoTracking()
                .Where(ResponseRules.IsReply)
                .Where(s => s.createdOn > earliest)
                .Select(s => new { s.toNum, s.createdOn })
                .ToListAsync();

            var repliesByNumber = replies
                .GroupBy(r => r.toNum ?? "")
                .ToDictionary(g => g.Key, g => g.Select(r => r.createdOn).OrderBy(d => d).ToList());

            return incoming
                .OrderByDescending(s => s.createdOn)
                .Select(s =>
                {
                    DateTime? repliedAt = null;
                    double? minutes = null;

                    // same rule as the grid: first reply to the same number after the incoming message
                    if (repliesByNumber.TryGetValue(s.fromNum ?? "", out var list))
                    {
                        var hit = list.FirstOrDefault(d => d > s.createdOn);
                        if (hit != default)
                        {
                            repliedAt = hit;
                            minutes = (hit - s.createdOn).TotalMinutes;
                        }
                    }

                    return new DashboardRow(s.Id, s.fromNum ?? "", s.smsBody ?? "", s.createdOn,
                        repliedAt, minutes, ResponseRules.BucketFor(minutes));
                })
                .ToList();
        }

        // =====================================================================
        // Dashboard JSON
        // =====================================================================
        public async Task<DashboardSummary> GetSummaryAsync(DateTime fromDay, DateTime toDay)
        {
            var rows = await LoadRowsAsync(fromDay, toDay);

            int total = rows.Count;
            var answered = rows.Where(r => r.Minutes.HasValue).Select(r => r.Minutes!.Value).OrderBy(m => m).ToList();
            int responded = answered.Count;
            int pending = total - responded;

            double? avg = responded == 0 ? null : answered.Average();
            double? median = responded == 0 ? null
                : responded % 2 == 1 ? answered[responded / 2]
                : (answered[responded / 2 - 1] + answered[responded / 2]) / 2.0;

            var buckets = new Dictionary<string, int>
            {
                [ResponseRules.OnTime] = rows.Count(r => r.Bucket == ResponseRules.OnTime),
                [ResponseRules.Gold] = rows.Count(r => r.Bucket == ResponseRules.Gold),
                [ResponseRules.Orange] = rows.Count(r => r.Bucket == ResponseRules.Orange),
                [ResponseRules.Red] = rows.Count(r => r.Bucket == ResponseRules.Red),
                [ResponseRules.Pending] = pending
            };

            double timely = total == 0 ? 0 : Math.Round(100.0 * buckets[ResponseRules.OnTime] / total, 1);

            // chart series: per hour for a single day, per day for a range
            bool byHour = fromDay.Date == toDay.Date;
            var series = new List<SeriesPoint>();
            if (byHour)
            {
                var groups = rows.GroupBy(r => _clock.ToBusiness(r.CreatedUtc).Hour).ToDictionary(g => g.Key, g => g.ToList());
                for (int h = 0; h < 24; h++)
                {
                    groups.TryGetValue(h, out var g);
                    series.Add(new SeriesPoint($"{h:00}:00", h.ToString(),
                        g?.Count(r => r.Minutes.HasValue) ?? 0, g?.Count(r => !r.Minutes.HasValue) ?? 0));
                }
            }
            else
            {
                var groups = rows.GroupBy(r => _clock.ToBusiness(r.CreatedUtc).Date).ToDictionary(g => g.Key, g => g.ToList());
                for (var d = fromDay.Date; d <= toDay.Date; d = d.AddDays(1))
                {
                    groups.TryGetValue(d, out var g);
                    series.Add(new SeriesPoint(d.ToString("dd MMM", CultureInfo.InvariantCulture), d.ToString("yyyy-MM-dd"),
                        g?.Count(r => r.Minutes.HasValue) ?? 0, g?.Count(r => !r.Minutes.HasValue) ?? 0));
                }
            }

            return new DashboardSummary(
                fromDay.ToString("yyyy-MM-dd"), toDay.ToString("yyyy-MM-dd"), _clock.TimeZone.Id,
                byHour ? "hour" : "day",
                total, responded, pending,
                avg.HasValue ? ResponseRules.FormatWaiting(avg.Value) : "—",
                median.HasValue ? ResponseRules.FormatWaiting(median.Value) : "—",
                timely, buckets, series, await BuildAttentionAsync(rows));
        }

        /// <summary>All messages that need a look: oldest pending first, then slowest replies. Paged on the dashboard.</summary>
        private async Task<List<AttentionRow>> BuildAttentionAsync(List<DashboardRow> rows)
        {
            var pending = rows.Where(r => r.Bucket == ResponseRules.Pending).OrderBy(r => r.CreatedUtc);
            var slow = rows.Where(r => r.Minutes >= ResponseRules.GoldFromMinutes).OrderByDescending(r => r.Minutes);
            var top = pending.Concat(slow).ToList();
            if (top.Count == 0) return new List<AttentionRow>();

            var phones = top.Select(r => r.From).Distinct().ToList();
            var names = await _db.ContactDetail.AsNoTracking()
                .Where(c => c.PhoneNumber != null && phones.Contains(c.PhoneNumber))
                .ToDictionaryAsync(c => c.PhoneNumber!, c => c.Name);

            return top.Select(r => new AttentionRow(
                r.Id,
                names.TryGetValue(r.From, out var n) && !string.IsNullOrWhiteSpace(n) ? n : r.From,
                r.From,
                _clock.ToBusiness(r.CreatedUtc).ToString("dd MMM, hh:mm tt", CultureInfo.InvariantCulture),
                r.Body.Length > 90 ? r.Body[..90] + "…" : r.Body,
                r.Minutes.HasValue ? ResponseRules.FormatWaiting(r.Minutes.Value) : "Pending",
                r.Bucket)).ToList();
        }

        // =====================================================================
        // Drill-down: ids of the Incoming SMS rows behind a card / bar
        // =====================================================================
        public async Task<HashSet<int>> DrillDownIdsAsync(DateTime fromDay, DateTime toDay, string? bucket, int? hour)
        {
            IEnumerable<DashboardRow> q = await LoadRowsAsync(fromDay, toDay);

            if (hour.HasValue)
                q = q.Where(r => _clock.ToBusiness(r.CreatedUtc).Hour == hour.Value);

            var b = (bucket ?? "").Trim().ToLowerInvariant();
            q = b switch
            {
                "responded" => q.Where(r => r.Minutes.HasValue),
                ResponseRules.OnTime or ResponseRules.Gold or ResponseRules.Orange
                    or ResponseRules.Red or ResponseRules.Pending => q.Where(r => r.Bucket == b),
                _ => q
            };

            return q.Select(r => r.Id).ToHashSet();
        }
    }
}