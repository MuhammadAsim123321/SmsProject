//using Hangfire.Common;
//using Microsoft.EntityFrameworkCore;
//using SendSmsCallAlerts.Data;
//using SendSmsCallAlerts.Models;
//using Twilio;
//using Twilio.Rest.Api.V2010.Account;
//using Twilio.TwiML.Voice;

//namespace SendSmsCallAlerts.Jobs
//{
//    public class SmsJob
//    {
//        private readonly ApplicationDbContext dbContext;
//        private readonly string TmZn = "Eastern Standard Time";
//        private readonly string accountSid = "";
//        private readonly string authToken = "";

//        public SmsJob(ApplicationDbContext dbContext)
//        {
//            this.dbContext = dbContext;
//        }
//        private const int LOOK_BACK_MINUTES = 5;

//        private async System.Threading.Tasks.Task<int> SaveSmsLog(string toNum, string frNum, string smsBody, string dir, int? contactId = null)
//        {
//            SmsDetail conv = new SmsDetail();
//            conv.toNum = toNum;
//            conv.fromNum = frNum;
//            conv.smsBody = smsBody;
//            conv.smsDirection = dir;
//            conv.createdOn = DateTime.UtcNow;
//            conv.IsReadYesOrNo = "";
//            conv.imgPath = "";
//            conv.ContactDetailId = contactId;

//            dbContext.SmsDetails.Add(conv);
//            await dbContext.SaveChangesAsync();


//            return conv.Id;
//        }

//        public async System.Threading.Tasks.Task RegisterAllJobs()
//        {
//            if(!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken))
//                TwilioClient.Init(accountSid, authToken);
//            DateTime now = DateTime.Now;
//            DateTime windowStart = now.AddMinutes(-LOOK_BACK_MINUTES);
//            // fetxh all active non-paused with job
//            var allActive= await dbContext.AllScheduledJobs.
//                Include(j=>j.Scheduler)
//                .Include(j=>j.ContactDetail)
//                .Where(j=>j.Scheduler.status.ToLower()== "active" && j.IsPaused == false).ToListAsync();
//            if (!allActive.Any()) return;

//            // keep only those which are in the look back window(5 min)
//            var dueJobs = allActive.Where(j => IsJobDueInWindow(j, windowStart, now)).ToList();
//            if(!dueJobs.Any()) return;
//            foreach(var job in dueJobs)
//            {
//                if(job.Scheduler==null || job.ContactDetail == null) continue;
//                DateTime today= DateTime.UtcNow.Date;
//                var smsLogs= await dbContext.SmsDetails.Where(s=>s.ScheduledJobId==job.Id && s.createdOn>=today).ToListAsync();
//                bool sent = smsLogs.Any(s => s.smsDirection == "Outgoing");
//                bool failed = smsLogs.Any(s => s.smsDirection == "OutgoingFailed");
//                if (sent) continue;
//                await SendSmsAsync(job, now);
//            }
//            await dbContext.SaveChangesAsync();
//        }
//        private bool IsJobDueInWindow(AllScheduledJob job, DateTime windowStart, DateTime now)
//        {
//            if (job.Scheduler == null) return false;

//            string mode = job.Scheduler.onceOrRepeat?.ToLower() ?? "";

//            if (mode == "once")
//            {
//                return job.Scheduler.executionDateAndTime >= windowStart &&
//                       job.Scheduler.executionDateAndTime <= now;
//            }
//            if (mode == "repeat")
//            {
//                // Must be within the active date range
//                if (now.Date < job.JobBookDate.Date ||
//                    now.Date > job.JobCompletedDate.Date) return false;

//                // Build today's scheduled run time and check if it's in the window
//                DateTime todayRun = now.Date.Add(job.Scheduler.executionTime.TimeOfDay);
//                return todayRun >= windowStart && todayRun <= now;
//            }

//            return false;
//        }

//        private async System.Threading.Tasks.Task SendSmsAsync(AllScheduledJob job, DateTime now)
//        {
//            var contact = job.ContactDetail!;
//            var scheduler = job.Scheduler!;

//            string fromNum = (scheduler.fromNum ?? "").Trim();
//            string customerPhone = (contact.PhoneNumber ?? "").Trim();

//            if (fromNum.Length == 10) fromNum = "+1" + fromNum;
//            if (customerPhone.Length == 10) customerPhone = "+1" + customerPhone;

//            string finalMessage = BuildMessage(job, contact, now);

//            var schedulerImages = await dbContext.MmsLinkss
//                .Where(m => m.SchedulerId == scheduler.Id && m.SmsId == 0)
//                .ToListAsync();
//            try
//            {
//                int newSmsId = 0;

//                if (schedulerImages.Any())
//                {
//                    var mediaUrls = schedulerImages.Select(img => new Uri(img.Location)).ToList();

//                    if (!string.IsNullOrEmpty(accountSid))
//                    {
//                        MessageResource.Create(
//                            body: finalMessage,
//                            from: new Twilio.Types.PhoneNumber(fromNum),
//                            to: new Twilio.Types.PhoneNumber(customerPhone),
//                            mediaUrl: mediaUrls);
//                    }

//                    newSmsId = await SaveSmsLog(customerPhone, fromNum, finalMessage, "Outgoing", contact.Id, job.Id);

//                    foreach (var img in schedulerImages)
//                    {
//                        dbContext.MmsLinkss.Add(new MmsLinks
//                        {
//                            SmsId = newSmsId,
//                            FileName = img.FileName,
//                            Location = img.Location,
//                            SchedulerId = img.SchedulerId
//                        });
//                    }
//                }
//                else
//                {
//                    if (!string.IsNullOrEmpty(accountSid))
//                    {
//                        MessageResource.Create(
//                            body: finalMessage,
//                            from: new Twilio.Types.PhoneNumber(fromNum),
//                            to: new Twilio.Types.PhoneNumber(customerPhone));
//                    }

//                    newSmsId = await SaveSmsLog(customerPhone, fromNum, finalMessage, "Outgoing", contact.Id, job.Id);
//                }

//                // Mark once-job as done
//                if (scheduler.onceOrRepeat?.ToLower() == "once")
//                {
//                    scheduler.status = "sent";
//                    job.JobCompletedDate = now;
//                }

//                job.LastExecutedAt = now;
//            }
//            catch (Exception ex)
//            {
//                // OutgoingFailed log saved with ScheduledJobId
//                // Next tick will find this and retry (Case B)
//                await SaveSmsLog(
//                    customerPhone, fromNum,
//                    finalMessage + "\n[Error]: " + ex.Message,
//                    "OutgoingFailed",
//                    contact.Id,
//                    job.Id);
//            }
//        }
//        private string BuildMessage(AllScheduledJob job, ContactDetail contact, DateTime now)
//        {
//            string templatebody = job.Scheduler!.smsBody ?? "";
//            string customerName = contact.Name ?? "";
//            string customerEmail = contact.Email ?? "";
//            string customerAddr = contact.Address ?? "";
//            string bookDate = job.JobBookDate.ToString("dd-MMM-yyyy");
//            string completeDate = job.JobCompletedDate.ToString("dd-MMM-yyyy HH:mm");

//            string finalMessage = templatebody
//                .Replace("{name}", customerName)
//                .Replace("{email}", customerEmail)
//                .Replace("{address}", customerAddr)
//                .Replace("{jobBookDate}", bookDate)
//                .Replace("{jobCompletedDate}", completeDate);

//            if (!templatebody.Contains("{name}") && !string.IsNullOrEmpty(customerName))
//                finalMessage = customerName + " " + finalMessage;

//            return finalMessage;
//        }

//        private async System.Threading.Tasks.Task<int> SaveSmsLog(
//           string toNum, string frNum, string smsBody, string dir,
//           int? contactId = null, int? scheduledJobId = null)
//        {
//            SmsDetail conv = new SmsDetail();
//            conv.toNum = toNum;
//            conv.fromNum = frNum;
//            conv.smsBody = smsBody;
//            conv.smsDirection = dir;
//            conv.createdOn = DateTime.UtcNow;
//            conv.IsReadYesOrNo = "";
//            conv.imgPath = "";
//            conv.ContactDetailId = contactId;
//            conv.ScheduledJobId = scheduledJobId;  // links log to the scheduled job

//            dbContext.SmsDetails.Add(conv);
//            await dbContext.SaveChangesAsync();

//            return conv.Id;
//        }


//    }
//}
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




        // fetxh all active non-paused with job

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
                if (await IsAlreadySentToday(job)) continue;
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
            if (hourCount == -1)
            {
                hourCount = job.CustomHours ?? 0;
            }
            string mode = job.Scheduler.onceOrRepeat?.ToLower() ?? "";

            DateTime runFromDate;

            switch (job.Scheduler.RunFromId)
            {
                case 1: runFromDate = job.AssignedDate; break;  // Today = job assign date
                case 2: runFromDate = job.CustomDate ?? job.AssignedDate; break;
                case 3: runFromDate = job.JobBookDate; break;
                case 4: // Job Created Date
                    runFromDate = job.JobCompletedDate;
                    break;
                default: return false;
            }


            if (hourCount == 0) return true;

            DateTime scheduledTime = runFromDate.AddHours(hourCount);
            return scheduledTime >= windowStart && scheduledTime <= now;
        }

        private async System.Threading.Tasks.Task SendSmsAsync(AllScheduledJob job, DateTime now)
        {
            var contact = job.ContactDetail!;
            var scheduler = job.Scheduler!;

            string fromNum = (scheduler.fromNum ?? "").Trim();
            string customerPhone = (contact.PhoneNumber ?? "").Trim();

            if (fromNum.Length == 10) fromNum = "+1" + fromNum;
            if (customerPhone.Length == 10) customerPhone = "+1" + customerPhone;

            string finalMessage = BuildMessage(job, contact, now);

            var schedulerImages = await dbContext.MmsLinkss
                .Where(m => m.SchedulerId == scheduler.Id && m.SmsId == 0)
                .ToListAsync();
            try
            {
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
                // OutgoingFailed log saved with ScheduledJobId
                // Next tick will find this and retry (Case B)
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
            string bookTime = job.JobBookDate.ToString("hh:mm tt");
            string completeDate = job.JobCompletedDate.ToString("dd-MMM-yyyy HH:mm");
            var category = dbContext.IvrOption
        .FirstOrDefault(x => x.Id == job.Scheduler.templateOrAudioId);
            string categoryName = category?.keyword ?? "";

            string finalMessage = templatebody
                .Replace("{name}", customerName)
                .Replace("{email}", customerEmail)
                .Replace("{address}", customerAddr)
                .Replace("{jobBookDate}", bookDate)
                .Replace("{jobBookedTime}", bookTime)
                .Replace("{jobCompletedDate}", completeDate)
                .Replace("{category}", categoryName);

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
            conv.ScheduledJobId = scheduledJobId;  // links log to the scheduled job

            dbContext.SmsDetails.Add(conv);
            await dbContext.SaveChangesAsync();

            return conv.Id;
        }

        private DateTime GetTime()
        {
            //TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TmZn);
            DateTime usEastCoastNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);

            return usEastCoastNow;
        }

    }
}