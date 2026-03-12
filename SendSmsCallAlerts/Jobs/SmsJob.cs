using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML.Voice;

namespace SendSmsCallAlerts.Jobs
{
    public class SmsJob
    {
        private readonly ApplicationDbContext dbContext;
        private readonly string TmZn = "Pakistan Standard Time";
        private readonly string accountSid = "";
        private readonly string authToken = "";


        public SmsJob(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        private const int LOOK_BACK_MINUTES = 1;

        public async System.Threading.Tasks.Task RegisterAllJobs()
        {
            DateTime now = GetTime();
            DateTime windowStart = await GetWindowStart(now);

            await LogJobExecution(now);

            var activeJobs = await GetActiveJobs();
            if (!activeJobs.Any()) return;

            var dueJobs = activeJobs.Where(j => IsJobDueInWindow(j, windowStart, now)).ToList();
            if (!dueJobs.Any()) return;

            await ProcessDueJobs(dueJobs, now);

            await dbContext.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task<DateTime> GetWindowStart(DateTime now)
        {
            var lastLog = await dbContext.HangfireJobLogs
                .OrderByDescending(x => x.ExecutedAt)
                .FirstOrDefaultAsync();

            return lastLog != null ? lastLog.ExecutedAt : now.AddMinutes(-LOOK_BACK_MINUTES);
        }

        private async System.Threading.Tasks.Task LogJobExecution(DateTime now)
        {
            dbContext.HangfireJobLogs.Add(new HangfireJobLog { ExecutedAt = now });
            await dbContext.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task<List<AllScheduledJob>> GetActiveJobs()
        {
            var optOutNumbers = await dbContext.OptOuts
                .Select(o => o.PhoneNumber)
                .ToListAsync();

            return await dbContext.AllScheduledJobs
                .Include(j => j.Scheduler)
                    .ThenInclude(s => s.TimeToRun)
                .Include(j => j.Scheduler)
                    .ThenInclude(s => s.RunFrom)
                .Include(j => j.ContactDetail)
                .Where(j => j.Scheduler.status.ToLower() == "active"
                         && j.IsPaused == false
                         && !optOutNumbers.Contains(j.ContactDetail.PhoneNumber))
                .ToListAsync();
        }

        private async System.Threading.Tasks.Task ProcessDueJobs(List<AllScheduledJob> dueJobs, DateTime now)
        {
            foreach (var job in dueJobs)
            {
                if (job.Scheduler == null || job.ContactDetail == null) continue;
               // if (await IsAlreadySentToday(job)) continue;
                await SendSmsAsync(job, now);
            }
        }

        private async System.Threading.Tasks.Task<bool> IsAlreadySentToday(AllScheduledJob job)
        {
            DateTime today = GetTime();
            var smsLogs = await dbContext.SmsDetails
                .Where(s => s.ScheduledJobId == job.Id && s.createdOn.Date == today.Date)
                .ToListAsync();

            return smsLogs.Any(s => s.smsDirection == "Outgoing");
        }

        private bool IsJobDueInWindow(AllScheduledJob job, DateTime windowStart, DateTime now)
        {
            if (job.Scheduler == null) return false;

            int hourCount = job.Scheduler.TimeToRun?.HourCount ?? 0;

            DateTime runFromDate;

            switch (job.Scheduler.RunFromId)
            {
                case 1: // Today Date
                    runFromDate = job.AssignedDate;
                    break;
                case 2: // Custom Date
                    runFromDate = job.CustomDate ?? job.AssignedDate;
                    break;
                case 3: // Job Book Date
                    runFromDate = job.JobBookDate;
                    break;
                case 4: // Job Created Date
                    runFromDate = job.JobCompletedDate;
                    break;
                default:
                    return false;
            }

            if (hourCount == 0) return true;

            DateTime scheduledTime = runFromDate.AddHours(hourCount);
            return scheduledTime >= windowStart && scheduledTime <= now;
        }

        private async System.Threading.Tasks.Task SendSmsAsync(AllScheduledJob job, DateTime now)
        {
            var contact = job.ContactDetail!;
            var scheduler = job.Scheduler!;

            string fromNum = GetFromNumber(); //(scheduler.fromNum ?? "").Trim();
            string customerPhone = (contact.PhoneNumber ?? "").Trim();

            //if (fromNum.Length == 10) fromNum = "+1" + fromNum;
            if (customerPhone.Length == 10) customerPhone = "+1" + customerPhone;

            string finalMessage = BuildMessage(job, contact, now);

            var schedulerImages = await dbContext.MmsLinkss
                .Where(m => m.SchedulerId == scheduler.Id && m.SmsId == 0)
                .ToListAsync();
            try
            {
                TwilioClient.Init(accountSid, authToken);

                int newSmsId = 0;

                if (schedulerImages.Any())
                {
                    var mediaUrls = schedulerImages.Select(img => new Uri(img.Location)).ToList();

                    if (!string.IsNullOrEmpty(accountSid))
                    {
                        MessageResource.Create(
                            body: finalMessage,
                            from: new Twilio.Types.PhoneNumber(fromNum),
                            to: new Twilio.Types.PhoneNumber(customerPhone),
                            mediaUrl: mediaUrls);
                    }

                    newSmsId = await SaveSmsLog(customerPhone, fromNum, finalMessage, "Outgoing", contact.Id, job.Id);

                    foreach (var img in schedulerImages)
                    {
                        dbContext.MmsLinkss.Add(new MmsLinks
                        {
                            SmsId = newSmsId,
                            FileName = img.FileName,
                            Location = img.Location,
                            SchedulerId = img.SchedulerId
                        });
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(accountSid))
                    {
                        MessageResource.Create(
                            body: finalMessage,
                            from: new Twilio.Types.PhoneNumber(fromNum),
                            to: new Twilio.Types.PhoneNumber(customerPhone));
                    }

                    newSmsId = await SaveSmsLog(customerPhone, fromNum, finalMessage, "Outgoing", contact.Id, job.Id);
                }

                // Mark once-job as done
                if (scheduler.onceOrRepeat?.ToLower() == "once")
                {
                    scheduler.status = "sent";
                    job.JobCompletedDate = now;
                }

                job.LastExecutedAt = now;
            }
            catch (Exception ex)
            {
                await SaveSmsLog(
                    customerPhone, fromNum,
                    finalMessage + "\n[Error]: " + ex.Message,
                    "OutgoingFailed",
                    contact.Id,
                    job.Id);
            }
        }

        private string BuildMessage(AllScheduledJob job, ContactDetail contact, DateTime now)
        {
            string templatebody = job.Scheduler!.smsBody ?? "";
            string customerName = contact.Name ?? "";
            string customerEmail = contact.Email ?? "";
            string customerAddr = contact.Address ?? "";
            string bookDate = job.JobBookDate.ToString("dd-MMM-yyyy");
            string completeDate = job.JobCompletedDate.ToString("dd-MMM-yyyy HH:mm");

            string finalMessage = templatebody
                .Replace("{name}", customerName)
                .Replace("{email}", customerEmail)
                .Replace("{address}", customerAddr)
                .Replace("{jobBookDate}", bookDate)
                .Replace("{jobCompletedDate}", completeDate);

            if (!templatebody.Contains("{name}") && !string.IsNullOrEmpty(customerName))
                finalMessage = customerName + " " + finalMessage;

            return finalMessage;
        }

        private async System.Threading.Tasks.Task<int> SaveSmsLog(
           string toNum, string frNum, string smsBody, string dir,
           int? contactId = null, int? scheduledJobId = null)
        {
            SmsDetail conv = new SmsDetail();
            conv.toNum = toNum;
            conv.fromNum = frNum;
            conv.smsBody = smsBody;
            conv.smsDirection = dir;
            conv.createdOn = GetTime();
            conv.IsReadYesOrNo = "";
            conv.imgPath = "";
            conv.ContactDetailId = contactId;
            conv.ScheduledJobId = scheduledJobId;

            dbContext.SmsDetails.Add(conv);
            await dbContext.SaveChangesAsync();

            return conv.Id;
        }

        private DateTime GetTime()
        {
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TmZn);
            DateTime usEastCoastNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);
            return usEastCoastNow;
        }

        private string GetFromNumber()
        {
            return "+18557233022";
        }
    }
}