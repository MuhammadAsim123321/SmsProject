using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Task 7 + 9. Picks up SmsLogs rows with Status = Pending that are due, sends them through
    /// ISmsGateway (Test/Live is decided by AppSettings:SmsMode inside the gateway) and updates the row:
    ///   success                  -> "Message Sent"
    ///   failure, attempts left   -> stays "Pending", NextAttemptAt = now + delay, FailureReason = last error
    ///   failure, no attempts     -> "SMS Failed" + reason
    ///   waited too long          -> "SMS Failed" ("Expired")
    /// </summary>
    public class SmsDispatchService
    {
        private const int LeaseMinutes = 5;   // a claimed row is invisible to other runs for this long (crash safety)

        private readonly ApplicationDbContext db;
        private readonly ISmsGateway gateway;
        private readonly ILogger<SmsDispatchService> logger;
        private readonly int maxAttempts;
        private readonly int batchSize;
        private readonly TimeSpan expireAfter;
        private readonly int[] retryDelays;

        public SmsDispatchService(ApplicationDbContext db, ISmsGateway gateway, IConfiguration config,
            ILogger<SmsDispatchService> logger)
        {
            this.db = db;
            this.gateway = gateway;
            this.logger = logger;
            maxAttempts = Math.Max(1, config.GetValue<int?>("AutomatedSms:MaxAttempts") ?? 3);
            batchSize = config.GetValue<int?>("AutomatedSms:DispatchBatchSize") ?? 50;
            expireAfter = TimeSpan.FromHours(config.GetValue<double?>("AutomatedSms:ExpireAfterHours") ?? 6);
            retryDelays = config.GetSection("AutomatedSms:RetryDelaysMinutes").Get<int[]>() ?? new[] { 2, 10 };
            if (retryDelays.Length == 0) retryDelays = new[] { 5 };
        }

        /// <summary>Called by Hangfire every minute. Returns how many messages were processed.</summary>
        public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var ids = await db.SmsLogs.AsNoTracking()
                .Where(l => l.Status == SmsLogStatus.Pending && (l.NextAttemptAt == null || l.NextAttemptAt <= now))
                .OrderBy(l => l.QueuedAt)
                .Select(l => l.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            var processed = 0;
            foreach (var id in ids)
            {
                // Claim the row. If another run got it first, 0 rows are updated and we skip it.
                var lease = now.AddMinutes(LeaseMinutes);
                var claimed = await db.SmsLogs
                    .Where(l => l.Id == id && l.Status == SmsLogStatus.Pending
                                && (l.NextAttemptAt == null || l.NextAttemptAt <= now))
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.NextAttemptAt, lease), ct);
                if (claimed == 0) continue;

                try
                {
                    var log = await db.SmsLogs.FirstAsync(l => l.Id == id, ct);
                    await DeliverAsync(log, ct);
                    processed++;
                }
                catch (Exception ex)
                {
                    // never let one bad row stop the batch; the lease makes it retry later
                    logger.LogError(ex, "Dispatching SmsLogs {Id} failed", id);
                }
            }
            return processed;
        }

        private async Task DeliverAsync(SmsLog log, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            if (now - log.QueuedAt > expireAfter)
            {
                MarkFailed(log, now, $"Expired: not delivered within {expireAfter.TotalHours:0.#} hours" +
                                     (string.IsNullOrWhiteSpace(log.FailureReason) ? "" : ". Last error: " + log.FailureReason));
                await SyncGuardAsync(log, ct);
                await db.SaveChangesAsync(ct);
                return;
            }

            log.AttemptCount++;
            log.LastAttemptAt = now;

            bool ok = false, simulated = false;
            string? error = null;
            try
            {
                var send = await gateway.SendAsync(log.RecipientNumber, log.MessageContent);
                ok = send.Success;
                simulated = send.Simulated;
                error = send.Error;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                logger.LogWarning(ex, "Gateway threw for SmsLogs {Id}", log.Id);
            }

            log.UpdatedOn = now;
            if (ok)
            {
                log.Status = SmsLogStatus.MessageSent;
                log.SentDate = now;
                log.IsSimulated = simulated;
                log.FailureReason = null;
                log.NextAttemptAt = null;
                await SyncGuardAsync(log, ct);
            }
            else
            {
                var reason = string.IsNullOrWhiteSpace(error) ? "Send failed" : error!;
                if (log.AttemptCount >= maxAttempts)
                {
                    MarkFailed(log, now, $"{reason} (after {log.AttemptCount} attempts)");
                    await SyncGuardAsync(log, ct);
                }
                else
                {
                    var delay = retryDelays[Math.Min(log.AttemptCount - 1, retryDelays.Length - 1)];
                    log.Status = SmsLogStatus.Pending;
                    log.NextAttemptAt = now.AddMinutes(delay);
                    log.FailureReason = Truncate($"Attempt {log.AttemptCount} failed: {reason}. Retrying in {delay} min.", 1000);
                }
            }
            await db.SaveChangesAsync(ct);
        }

        private static void MarkFailed(SmsLog log, DateTime now, string reason)
        {
            log.Status = SmsLogStatus.SmsFailed;
            log.FailureReason = Truncate(reason, 1000);
            log.NextAttemptAt = null;
            log.UpdatedOn = now;
        }

        /// <summary>Keep the "occurrence guard" row in AutomatedSmsLogs consistent with the final result.</summary>
        private async Task SyncGuardAsync(SmsLog log, CancellationToken ct)
        {
            if (log.AutomatedSmsLogId == null) return;
            var guard = await db.AutomatedSmsLogs.FirstOrDefaultAsync(g => g.Id == log.AutomatedSmsLogId, ct);
            if (guard == null) return;
            if (log.Status == SmsLogStatus.MessageSent)
            {
                guard.Status = log.IsSimulated ? AutomatedSmsStatus.NoTwilio : AutomatedSmsStatus.Sent;
                guard.SentAt = log.SentDate;
                guard.ErrorMessage = null;
            }
            else
            {
                guard.Status = AutomatedSmsStatus.Failed;
                guard.ErrorMessage = log.FailureReason;
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}