namespace SendSmsCallAlerts.Models
{
    public static class AppRoles
    {
        // Stored in User.Role and in Session["UserRole"].
        // Uppercase on purpose: existing controllers compare against "ADMIN".
        public const string Admin = "ADMIN";
        public const string Staff = "STAFF";

        public static readonly string[] All = { Admin, Staff };

        // "ADMIN"/"admin" -> ADMIN. Anything else (null, "USER", unknown) -> STAFF.
        public static string Normalize(string? role) =>
            string.Equals(role?.Trim(), Admin, StringComparison.OrdinalIgnoreCase) ? Admin : Staff;

        public static bool IsValid(string? role) =>
            role != null && All.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);

        public static string DisplayName(string? role) =>
            Normalize(role) == Admin ? "Admin" : "Staff";
    }
}