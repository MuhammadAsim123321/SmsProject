using Microsoft.AspNetCore.Http;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Helpers
{
    public static class RoleViewExtensions
    {
        public static bool IsLoggedIn(this ISession? session) =>
            !string.IsNullOrWhiteSpace(session?.GetString("UserId"));

        public static string CurrentRole(this ISession? session) =>
            AppRoles.Normalize(session?.GetString("UserRole"));

        public static bool IsAdmin(this ISession? session) =>
            session.CurrentRole() == AppRoles.Admin;

        public static string RoleDisplayName(this ISession? session) =>
            AppRoles.DisplayName(session.CurrentRole());
    }
}