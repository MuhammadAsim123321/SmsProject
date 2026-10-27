using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Writes SmsLogs rows for messages that are sent immediately (Send SMS page, job message plan).
    /// Automated notifications do not use this: they are queued as Pending and sent by SmsDispatchService.
    /// Logging must never break sending, so failures here are only written to the application log.
    /// </summary>
    public class SmsLogService
    {
        private readonly ApplicationDbContext db;
        private readonly ILogger<SmsLogService> logger;

        public SmsLogService(ApplicationDbContext db, ILogger<SmsLogService> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        /// <summary>
        /// Record the outcome of a send that already happened.
        /// Call it right after gateway.SendAsync(...) with the result (success or failure).
        /// </summary>
        public async Task RecordImmediateAsync(
            string source,
            string recipientNumber,
            string body,
            bool success,
            bool simulated,
            string? error,
            int? jobId = null,
            int? smsDetailId = null,
            string? recipientName = null,
            string? recipientType = null,
            int? recipientId = null,
            CancellationToken ct = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                db.SmsLogs.Add(new SmsLog
                {
                    Source = source,
                    RecipientNumber = Truncate(recipientNumber ?? "", 30),
                    MessageContent = body ?? "",
                    Status = success ? SmsLogStatus.MessageSent : SmsLogStatus.SmsFailed,
                    SentDate = success ? now : null,
                    FailureReason = success ? null : Truncate(string.IsNullOrWhiteSpace(error) ? "Send failed" : error!, 1000),
                    IsSimulated = simulated,
                    AttemptCount = 1,
                    QueuedAt = now,
                    LastAttemptAt = now,
                    JobId = jobId,
                    SmsDetailId = smsDetailId,
                    RecipientName = recipientName,
                    RecipientType = recipientType,
                    RecipientId = recipientId,
                    CreatedOn = now
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not write SmsLogs row for {Number}", recipientNumber);
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}