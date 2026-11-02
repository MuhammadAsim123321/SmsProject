using System.ComponentModel.DataAnnotations;

namespace SendSmsCallAlerts.Models
{
    /// <summary>One row per login of a STAFF user.</summary>
    public class UserLoginSession
    {
        [Key]
        public long Id { get; set; }
        public int UserId { get; set; }

        public DateTime LoginAt { get; set; } = DateTime.UtcNow;           // UTC

        /// <summary>Last sign of life (a tracked request or a heartbeat ping). UTC.</summary>
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null = never logged out explicitly (browser closed / session timed out).</summary>
        public DateTime? LogoutAt { get; set; }

        /// <summary>Seconds the user was really working (see ActivityTracker.ActiveGapSeconds).</summary>
        public int ActiveSeconds { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(300)]
        public string? UserAgent { get; set; }
    }

    public static class ActivityTypes
    {
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string LoginFailed = "LoginFailed";
        public const string PageView = "PageView";   // opened a page
        public const string Action = "Action";       // did something (send, save, delete ...)
    }

    /// <summary>One row per tracked step of a STAFF user.</summary>
    public class UserActivityLog
    {
        [Key]
        public long Id { get; set; }
        public int UserId { get; set; }
        public long? LoginSessionId { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;         // UTC

        [Required, StringLength(20)]
        public string Type { get; set; } = ActivityTypes.PageView;

        [StringLength(50)]
        public string? Controller { get; set; }

        [StringLength(50)]
        public string? ActionName { get; set; }

        [StringLength(10)]
        public string? HttpMethod { get; set; }

        /// <summary>Human text shown to the admin, e.g. "Sent an SMS".</summary>
        [Required, StringLength(200)]
        public string Description { get; set; } = "";

        /// <summary>Customer number / record id the step was about (when known).</summary>
        [StringLength(60)]
        public string? Target { get; set; }

        [StringLength(300)]
        public string? Path { get; set; }

        public bool Success { get; set; } = true;
    }
}