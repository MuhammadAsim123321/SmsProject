using Microsoft.AspNetCore.Mvc;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    // ── View-models ─────────────────────────────────────────────────────────────

    public class RcCallRowVm
    {
        public string Id { get; set; } = "";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string FromNumber { get; set; } = "";
        public string FromName { get; set; } = "";
        public string ToNumber { get; set; } = "";
        public string ToName { get; set; } = "";
        public string Duration { get; set; } = "";   // formatted as mm:ss
        public int DurationSeconds { get; set; }
        public string Result { get; set; } = "";
        public string ResultCss { get; set; } = "";
    }

    public class RcCallsVm
    {
        public List<RcCallRowVm> Rows { get; set; } = new();
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Q { get; set; }
        public string? Error { get; set; }
    }

    // ── Controller ──────────────────────────────────────────────────────────────

    [SessionChecking]
    public class RingCentralCallsController : Controller
    {
        private readonly RingCentralService _rc;
        private readonly IAppClock _clock;

        public RingCentralCallsController(RingCentralService rc, IAppClock clock)
        {
            _rc = rc;
            _clock = clock;
        }

        public async Task<IActionResult> Index(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? q = null)
        {
            // dateFrom / dateTo are the user-visible inclusive local dates shown in the date pickers.
            dateFrom ??= _clock.BusinessNow.AddDays(-30).Date;
            dateTo   ??= _clock.BusinessNow.Date;

            var vm = new RcCallsVm
            {
                DateFrom = dateFrom,
                DateTo   = dateTo,
                Q        = q
            };

            try
            {
                // Convert the local date boundaries to UTC before sending to RingCentral.
                // Without this, "07-Oct" midnight is treated as UTC midnight, which is several
                // hours ahead of US local time — causing records from 08-Oct (local) to appear.
                var utcFrom = _clock.ToUtc(dateFrom.Value.Date);
                var utcTo   = _clock.ToUtc(dateTo.Value.Date.AddDays(1)); // exclusive end

                var records = await _rc.GetAllOutboundCallsAsync(utcFrom, utcTo);

                vm.Rows = records
                    .Where(r => string.IsNullOrWhiteSpace(q) || ContainsQuery(r, q))
                    .Select(r => MapRow(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                vm.Error = ex.Message;
            }

            return View(vm);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static bool ContainsQuery(RcCallRecord r, string q)
        {
            q = q.Trim();
            return (r.From?.PhoneNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.From?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.To?.PhoneNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.To?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Result?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private RcCallRowVm MapRow(RcCallRecord r)
        {
            // Convert the UTC timestamp from RingCentral to the business local timezone
            // so the displayed date matches what the user filtered by.
            var startLocal = r.StartTime.HasValue
                ? _clock.ToBusiness(r.StartTime.Value)
                : DateTime.MinValue;

            return new RcCallRowVm
            {
                Id              = r.Id ?? "",
                Date            = startLocal == DateTime.MinValue ? "—" : startLocal.ToString("dd-MMM-yyyy"),
                Time            = startLocal == DateTime.MinValue ? "—" : startLocal.ToString("HH:mm:ss"),
                FromNumber      = r.From?.PhoneNumber ?? r.From?.ExtensionNumber ?? "—",
                FromName        = r.From?.Name ?? "",
                ToNumber        = r.To?.PhoneNumber ?? r.To?.ExtensionNumber ?? "—",
                ToName          = r.To?.Name ?? "",
                Duration        = FormatDuration(r.Duration),
                DurationSeconds = r.Duration,
                Result          = r.Result ?? "—",
                ResultCss       = ResultToCss(r.Result)
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
            "accepted"       => "nova-status--sent",
            "no answer"      => "nova-status--skipped",
            "busy"           => "nova-status--skipped",
            "voicemail"      => "nova-status--pending",
            "rejected"       => "nova-status--failed",
            "missed"         => "nova-status--failed",
            "stopped"        => "nova-status--failed",
            _                => "nova-status--muted"
        };
    }
}
