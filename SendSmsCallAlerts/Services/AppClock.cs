namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// One place for "what time is it" in the app.
    /// Rule: SmsDetail.createdOn is stored in UTC. Job start/end and plan dates are
    /// business wall-clock time (AppSettings:TmZn), because technician hours are wall-clock.
    /// </summary>
    public interface IAppClock
    {
        TimeZoneInfo TimeZone { get; }
        DateTime UtcNow { get; }
        /// <summary>Current wall-clock time in the business time zone.</summary>
        DateTime BusinessNow { get; }
        DateTime ToBusiness(DateTime utc);
        DateTime ToUtc(DateTime businessLocal);
    }

    public class AppClock : IAppClock
    {
        public TimeZoneInfo TimeZone { get; }

        public AppClock(IConfiguration config, ILogger<AppClock> logger)
        {
            var id = config["AppSettings:TmZn"];
            if (string.IsNullOrWhiteSpace(id))
            {
                logger.LogWarning("AppSettings:TmZn is not set. Falling back to UTC.");
                id = "UTC";
            }
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime BusinessNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

        public DateTime ToBusiness(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

        public DateTime ToUtc(DateTime businessLocal)
        {
            var unspecified = DateTime.SpecifyKind(businessLocal, DateTimeKind.Unspecified);
            if (TimeZone.IsInvalidTime(unspecified)) unspecified = unspecified.AddHours(1); // DST gap
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
        }
    }
}