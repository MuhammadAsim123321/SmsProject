using Microsoft.AspNetCore.Mvc;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    /// <summary>Admin-only. Role is re-read from the DB on every request by RoleAuthorize.</summary>
    [RoleAuthorize(AppRoles.Admin)]
    public class DashboardController : Controller
    {
        private const int MaxRangeDays = 366;

        private readonly DashboardService _dashboard;
        private readonly IAppClock _clock;

        public DashboardController(DashboardService dashboard, IAppClock clock)
        {
            _dashboard = dashboard;
            _clock = clock;
        }

        public IActionResult Index()
        {
            // "today" in the business time zone, not the server's
            ViewBag.Today = _clock.BusinessNow.ToString("yyyy-MM-dd");
            return View();
        }

        /// <summary>from / to = business days, yyyy-MM-dd, inclusive. Missing = today.</summary>
        [HttpGet]
        public async Task<IActionResult> Summary(string? from, string? to)
        {
            var today = _clock.BusinessNow.Date;
            var fromDay = DashboardService.TryParseDay(from, out var f) ? f.Date : today;
            var toDay = DashboardService.TryParseDay(to, out var t) ? t.Date : fromDay;
            if (toDay < fromDay) (fromDay, toDay) = (toDay, fromDay);

            if ((toDay - fromDay).TotalDays >= MaxRangeDays)
                return BadRequest(new { message = "Choose a range of up to 1 year." });

            return Json(await _dashboard.GetSummaryAsync(fromDay, toDay));
        }
    }
}