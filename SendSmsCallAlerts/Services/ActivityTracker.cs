using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Writes STAFF login sessions and activity rows.
    /// Every call uses its OWN DbContext scope and never throws: tracking must not break a request.
    /// Active time: a gap between two signs of life counts only if it is <= IdleAfterMinutes.
    /// </summary>
    public class ActivityTracker
    {
        public const string SessionKey = "ActivitySessionId";

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ActivityTracker> _logger;
        private readonly int _activeGapSeconds;

        public ActivityTracker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ActivityTracker> logger)
        {
            _scopes = scopes;
            _logger = logger;
            _activeGapSeconds = Math.Max(60, (config.GetValue<int?>("StaffActivity:IdleAfterMinutes") ?? 5) * 60);
        }

        public static bool IsStaff(string? role) =>
            !string.IsNullOrWhiteSpace(role) && AppRoles.Normalize(role) == AppRoles.Staff;

        public async Task StartSessionAsync(HttpContext http, int userId)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var row = new UserLoginSession
                {
                    UserId = userId,
                    LoginAt = now,
                    LastActivityAt = now,
                    IpAddress = Cut(http.Connection.RemoteIpAddress?.ToString(), 45),
                    UserAgent = Cut(http.Request.Headers["User-Agent"].ToString(), 300)
                };
                db.UserLoginSessions.Add(row);
                await db.SaveChangesAsync();
                http.Session.SetString(SessionKey, row.Id.ToString());

                db.UserActivityLogs.Add(NewLog(http, userId, row.Id, ActivityTypes.Login, "Logged in", null, null, null, true));
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Activity: start session failed"); }
        }

        /// <summary>Call BEFORE the session keys are removed.</summary>
        // EndSessionAsync: signature + one line
        public async Task EndSessionAsync(HttpContext http, int userId, string logoutType = LogoutTypes.Manual)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var row = await GetOrCreateSessionAsync(db, http, userId, now);
                row.LogoutAt = now;
                row.LogoutType = logoutType;   // NEW
                db.UserActivityLogs.Add(NewLog(http, userId, row.Id, ActivityTypes.Logout,
                    logoutType == LogoutTypes.System ? "Logged out (session expired)" : "Logged out", null, null, null, true));
                await db.SaveChangesAsync();
                http.Session.Remove(SessionKey);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Activity: end session failed"); }
        }

        /// <summary>Wrong password for a STAFF username (unknown names and admins are ignored).</summary>
        public async Task LoginFailedAsync(HttpContext http, string? username)
        {
            try
            {
                username = (username ?? "").Trim();
                if (username.Length == 0) return;
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var u = await db.User.AsNoTracking().Where(x => x.Username == username)
                    .Select(x => new { x.Id, x.Role }).FirstOrDefaultAsync();
                if (u == null || !IsStaff(u.Role)) return;
                db.UserActivityLogs.Add(NewLog(http, u.Id, null, ActivityTypes.LoginFailed, "Failed login attempt", null, null, null, false));
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Activity: login-failed log failed"); }
        }

        /// <summary>Page view or action. Also counts as a sign of life.</summary>
        public async Task TrackAsync(HttpContext http, int userId, string type, string description,
            string? controller = null, string? action = null, string? target = null, bool success = true)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await GetOrCreateSessionAsync(db, http, userId, DateTime.UtcNow);
                db.UserActivityLogs.Add(NewLog(http, userId, row.Id, type, description, controller, action, target, success));
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Activity: track failed"); }
        }

        /// <summary>Heartbeat: sign of life only, no log row.</summary>
        public async Task PingAsync(HttpContext http, int userId)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await GetOrCreateSessionAsync(db, http, userId, DateTime.UtcNow);
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Activity: ping failed"); }
        }

        // finds the login-session row (creates one if the user logged in before tracking existed) and touches it
        private async Task<UserLoginSession> GetOrCreateSessionAsync(ApplicationDbContext db, HttpContext http, int userId, DateTime now)
        {
            UserLoginSession? row = null;
            if (long.TryParse(http.Session.GetString(SessionKey), out var sid))
                row = await db.UserLoginSessions.FirstOrDefaultAsync(s => s.Id == sid && s.UserId == userId);

            if (row == null)
            {
                row = new UserLoginSession
                {
                    UserId = userId,
                    LoginAt = now,
                    LastActivityAt = now,
                    IpAddress = Cut(http.Connection.RemoteIpAddress?.ToString(), 45),
                    UserAgent = Cut(http.Request.Headers["User-Agent"].ToString(), 300)
                };
                db.UserLoginSessions.Add(row);
                await db.SaveChangesAsync();
                http.Session.SetString(SessionKey, row.Id.ToString());
            }
            // GetOrCreateSessionAsync, in the else branch, add the first 5 lines.
            // The stale-session sweep may have closed a session that is actually still alive (AJAX-only usage). User is back, so reopen it.
            else
            {
                if (row.LogoutAt != null && row.LogoutType == LogoutTypes.System)
                {
                    row.LogoutAt = null;
                    row.LogoutType = null;
                }
                var gap = (int)(now - row.LastActivityAt).TotalSeconds;
                if (gap > 0 && gap <= _activeGapSeconds) row.ActiveSeconds += gap;
                if (now > row.LastActivityAt) row.LastActivityAt = now;
            }
            return row;
        }

        private static UserActivityLog NewLog(HttpContext http, int userId, long? sessionId, string type, string description,
            string? controller, string? action, string? target, bool success) => new()
            {
                UserId = userId,
                LoginSessionId = sessionId,
                CreatedOn = DateTime.UtcNow,
                Type = type,
                Controller = Cut(controller, 50),
                ActionName = Cut(action, 50),
                HttpMethod = Cut(http.Request.Method, 10),
                Description = Cut(description, 200) ?? "",
                Target = Cut(target, 60),
                Path = Cut(http.Request.Path.Value, 300),
                Success = success
            };

        private static string? Cut(string? s, int max) => s == null ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}