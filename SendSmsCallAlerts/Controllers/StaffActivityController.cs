using Microsoft.AspNetCore.Mvc;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    /// <summary>Admin-only. Role is re-read from the DB on every request by RoleAuthorize.</summary>
    [RoleAuthorize(AppRoles.Admin)]
    public class StaffActivityController : Controller
    {
        private const int MaxRangeDays = 366;
        private readonly StaffActivityService _svc;
        private readonly IAppClock _clock;
        private readonly RingCentralService _rc;
        private readonly ILogger<StaffActivityController> _logger;

        public StaffActivityController(StaffActivityService svc, IAppClock clock, RingCentralService rc, ILogger<StaffActivityController> logger)
        {
            _svc = svc;
            _clock = clock;
            _rc = rc;
            _logger = logger;
        }

        private (DateTime From, DateTime To) Range(string? from, string? to)
        {
            var today = _clock.BusinessNow.Date;
            var f = DashboardService.TryParseDay(from, out var pf) ? pf.Date : today;
            var t = DashboardService.TryParseDay(to, out var pt) ? pt.Date : f;
            if (t < f) (f, t) = (t, f);
            if ((t - f).TotalDays >= MaxRangeDays) f = t.AddDays(-(MaxRangeDays - 1));
            ViewBag.From = f.ToString("yyyy-MM-dd");
            ViewBag.To = t.ToString("yyyy-MM-dd");
            ViewBag.Today = today.ToString("yyyy-MM-dd");
            ViewBag.Last7 = today.AddDays(-6).ToString("yyyy-MM-dd");
            ViewBag.Last30 = today.AddDays(-29).ToString("yyyy-MM-dd");
            ViewBag.MonthStart = new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM-dd");   // NEW
            return (f, t);
        }

        public async Task<IActionResult> Detail(int id, string? from, string? to, string? group)
        {
            var (f, t) = Range(from, to);
            var g = (group ?? "day").ToLowerInvariant();
            ViewBag.Group = g is "week" or "month" ? g : "day";
            var detail = await _svc.GetDetailAsync(id, f, t, (string)ViewBag.Group);
            if (detail == null) return NotFound();
            return View(detail);
        }

        public async Task<IActionResult> Index(string? from, string? to)
        {
            var (f, t) = Range(from, to);

            // Fetch SMS data (existing behavior)
            var staffRows = await _svc.GetStaffRowsAsync(f, t);

            // Fetch phone call data (new)
            var callRows = new List<RcCallRowVm>();
            string? callsError = null;

            try
            {
                // Convert local date boundaries to UTC before querying RingCentral
                var utcFrom = _clock.ToUtc(f);
                var utcTo = _clock.ToUtc(t.AddDays(1)); // exclusive end

                var records = await _rc.GetAllOutboundCallsAsync(utcFrom, utcTo);

                callRows = records
                    .Select(r => MapRcCallRow(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch RingCentral call data for date range {From} to {To}", f, t);
                callsError = "Could not load call data. " + ex.Message;
            }

            var vm = new StaffActivityIndexVm
            {
                StaffRows = staffRows,
                CallRows = callRows,
                CallsError = callsError
            };

            return View(vm);
        }

        // ── RingCentral helper (mirrors RingCentralCallsController.MapRow) ──

        // replaced by staff-calls-fix 2024 [this method is new; no old version to comment out]
        private RcCallRowVm MapRcCallRow(RcCallRecord r)
        {
            // Convert the UTC timestamp from RingCentral to the business local timezone
            // so the displayed date matches what the user filtered by.
            var startLocal = r.StartTime.HasValue
                ? _clock.ToBusiness(r.StartTime.Value)
                : DateTime.MinValue;

            return new RcCallRowVm
            {
                Id = r.Id ?? "",
                Date = startLocal == DateTime.MinValue ? "—" : startLocal.ToString("dd-MMM-yyyy"),
                Time = startLocal == DateTime.MinValue ? "—" : startLocal.ToString("HH:mm:ss"),
                FromNumber = r.From?.PhoneNumber ?? r.From?.ExtensionNumber ?? "—",
                FromName = r.From?.Name ?? "",
                ToNumber = r.To?.PhoneNumber ?? r.To?.ExtensionNumber ?? "—",
                ToName = r.To?.Name ?? "",
                Duration = FormatDuration(r.Duration),
                DurationSeconds = r.Duration,
                Result = r.Result ?? "—",
                ResultCss = ResultToCss(r.Result)
            };
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds <= 0) return "0:00";
            var m = seconds / 60;
            var s = seconds % 60;
            return $"{m}:{s:D2}";
        }

        private static string ResultToCss(string? result) => result?.ToLowerInvariant() switch
        {
            "call connected" => "nova-status--sent",
            "accepted" => "nova-status--sent",
            "no answer" => "nova-status--skipped",
            "busy" => "nova-status--skipped",
            "voicemail" => "nova-status--pending",
            "rejected" => "nova-status--failed",
            "missed" => "nova-status--failed",
            "stopped" => "nova-status--failed",
            _ => "nova-status--muted"
        };
        
        // ── old Index() [replaced by staff-calls-fix 2024]
        // public async Task<IActionResult> Index(string? from, string? to)
        // {
        //     var (f, t) = Range(from, to);
        //     return View(await _svc.GetStaffRowsAsync(f, t));
        // }

        
    }
}