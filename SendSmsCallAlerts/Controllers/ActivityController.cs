using Microsoft.AspNetCore.Mvc;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    /// <summary>Heartbeat from the browser. Same protection as SmsController. Never logged as a page view.</summary>
    [SessionChecking]
    [RoleAuthorize]
    public class ActivityController : Controller
    {
        private readonly ActivityTracker _tracker;
        public ActivityController(ActivityTracker tracker) => _tracker = tracker;

        [HttpPost]
        public async Task<IActionResult> Ping()
        {
            if (ActivityTracker.IsStaff(HttpContext.Session.GetString("UserRole"))
                && int.TryParse(HttpContext.Session.GetString("UserId"), out var uid))
                await _tracker.PingAsync(HttpContext, uid);
            return NoContent();
        }
    }
}