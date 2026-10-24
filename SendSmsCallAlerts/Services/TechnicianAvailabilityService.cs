//using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    /// <summary>Field = key of the form field the message belongs to (jobStartAt / jobEndAt).</summary>
    public record AvailabilityCheckResult(bool Allowed, bool Configured, string Message, string? Field = null);

    public class TechnicianAvailabilityService
    {
        private readonly ApplicationDbContext _context;

        public TechnicianAvailabilityService(ApplicationDbContext context)
        {
            _context = context;
        }

        // UI order: Monday first, Sunday last
        public static readonly int[] DisplayOrder = { 1, 2, 3, 4, 5, 6, 0 };
        public static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        /// <summary>Default week: Mon-Fri 09:00-17:00, Sat/Sun off.</summary>
        public static List<TechnicianAvailability> DefaultWeek()
        {
            var list = new List<TechnicianAvailability>();
            for (int d = 0; d < 7; d++)
            {
                bool weekday = d >= 1 && d <= 5;
                list.Add(new TechnicianAvailability
                {
                    DayOfWeek = d,
                    IsAvailable = weekday,
                    StartTime = weekday ? new TimeSpan(9, 0, 0) : null,
                    EndTime = weekday ? new TimeSpan(17, 0, 0) : null
                });
            }
            return list;
        }

        /// <summary>Always returns exactly 7 rows (0..6) even if the browser posted fewer/duplicates.</summary>
        public static List<TechnicianAvailability> Normalize(IEnumerable<TechnicianAvailability> posted)
        {
            var src = posted?.ToList() ?? new List<TechnicianAvailability>();
            var result = new List<TechnicianAvailability>();
            for (int d = 0; d < 7; d++)
            {
                var row = src.FirstOrDefault(r => r.DayOfWeek == d);
                bool on = row != null && row.IsAvailable;
                result.Add(new TechnicianAvailability
                {
                    DayOfWeek = d,
                    IsAvailable = on,
                    StartTime = on ? row!.StartTime : null,
                    EndTime = on ? row!.EndTime : null
                });
            }
            return result;
        }

        /// <summary>Returns an error message, or null when valid.</summary>
        public static string? Validate(List<TechnicianAvailability> week)
        {
            if (!week.Any(w => w.IsAvailable))
                return "Select at least one available day.";

            foreach (var w in week.Where(w => w.IsAvailable))
            {
                var day = DayNames[w.DayOfWeek];
                if (w.StartTime == null || w.EndTime == null)
                    return $"{day}: start and end time are required.";
                if (w.StartTime >= w.EndTime)
                    return $"{day}: start time must be earlier than end time.";
            }
            return null;
        }

        /// <summary>e.g. "Mon-Fri 09:00-17:00; Sat 09:00-13:00"</summary>
        public static string BuildSummary(IEnumerable<TechnicianAvailability>? rows)
        {
            var list = rows?.ToList() ?? new List<TechnicianAvailability>();
            var ordered = DisplayOrder.Select(d => list.FirstOrDefault(r => r.DayOfWeek == d)).ToList();
            var parts = new List<string>();

            int i = 0;
            while (i < ordered.Count)
            {
                var r = ordered[i];
                if (r == null || !r.IsAvailable || r.StartTime == null || r.EndTime == null) { i++; continue; }

                int j = i;
                while (j + 1 < ordered.Count
                       && ordered[j + 1] != null
                       && ordered[j + 1]!.IsAvailable
                       && ordered[j + 1]!.StartTime == r.StartTime
                       && ordered[j + 1]!.EndTime == r.EndTime)
                    j++;

                var label = i == j
                    ? DayNames[r.DayOfWeek]
                    : $"{DayNames[r.DayOfWeek]}-{DayNames[ordered[j]!.DayOfWeek]}";
                parts.Add($"{label} {Fmt(r.StartTime)}-{Fmt(r.EndTime)}");
                i = j + 1;
            }

            return parts.Count == 0 ? "Not configured" : string.Join("; ", parts);
        }

        /// <summary>Is the technician available at this local business date/time? (start time only)</summary>
        public static AvailabilityCheckResult Check(IEnumerable<TechnicianAvailability>? rows, DateTime when)
        {
            var list = rows?.ToList() ?? new List<TechnicianAvailability>();
            if (list.Count == 0)
                return new AvailabilityCheckResult(true, false, "No availability configured for this technician.");

            var row = list.FirstOrDefault(r => r.DayOfWeek == (int)when.DayOfWeek);
            if (row == null || !row.IsAvailable || row.StartTime == null || row.EndTime == null)
                return new AvailabilityCheckResult(false, true, $"Technician is not available on {when:dddd}.", "jobStartAt");

            var t = when.TimeOfDay;
            if (t < row.StartTime || t >= row.EndTime)
                return new AvailabilityCheckResult(false, true,
                    $"Technician works {Fmt(row.StartTime)}-{Fmt(row.EndTime)} on {when:dddd}.", "jobStartAt");

            return new AvailabilityCheckResult(true, true, "Available.");
        }

        public async Task<AvailabilityCheckResult> CheckAsync(int technicianId, DateTime when)
        {
            var rows = await _context.TechnicianAvailabilities
                .AsNoTracking()
                .Where(a => a.TechnicianId == technicianId)
                .ToListAsync();
            return Check(rows, when);
        }

        /// <summary>
        /// Working-hours part of a slot check (no database): end after start, same day,
        /// start inside hours, end not after closing time.
        /// </summary>
        public static AvailabilityCheckResult CheckSlotHours(IEnumerable<TechnicianAvailability>? rows, DateTime start, DateTime end)
        {
            if (end <= start)
                return new AvailabilityCheckResult(false, true, "End time must be after the start time.", "jobEndAt");
            if (start.Date != end.Date)
                return new AvailabilityCheckResult(false, true, "Job must start and end on the same day.", "jobEndAt");

            var list = rows?.ToList() ?? new List<TechnicianAvailability>();

            var startCheck = Check(list, start);
            if (!startCheck.Allowed) return startCheck;

            var row = list.FirstOrDefault(r => r.DayOfWeek == (int)start.DayOfWeek);
            if (row != null && row.IsAvailable && row.EndTime.HasValue && end.TimeOfDay > row.EndTime.Value)
            {
                return new AvailabilityCheckResult(false, true,
                    $"Job ends at {Fmt(end.TimeOfDay)}, after the technician's working hours end ({Fmt(row.EndTime)}).", "jobEndAt");
            }

            return new AvailabilityCheckResult(true, startCheck.Configured, "Available.");
        }

        /// <summary>
        /// Full slot check for one technician: working hours + no overlap with another
        /// non-cancelled job. excludeJobId = the job being edited (so it never clashes with itself).
        /// </summary>
        public async Task<AvailabilityCheckResult> CheckJobSlotAsync(int technicianId, DateTime start, DateTime end, int? excludeJobId = null)
        {
            var rows = await _context.TechnicianAvailabilities
                .AsNoTracking()
                .Where(a => a.TechnicianId == technicianId)
                .ToListAsync();

            var hours = CheckSlotHours(rows, start, end);
            if (!hours.Allowed) return hours;

            int exclude = excludeJobId ?? 0;
            var clash = await _context.Jobs
                .AsNoTracking()
                .Where(j => j.TechnicianId == technicianId
                            && j.Status != JobStatus.Cancelled
                            && j.Id != exclude
                            && j.StartAt < end
                            && start < j.EndAt)
                .OrderBy(j => j.StartAt)
                .FirstOrDefaultAsync();

            if (clash != null)
            {
                return new AvailabilityCheckResult(false, true,
                    $"Technician is already booked {clash.StartAt:dd-MMM HH:mm} to {clash.EndAt:HH:mm} (job #{clash.Id}).", "jobStartAt");
            }

            return new AvailabilityCheckResult(true, hours.Configured, "Available.");
        }

        /// <summary>
        /// Availability of many technicians at once (used by the modal's technician dropdown).
        /// Technicians must be loaded with their Availabilities. end == null -> start-time check only.
        /// </summary>
        public async Task<Dictionary<int, AvailabilityCheckResult>> CheckManyAsync(
            IEnumerable<Technician> technicians, DateTime start, DateTime? end, int? excludeJobId)
        {
            var techs = technicians.ToList();
            var result = new Dictionary<int, AvailabilityCheckResult>();
            bool hasEnd = end.HasValue && end.Value > start;

            var booked = new Dictionary<int, List<Job>>();
            if (hasEnd)
            {
                var ids = techs.Select(t => t.Id).ToList();
                int exclude = excludeJobId ?? 0;
                var endValue = end!.Value;
                var jobs = await _context.Jobs.AsNoTracking()
                    .Where(j => j.TechnicianId != null
                                && ids.Contains(j.TechnicianId.Value)
                                && j.Status != JobStatus.Cancelled
                                && j.Id != exclude
                                && j.StartAt < endValue
                                && start < j.EndAt)
                    .ToListAsync();
                booked = jobs.GroupBy(j => j.TechnicianId!.Value).ToDictionary(g => g.Key, g => g.ToList());
            }

            foreach (var t in techs)
            {
                AvailabilityCheckResult r;
                if (hasEnd)
                {
                    r = CheckSlotHours(t.Availabilities, start, end!.Value);
                    if (r.Allowed && booked.TryGetValue(t.Id, out var clashes))
                    {
                        var c = clashes.OrderBy(x => x.StartAt).First();
                        r = new AvailabilityCheckResult(false, true,
                            $"Technician is already booked {c.StartAt:dd-MMM HH:mm} to {c.EndAt:HH:mm} (job #{c.Id}).", "jobStartAt");
                    }
                }
                else
                {
                    r = Check(t.Availabilities, start);
                }
                result[t.Id] = r;
            }
            return result;
        }

        private static string Fmt(TimeSpan? t) => t.HasValue ? t.Value.ToString(@"hh\:mm") : "--:--";
    }
}