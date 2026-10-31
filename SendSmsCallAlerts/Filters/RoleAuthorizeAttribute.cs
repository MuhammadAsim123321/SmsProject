using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System.Net;

namespace SendSmsCallAlerts.Filters
{
    /// [RoleAuthorize]                  -> any logged-in user
    /// [RoleAuthorize(AppRoles.Admin)]  -> Admin only
    /// Class-level and action-level attributes both run (AND): an action can only be STRICTER than its controller.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RoleAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles ?? Array.Empty<string>();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                return;

            var http = context.HttpContext;
            var isAjax = http.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // 1) logged in? (same contract as the old SessionChecking: AJAX gets 403 "LogOut", pages get redirected)
            if (!int.TryParse(http.Session.GetString("UserId"), out var userId))
            {
                SessionExpired(context, isAjax);
                return;
            }

            // 2) read the CURRENT role from the DB, so role changes and deleted users apply immediately
            var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
            var row = db.User.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Role })
                        .FirstOrDefault();

            if (row == null)
            {
                http.Session.Clear();
                SessionExpired(context, isAjax);
                return;
            }

            var role = AppRoles.Normalize(row.Role);
            http.Session.SetString("UserRole", role); // keeps layout/sidebar and old controllers in sync

            // 3) role check (no roles listed = any logged-in user)
            if (_roles.Length > 0 && !_roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = isAjax
                    ? new JsonResult(new { success = false, message = "You do not have permission to perform this action." }) { StatusCode = 403 }
                    : new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }

        private static void SessionExpired(ActionExecutingContext context, bool isAjax)
        {
            var http = context.HttpContext;
            if (isAjax)
            {
                // _Layout.cshtml ajaxError looks for 403 + "LogOut" and redirects to the login page
                context.Result = new JsonResult("LogOut") { StatusCode = 403 };
            }
            else
            {
                var returnUrl = http.Request.Path + http.Request.QueryString;
                context.Result = new RedirectResult($"/Account/Logout?id=-2&returnUrl={WebUtility.UrlEncode(returnUrl)}");
            }
        }
    }
}