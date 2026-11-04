using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Filters
{
    /// <summary>
    /// Global filter. RoleAuthorize stays as it is; this only RECORDS afterwards, for STAFF, and only when no
    /// other filter short-circuited the request. Page views = full GET page loads (ViewResult), never AJAX.
    /// Actions = the whitelist below, so grid polling and lookups can never count as activity.
    /// </summary>
    public class StaffActivityFilter : IAsyncActionFilter
    {
        private static readonly Dictionary<string, string> Tracked = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Conversations.SendSms"] = "Sent an SMS reply",
            ["Sms.DeleteMsg"] = "Deleted a message",
            ["Sms.Delete"] = "Deleted a message",
            ["Sms.DeleteLogsByDate"] = "Deleted messages by date range",
            ["Sms.DownloadLogsByDate"] = "Downloaded the SMS log (CSV)",
            ["Sms.SaveContactJob"] = "Saved contact / job / message plan",
            ["Sms.OpenAttachments"] = "Opened message attachments",
            ["Sms.DownloadAttachment"] = "Downloaded an attachment",
            ["Sms.GetContactDetailsByPhone"] = "Opened a customer's details",
            ["Sms.TogglePausePlan"] = "Paused / resumed a scheduled message",
            ["Sms.DeletePlan"] = "Deleted a scheduled message",
            ["Sms.DeleteScheduler"] = "Deleted a scheduler"
        };

        private static readonly Dictionary<string, string> Pages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Sms.IncomingLogs"] = "Opened Incoming SMS",
            ["Sms.ScheduledJobs"] = "Opened Scheduled jobs",
            ["Sms.TechnicianJobs"] = "Opened Technician jobs",
            ["Conversations.SMS"] = "Opened Send SMS",
            ["Conversations.Chat"] = "Opened a conversation",
            ["IvrOptions.Index"] = "Opened Main options",
            ["OptionInstructions.Index"] = "Opened Option instructions",
            ["IntroSms.Create"] = "Opened Intro SMS",
            ["Technicians.Index"] = "Opened Technicians",
            ["AutomatedNotifications.Index"] = "Opened Automated SMS",
            ["SmsLogs.Index"] = "Opened SMS Logs",
            ["Profile.Index"] = "Opened My profile"
        };

        private static readonly string[] TargetNames =
            { "phoneNumber", "num", "toNum", "to", "id", "smsId", "planId", "schedulerId", "path", "fileName" };

        private readonly ActivityTracker _tracker;
        public StaffActivityFilter(ActivityTracker tracker) => _tracker = tracker;

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var executed = await next();
            try
            {
                if (executed.Canceled) return;                       // denied / redirected by another filter
                var http = ctx.HttpContext;
                if (!ActivityTracker.IsStaff(http.Session.GetString("UserRole"))) return;
                if (!int.TryParse(http.Session.GetString("UserId"), out var userId)) return;

                var controller = ctx.RouteData.Values["controller"]?.ToString() ?? "";
                var action = ctx.RouteData.Values["action"]?.ToString() ?? "";
                var key = controller + "." + action;

                if (Tracked.TryGetValue(key, out var text))
                {
                    await _tracker.TrackAsync(http, userId, ActivityTypes.Action, text, controller, action,
                        FindTarget(ctx), executed.Exception == null || executed.ExceptionHandled);
                    return;
                }

                bool isAjax = http.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (HttpMethods.IsGet(http.Request.Method) && !isAjax && executed.Result is ViewResult
                    && !controller.Equals("Account", StringComparison.OrdinalIgnoreCase))
                {
                    var desc = Pages.TryGetValue(key, out var p) ? p : $"Opened {controller} / {action}";
                    await _tracker.TrackAsync(http, userId, ActivityTypes.PageView, desc, controller, action, FindTarget(ctx));
                }
            }
            catch { /* tracking never breaks a request */ }
        }

        private static string? FindTarget(ActionExecutingContext ctx)
        {
            var args = ctx.ActionArguments;
            if (args.Count == 0) return null;

            foreach (var name in TargetNames)
                foreach (var kv in args)
                    if (kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase) && kv.Value != null && IsSimple(kv.Value))
                        return kv.Value.ToString();

            // date-range actions
            var from = args.FirstOrDefault(k => k.Key.Equals("fromDate", StringComparison.OrdinalIgnoreCase)).Value as DateTime?;
            var to = args.FirstOrDefault(k => k.Key.Equals("toDate", StringComparison.OrdinalIgnoreCase)).Value as DateTime?;
            if (from.HasValue && to.HasValue) return $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";

            // request objects (SaveContactJob and similar)
            foreach (var v in args.Values)
            {
                if (v == null || IsSimple(v)) continue;
                foreach (var prop in new[] { "PhoneNumber", "Phone", "ToNum", "To" })
                {
                    var val = v.GetType().GetProperty(prop)?.GetValue(v) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            return null;
        }

        private static bool IsSimple(object v) => v is string || v is decimal || v is DateTime || v.GetType().IsPrimitive;
    }
}