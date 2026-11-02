using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Filters
{
    public class HangfireAdminFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            if (!int.TryParse(http.Session.GetString("UserId"), out var userId))
                return false;

            var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
            var row = db.User.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Role })
                        .FirstOrDefault();

            return row != null && AppRoles.Normalize(row.Role) == AppRoles.Admin;
        }
    }
}