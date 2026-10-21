using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    public record AvailabilityCheckResult(bool Allowed, bool Configured, string Message);

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
                    StartTime = on ? row.StartTime : null,
                    EndTime = on ? row.EndTime : null
                });
            }
            return result;
        }

        /// <summary>Returns an error message, or null when valid.</summary>
        public static string Validate(List<TechnicianAvailability> week)
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
        public static string BuildSummary(IEnumerable<TechnicianAvailability> rows)
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
                       && ordered[j + 1].IsAvailable
                       && ordered[j + 1].StartTime == r.StartTime
                       && ordered[j + 1].EndTime == r.EndTime)
                    j++;

                var label = i == j
                    ? DayNames[r.DayOfWeek]
                    : $"{DayNames[r.DayOfWeek]}-{DayNames[ordered[j].DayOfWeek]}";
                parts.Add($"{label} {Fmt(r.StartTime)}-{Fmt(r.EndTime)}");
                i = j + 1;
            }

            return parts.Count == 0 ? "Not configured" : string.Join("; ", parts);
        }

        /// <summary>Is the technician available at this local business date/time?</summary>
        public static AvailabilityCheckResult Check(IEnumerable<TechnicianAvailability> rows, DateTime when)
        {
            var list = rows?.ToList() ?? new List<TechnicianAvailability>();
            if (list.Count == 0)
                return new AvailabilityCheckResult(true, false, "No availability configured for this technician.");

            var row = list.FirstOrDefault(r => r.DayOfWeek == (int)when.DayOfWeek);
            if (row == null || !row.IsAvailable || row.StartTime == null || row.EndTime == null)
                return new AvailabilityCheckResult(false, true, $"Technician is not available on {when:dddd}.");

            var t = when.TimeOfDay;
            if (t < row.StartTime || t >= row.EndTime)
                return new AvailabilityCheckResult(false, true,
                    $"Technician works {Fmt(row.StartTime)}-{Fmt(row.EndTime)} on {when:dddd}.");

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
        /// Validates a full job slot: end after start, same day, inside working hours,
        /// and no overlap with another job of the same technician.
        /// </summary>
        public async Task<AvailabilityCheckResult> CheckJobSlotAsync(
            int technicianId, DateTime start, DateTime end, int? excludeContactId, int? excludeSchedulerId)
        {
            if (end <= start)
                return new AvailabilityCheckResult(false, true, "Job end time must be after the start time.");

            if (start.Date != end.Date)
                return new AvailabilityCheckResult(false, true, "Job must start and end on the same day.");

            var rows = await _context.TechnicianAvailabilities
                .AsNoTracking()
                .Where(a => a.TechnicianId == technicianId)
                .ToListAsync();

            // 1) start inside working hours
            var startCheck = Check(rows, start);
            if (!startCheck.Allowed) return startCheck;

            // 2) end inside working hours
            var row = rows.FirstOrDefault(r => r.DayOfWeek == (int)start.DayOfWeek);
            if (row != null && row.IsAvailable && row.EndTime.HasValue && end.TimeOfDay > row.EndTime.Value)
            {
                return new AvailabilityCheckResult(false, true,
                    $"Job ends at {Fmt(end.TimeOfDay)}, after the technician's working hours end ({Fmt(row.EndTime)}).");
            }

            // 3) no overlap with another job of this technician
            var overlapQuery = _context.AllScheduledJobs
                .AsNoTracking()
                .Where(j => j.TechnicianId == technicianId
                            && j.JobBookDate < end
                            && start < j.JobCompletedDate);

            // ignore the same customer + same scheduler (re-saving the same job)
            if (excludeContactId.HasValue)
                overlapQuery = overlapQuery.Where(j => !(j.ContactDetailId == excludeContactId && j.SchedulerId == excludeSchedulerId));

            var clash = await overlapQuery.OrderBy(j => j.JobBookDate).FirstOrDefaultAsync();
            if (clash != null)
            {
                return new AvailabilityCheckResult(false, true,
                    $"Technician is already booked {clash.JobBookDate:dd-MMM HH:mm} to {clash.JobCompletedDate:HH:mm} (job #{clash.Id}).");
            }

            return new AvailabilityCheckResult(true, startCheck.Configured, "Available.");
        }

        private static string Fmt(TimeSpan? t) => t.HasValue ? t.Value.ToString(@"hh\:mm") : "--:--";
    }
}