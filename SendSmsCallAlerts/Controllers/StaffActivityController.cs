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

        public StaffActivityController(StaffActivityService svc, IAppClock clock) { _svc = svc; _clock = clock; }

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
            return (f, t);
        }

        public async Task<IActionResult> Index(string? from, string? to)
        {
            var (f, t) = Range(from, to);
            return View(await _svc.GetStaffRowsAsync(f, t));
        }

        public async Task<IActionResult> Detail(int id, string? from, string? to)
        {
            var (f, t) = Range(from, to);
            var detail = await _svc.GetDetailAsync(id, f, t);
            if (detail == null) return NotFound();
            return View(detail);
        }
    }
}