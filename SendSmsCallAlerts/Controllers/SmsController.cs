using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Win32;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Migrations;
using SendSmsCallAlerts.Models;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Mail;
using System.Xml.Linq;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.Message;
using Twilio.TwiML.Voice;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class SmsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly string filePath;
        private readonly ApplicationDbContext _context;

        private readonly HttpClient _httpClient;

        public SmsController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment,
            IHttpClientFactory httpClientFactory, ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "data.txt");
            _httpClient = httpClientFactory.CreateClient();
            _context = dbContext;
        }

        public IActionResult IncomingLogs()
        {
            return View();
        }

        private async System.Threading.Tasks.Task GetMMSLink(string sid)
        {
            var media = MediaResource.Read(
              pathMessageSid: sid,
              limit: 20
          );

            await ViewMedia(media.First().Sid, media.First().Uri, media.First().ContentType);
        }

        public async System.Threading.Tasks.Task ViewMedia(string mediaSid, string medUrl, string contentTp)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

            string mediaUrl = await GetMediaUrl(accountSid, authToken, mediaSid, medUrl);

            var contentType = contentTp;

            var filePath = GetMediaFileName(mediaUrl, contentType);
            await DownloadUrlToFileAsync(mediaUrl, filePath, accountSid, authToken);

            if (mediaUrl != null)
            {
                await DownloadAndDisplayMedia(mediaUrl, accountSid, authToken);
            }
        }

        private string GetMediaFileName(string mediaUrl, string contentType)
        {
            return Path.Combine(_hostingEnvironment.ContentRootPath, Path.GetFileName(mediaUrl) +
                GetDefaultExtension(contentType));
        }

        private static async System.Threading.Tasks.Task DownloadUrlToFileAsync(string mediaUrl,
            string filePath, string accountSid, string authToken)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}")));

                var response = await client.GetAsync(mediaUrl);
                var httpStream = await response.Content.ReadAsStreamAsync();
                using (var fileStream = System.IO.File.Create(filePath))
                {
                    await httpStream.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                }
            }
        }

        public static string GetDefaultExtension(string mimeType)
        {
            var key = Registry.ClassesRoot.OpenSubKey(
                @"MIME\Database\Content Type\" + mimeType, false);
            var ext = key?.GetValue("Extension", null)?.ToString();
            return ext ?? "application/octet-stream";
        }

        static async System.Threading.Tasks.Task DownloadAndDisplayMedia(string mediaUrl, string accountSid, string authToken)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
               "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}")));

                HttpResponseMessage response = await client.GetAsync(mediaUrl);

                if (response.IsSuccessStatusCode)
                {
                    string filePath = "downloaded_media.jpg";
                    await System.IO.File.WriteAllBytesAsync(filePath, await response.Content.ReadAsByteArrayAsync());
                    Console.WriteLine($"Media downloaded and saved to: {filePath}");
                }
                else
                {
                    Console.WriteLine($"Failed to download media: {response.ReasonPhrase}");
                }
            }
        }

        static async Task<string> GetMediaUrl(string accountSid, string authToken, string mediaSid, string medUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}")));

                string apiUrl = $"https://api.twilio.com" + medUrl + "";

                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseData = await response.Content.ReadAsStringAsync();
                    string mediaUrl = responseData.Split("\"uri\":")[1].Split("\"")[1];
                    return $"https://api.twilio.com{mediaUrl}";
                }
                else
                {
                    Console.WriteLine($"Failed to fetch media: {response.ReasonPhrase}");
                    return null;
                }
            }
        }

        private async Task<FileStreamResult> StreamMedia(string mediaUrl, string accountSid, string authToken)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}")));

                    HttpResponseMessage response = await httpClient.GetAsync(mediaUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        System.IO.Stream stream = await response.Content.ReadAsStreamAsync();
                        return new FileStreamResult(stream, response.Content.Headers.ContentType.MediaType);
                    }
                    else
                    {
                        Console.WriteLine($"Error fetching media: {response.StatusCode} - {response.ReasonPhrase}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while fetching media: {ex.Message}");
                return null;
            }
        }

        private string GetTwilioNumber()
        {
            return RetrieveData();
        }

        public string RetrieveData()
        {
            try
            {
                string usrRole = HttpContext.Session.GetString("UserRole");
                if (usrRole == "ADMIN") return null;

                int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                string ph = _context.User.Where(r => r.Id == uId).First().Phone;
                if (ph == null || ph == "") return "N/A";
                return ph;
            }
            catch (Exception ex)
            {
                return "Error";
            }
        }

        public IActionResult OpenAttachments(int id)
        {
            ViewBag.SmsBody = _context.SmsDetails.Where(r => r.Id == id).First().smsBody;
            var mmsList = _context.MmsLinkss.Where(r => r.SmsId == id).ToList();
            return View(mmsList);
        }

        public async Task<ActionResult> DownloadAttachment(string path)
        {
            string WebAddress = _configuration["AppSettings:WebAddress"];
            path = _hostingEnvironment.WebRootPath + "/" + path;
            return File(path, "application/octet-stream", "TempFileName");
        }

        public IActionResult DownloadMMSFileDirect(string fileName)
        {
            string filePath = Path.Combine(_hostingEnvironment.WebRootPath, fileName);

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            string fileExtension = Path.GetExtension(filePath);
            string suggestedFileName = string.IsNullOrEmpty(fileName) ? "downloaded_file" : fileName;

            return File(System.IO.File.ReadAllBytes(filePath), "application/octet-stream", suggestedFileName + fileExtension);
        }

        public IActionResult _DownloadMMSFileDirect(string fileName)
        {
            string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "downloads", "ME4ee908d01e24e9ce9e1055ba815fcad4_07_25_07_.jpg");

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            return File(System.IO.File.ReadAllBytes(filePath), "application/octet-stream", fileName);
        }

        public async Task<IActionResult> DownloadMMSFile(string filePath = "")
        {
            filePath = Path.Combine(_hostingEnvironment.WebRootPath, "downloads", "ME4ee908d01e24e9ce9e1055ba815fcad4_07_25_07_.jpg");

            string fileName = Path.GetFileName(filePath);

            Response.Clear();
            Response.ContentType = "application/octet-stream";
            Response.Headers.Add("Content-Disposition", "attachment; filename=" + fileName);
            Response.Headers.Add("Content-Length", new FileInfo(filePath).Length.ToString());

            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(Response.Body);
            }

            return new FileStreamResult(Response.Body, "application/octet-stream")
            {
                FileDownloadName = fileName
            };
        }

        private string ConvertFileStreamResultToBase64(FileStreamResult fileStreamResult)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                fileStreamResult.FileStream.CopyTo(memoryStream);
                byte[] byteArray = memoryStream.ToArray();
                return Convert.ToBase64String(byteArray);
            }
        }

        public async Task<string> DeleteMsg(int id)
        {
            var ivrOptions = await _context.SmsDetails.FindAsync(id);
            if (ivrOptions != null)
            {
                var links = _context.MmsLinkss.Where(r => r.SmsId == id).ToList();

                if (links != null && links.Count() > 0)
                {
                    foreach (var lnk in links)
                    {
                        DeleteMdFileUsingLink(lnk.FileName);
                    }
                }

                _context.MmsLinkss.RemoveRange(links);
                _context.SmsDetails.Remove(ivrOptions);
            }

            await _context.SaveChangesAsync();
            return "success";
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (_context.IvrOption == null)
            {
                return Problem("Entity set 'ApplicationDbContext.IvrOption'  is null.");
            }
            var ivrOptions = await _context.SmsDetails.FindAsync(id);
            if (ivrOptions != null)
            {
                var links = _context.MmsLinkss.Where(r => r.SmsId == id).ToList();

                if (links != null && links.Count() > 0)
                {
                    foreach (var lnk in links)
                    {
                        DeleteMdFileUsingLink(lnk.FileName);
                    }
                }

                _context.MmsLinkss.RemoveRange(links);
                _context.SmsDetails.Remove(ivrOptions);
            }

            await _context.SaveChangesAsync();

            TempData["SucSms"] = "Message deleted successfully!";
            return RedirectToAction("IncomingLogs");
        }

        public string DeleteMdFileUsingLink(string location = "")
        {
            string fullPath = Path.Combine(_hostingEnvironment.WebRootPath, location);

            try
            {
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    return "File deleted successfully.";
                }
                else
                {
                    return "File does not exist at the specified location.";
                }
            }
            catch (Exception ex)
            {
                return $"Error deleting file: {ex.Message}";
            }
        }

        // GET CATEGORIES FOR DROPDOWN
        [HttpGet]
        public IActionResult GetCategories()
        {
            var categories = _context.IvrOption
                .Select(x => x.keyword)
                .Distinct()
                .ToList();

            return Json(categories);
        }

        // ✅ FIXED: GET SCHEDULERS - 
        [HttpGet]
        public IActionResult GetSchedulers()
        {
            var schedulers = _context.Schedulers
                .Include(s => s.TimeToRun)
                .Select(s => new
                {
                    s.Id,
                    s.name,
                    RunFromId = s.RunFromId ?? 0,
                    TimeToRunId = s.TimeToRunId ?? 0,
                    HourCount = s.TimeToRun != null ? s.TimeToRun.HourCount : 0  // ADD

                })
                .ToList();

            return Json(schedulers);
        }

        // DELETE LOGS BY DATE RANGE
        [HttpPost]
        public async Task<IActionResult> DeleteLogsByDate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var endOfToDate = toDate.Date.AddDays(1).AddTicks(-1);
                var recordsToDelete = _context.SmsDetails
                    .Where(s => s.createdOn >= fromDate.Date && s.createdOn <= endOfToDate);

                int count = await recordsToDelete.CountAsync();
                if (count > 0)
                {
                    _context.SmsDetails.RemoveRange(recordsToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DOWNLOAD CSV BY DATE RANGE
        [HttpPost]
        public async Task<IActionResult> DownloadLogsByDate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var endOfToDate = toDate.Date.AddDays(1).AddTicks(-1);

                var logs = await (from sms in _context.SmsDetails
                                  where sms.createdOn >= fromDate.Date && sms.createdOn <= endOfToDate
                                  let phoneNumber = sms.smsDirection == "Incoming" ? sms.fromNum : sms.toNum
                                  join contact in _context.ContactDetail on phoneNumber equals contact.PhoneNumber into contactJoin
                                  from c in contactJoin.DefaultIfEmpty()
                                  join job in _context.JobSchedule on (c != null && sms.smsDirection == "Incoming" ? c.Id : -1) equals job.ContactDetailId into jobJoin
                                  from j in jobJoin.DefaultIfEmpty()
                                  select new
                                  {
                                      Id = sms.Id,
                                      Date = sms.createdOn.ToString("MM-dd-yyyy HH:mm"),
                                      From = sms.fromNum,
                                      Body = (sms.smsBody ?? "").Replace(",", " ").Replace("\r", " ").Replace("\n", " "),
                                      Name = c != null ? c.Name : "N/A",
                                      Category = (j != null && sms.smsDirection == "Incoming") ? j.Category : "N/A",
                                      Scheduler = (j != null && sms.smsDirection == "Incoming") ? j.Scheduler : "N/A"
                                  }).ToListAsync();

                var logIds = logs.Select(l => l.Id).ToList();
                var allAttachements = new List<MmsLinks>();
                if (logIds.Any())
                {
                    allAttachements = await _context.MmsLinkss.Where(r => logIds.Contains(r.SmsId)).ToListAsync();
                }

                var builder = new System.Text.StringBuilder();
                builder.AppendLine("Date,From,Body,Attachments,Name,Category,Scheduler");

                foreach (var log in logs)
                {
                    var smsAttachments = allAttachements.Where(m => m.SmsId == log.Id).Select(m => m.Location).ToList();
                    string attachmentsString = smsAttachments.Any() ? $"\"{string.Join(", ", smsAttachments)}\"" : "N/A";
                    builder.AppendLine($"{log.Date},{log.From},{log.Body},{attachmentsString},{log.Name},{log.Category},{log.Scheduler}");
                }

                var csvBytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
                string fileName = $"SMS_Logs_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.csv";

                return File(csvBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Scheduled Jobs Page
        public IActionResult ScheduledJobs()
        {
            return View();
        }

        // Get Scheduled Jobs Data for DataTable
        [HttpGet]
        public IActionResult GetScheduledJobsData()
        {
            try
            {
                var jobs = (from job in _context.AllScheduledJobs

                            join contact in _context.ContactDetail
                                on job.ContactDetailId equals contact.Id into contactJoin
                            from contact in contactJoin.DefaultIfEmpty()

                            join scheduler in _context.Schedulers
                                on job.SchedulerId equals scheduler.Id into schedulerJoin
                            from scheduler in schedulerJoin.DefaultIfEmpty()

                            select new
                            {
                                jobId = job.Id,
                                phoneNumber = contact != null ? (contact.PhoneNumber ?? "N/A") : "N/A",
                                name = contact != null ? (contact.Name ?? "N/A") : "N/A",
                                schedulerName = scheduler != null ? (scheduler.name ?? "N/A") : "N/A",
                                onceOrRepeat = scheduler != null ? (scheduler.onceOrRepeat ?? "N/A") : "N/A",
                                jobBookDate = job.JobBookDate.ToString("dd-MMM-yyyy"),
                                jobCompletedDate = job.JobCompletedDate.ToString("dd-MMM-yyyy hh:mm tt"),
                                status = scheduler != null ? (scheduler.status ?? "N/A") : "N/A",
                                isPaused = job.IsPaused,
                                lastExecutedAt = job.LastExecutedAt
                            }).OrderByDescending(x => x.jobId).ToList();

                return Json(new { aaData = jobs });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Toggle Pause/Resume for a Job
        [HttpPost]
        public async Task<IActionResult> TogglePauseJob(int jobId)
        {
            var job = await _context.AllScheduledJobs.FindAsync(jobId);
            if (job == null)
            {
                return Json(new { success = false, message = "Job not found." });
            }
            job.IsPaused = !job.IsPaused;
            _context.AllScheduledJobs.Update(job);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isPaused = job.IsPaused });
        }

        // Delete a Scheduled Job
        [HttpPost]
        public async Task<IActionResult> DeleteScheduledJob(int jobId)
        {
            var job = await _context.AllScheduledJobs.FindAsync(jobId);
            if (job != null)
            {
                _context.AllScheduledJobs.Remove(job);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Job not found." });
        }

        // GetIncomingLogs
        private async Task<List<SmsDetailsVm>> GetIncomingLogs(string num)
        {
            var rawData = await (from sms in _context.SmsDetails
                                 .Where(r => string.IsNullOrEmpty(num) || r.fromNum == num)

                                 join contact in _context.ContactDetail
                                     on (sms.ContactDetailId != null ? sms.ContactDetailId.Value : -1) equals contact.Id into contactByIdJoin
                                 from cById in contactByIdJoin.DefaultIfEmpty()

                                 let phoneToMatch = sms.smsDirection == "Incoming" ? sms.fromNum : sms.toNum
                                 join contactByPhone in _context.ContactDetail
                                     on phoneToMatch equals contactByPhone.PhoneNumber into contactByPhoneJoin
                                 from cByPhone in contactByPhoneJoin.DefaultIfEmpty()

                                 join mms in _context.MmsLinkss on sms.Id equals mms.SmsId into mmsJoin
                                 select new
                                 {
                                     sms = sms,
                                     contact = cById != null ? cById : cByPhone,
                                     attchmentsCount = mmsJoin.Count()
                                 }).ToListAsync();

            var contactIds = rawData
                .Where(r => r.contact != null)
                .Select(r => r.contact.Id)
                .Distinct()
                .ToList();

            var allJobs = await (from j in _context.AllScheduledJobs
                                 join s in _context.Schedulers on j.SchedulerId equals s.Id
                                 where contactIds.Contains(j.ContactDetailId ?? 0)
                                      
                                 select new { Job = j, Scheduler = s }).ToListAsync();

            var categories = await _context.IvrOption.ToListAsync();
            var result = new List<SmsDetailsVm>();

            foreach (var item in rawData)
            {
                bool isIncoming = item.sms.smsDirection == "Incoming";
                var contactJobs = allJobs
                    .Where(j => item.contact != null && j.Job.ContactDetailId == item.contact.Id)
                    .ToList();

                string displayName = item.contact?.Name ?? "N/A";
                string displayAddress = item.contact?.Address ?? "N/A";
                string displayEmail = item.contact?.Email ?? "N/A";

                if (contactJobs.Any())
                {
                    var categoryNames = contactJobs.Select(c =>
                    {
                        var cat = categories.FirstOrDefault(x => x.Id == c.Scheduler.templateOrAudioId);
                        return cat != null && !string.IsNullOrEmpty(cat.keyword) ? cat.keyword : "N/A";
                    }).ToList();

                    var schedulerNames = contactJobs
                        .Select(c => !string.IsNullOrEmpty(c.Scheduler.name) ? c.Scheduler.name : "N/A")
                        .ToList();

                    var bookDates = contactJobs
                        .Select(c => c.Job.JobBookDate == DateTime.MinValue ? "N/A" : c.Job.JobBookDate.ToString("dd/MM/yyyy"))
                        .ToList();
                    var bookTimes = contactJobs
                                .Select(c => c.Job.JobBookDate == DateTime.MinValue ? "N/A" : c.Job.JobBookDate.ToString("hh:mm tt"))
                                    .ToList();

                    result.Add(new SmsDetailsVm
                    {
                        Id = item.sms.Id,
                        fromNum = item.sms.fromNum,
                        toNum = item.sms.toNum,
                        smsBody = item.sms.smsBody,
                        smsDirection = item.sms.smsDirection,
                        IsReadYesOrNo = item.sms.IsReadYesOrNo,
                        dt = item.sms.createdOn.ToString("MM-dd-yy HH:mm"),
                        createdOn = item.sms.createdOn,
                        imgPath = item.sms.imgPath,
                        attchmentsCount = item.attchmentsCount,
                        name = displayName,
                        address = displayAddress,
                        email = displayEmail,
                        category = string.Join(", ", categoryNames),
                        scheduler = string.Join(", ", schedulerNames),
                        jobBookDatesList = string.Join(", ", bookDates),
                        jobBookTimesList = string.Join(", ", bookTimes),
                        JobBookDate = contactJobs.FirstOrDefault()?.Job.JobBookDate,
                        SchedulerId = contactJobs.FirstOrDefault()?.Scheduler.Id ?? 0
                    });
                }
                else
                {
                    result.Add(new SmsDetailsVm
                    {
                        Id = item.sms.Id,
                        fromNum = item.sms.fromNum,
                        toNum = item.sms.toNum,
                        smsBody = item.sms.smsBody,
                        smsDirection = item.sms.smsDirection,
                        IsReadYesOrNo = item.sms.IsReadYesOrNo,
                        dt = item.sms.createdOn.ToString("MM-dd-yy HH:mm"),
                        createdOn = item.sms.createdOn,
                        imgPath = item.sms.imgPath,
                        attchmentsCount = item.attchmentsCount,
                        name = displayName,
                        address = displayAddress,
                        email = displayEmail,
                        category = "N/A",
                        scheduler = "N/A",
                        jobBookDatesList = "N/A",
                        jobBookTimesList = "N/A",
                        JobBookDate = null,
                        SchedulerId = 0
                    });
                }
            }

            return result.OrderByDescending(x => x.createdOn).ToList();
        }

        // GetIvrOptions
        [HttpGet]
        public IActionResult GetIvrOptions()
        {
            var options = _context.IvrOption
                .Select(i => new
                {
                    i.Id,
                    i.keyword
                })
                .ToList();

            return Json(options);
        }

        public async System.Threading.Tasks.Task SaveAndSendSMS(Scheduler sch)
        {
            try
            {
                var locLst = _context.MmsLinkss.Where(r => r.SchedulerId == sch.Id).ToList();
                if (locLst.Count() > 0)
                {
                    var mediaUrl = new List<Uri>();
                    foreach (var rec in locLst)
                    {
                        mediaUrl.Add(new Uri(rec.Location));
                    }
                    int smsId = await SaveSmsLog(sch.toNum, sch.fromNum, sch.smsBody, "Outgoing");
                    foreach (var r in locLst)
                    {
                        r.SmsId = smsId;
                    }
                    await _context.SaveChangesAsync();
                }
                else
                {
                    await SaveSmsLog(sch.toNum, sch.fromNum, sch.smsBody, "Outgoing");
                }
            }
            catch (Exception e)
            {
                await SaveSmsLog(sch.toNum, sch.fromNum, sch.smsBody + "Error:" + e.ToString(), "OutgoingFailed");
            }
        }

        private async System.Threading.Tasks.Task<int> SaveSmsLog(string toNum, string fromNum, string body, string direction)
        {
            var newSms = new SmsDetail
            {
                toNum = toNum ?? "",
                fromNum = fromNum ?? "",
                smsBody = body ?? "",
                smsDirection = direction,
                createdOn = DateTime.Now,
                IsReadYesOrNo = "No"
            };
            _context.SmsDetails.Add(newSms);
            await _context.SaveChangesAsync();
            return newSms.Id;
        }

        // ✅ FIXED: UpdateSmsDetails 
        [HttpPost]
        public async Task<IActionResult> UpdateSmsDetails(SmsUpdateDto model)
        {
            try
            {
                string phoneNumber = model.fromNum;
                if (string.IsNullOrEmpty(phoneNumber)) return Json(new { success = false, message = "Phone number is required." });

                int categoryId = 0;
                if (!string.IsNullOrEmpty(model.category)) int.TryParse(model.category, out categoryId);

                var contact = await _context.ContactDetail.FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

                if (contact != null)
                {
                    contact.Name = model.name;
                    contact.Address = model.address;
                    contact.Email = model.email;
                    _context.ContactDetail.Update(contact);
                }
                else
                {
                    contact = new ContactDetail { Name = model.name, Address = model.address, Email = model.email, PhoneNumber = phoneNumber };
                    _context.ContactDetail.Add(contact);
                    await _context.SaveChangesAsync();
                }

                var smsToUpdate = await _context.SmsDetails.Where(s => s.fromNum == phoneNumber).ToListAsync();
                foreach (var sms in smsToUpdate) { sms.ContactDetailId = contact.Id; }
                _context.SmsDetails.UpdateRange(smsToUpdate);

                if (model.smsDirection == "Outgoing")
                {
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, isOutgoing = true, message = "Contact details updated successfully." });
                }

                if (model.schedulerId.HasValue && model.schedulerId.Value > 0)
                {
                    var scheduler = await _context.Schedulers.FindAsync(model.schedulerId.Value);
                    if (scheduler != null && categoryId > 0)
                    {
                        scheduler.templateOrAudioId = categoryId;
                        _context.Schedulers.Update(scheduler);
                    }

                    var newScheduledJob = new AllScheduledJob
                    {
                        ContactDetailId = contact.Id,
                        SchedulerId = model.schedulerId.Value,
                        AssignedDate = GetTime(),
                        JobBookDate = model.JobBookDate ?? DateTime.Now,
                        JobCompletedDate = model.JobCompletedDate ?? DateTime.Now,
                        CustomDate = model.CustomDate,
                        CustomHours = model.CustomHours
                    };
                    _context.AllScheduledJobs.Add(newScheduledJob);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, isOutgoing = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GetContactDetailsByPhone
        [HttpGet]
        public async Task<IActionResult> GetContactDetailsByPhone(string phoneNumber)
        {
            try
            {
                var contact = await _context.ContactDetail
                    .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

                if (contact == null)
                {
                    return Json(new { success = true, found = false });
                }

                var activeJobs = await (from j in _context.AllScheduledJobs
                                        join s in _context.Schedulers on j.SchedulerId equals s.Id
                                        where j.ContactDetailId == contact.Id
                                        select new
                                        {
                                            jobId = j.Id,
                                            schedulerName = s.name
                                        }).ToListAsync();

                return Json(new
                {
                    success = true,
                    found = true,
                    name = contact.Name ?? "",
                    email = contact.Email ?? "",
                    address = contact.Address ?? "",
                    activeJobs = activeJobs
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Delete Scheduler
        [HttpPost]
        public async Task<IActionResult> DeleteScheduler(int schedulerId)
        {
            try
            {
                var scheduler = await _context.Schedulers.FindAsync(schedulerId);
                if (scheduler == null)
                {
                    return Json(new { success = false, message = "Scheduler not found." });
                }

                string phoneNumber = scheduler.toNum;

                _context.Schedulers.Remove(scheduler);
                await _context.SaveChangesAsync();

                return Json(new { success = true, phoneNumber = phoneNumber });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GetData
        public async Task<JsonResult> GetData()
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];
            TwilioClient.Init(accountSid, authToken);

            List<SmsDetailsVm> stats = await GetIncomingLogs(GetTwilioNumber());

            if (stats == null)
            {
                return Json(new
                {
                    aaData = new List<Models.Conversation>()
                });
            }

            var serializedStats = stats.Select(conversation => new
            {
                conversation.Id,
                conversation.fromNum,
                conversation.toNum,
                conversation.smsBody,
                conversation.smsDirection,
                conversation.IsReadYesOrNo,
                conversation.dt,
                conversation.createdOn,
                conversation.imgPath,
                conversation.attchmentsCount,
                conversation.name,
                conversation.address,
                conversation.email,
                conversation.category,
                conversation.scheduler,
                conversation.jobBookDatesList,
                conversation.jobBookTimesList,
                conversation.JobBookDate,
                conversation.SchedulerId
            });

            return Json(new
            {
                aaData = serializedStats
            });
        }

        private DateTime GetTime()
        {
            string TmZn = _configuration["AppSettings:TmZn"];
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TmZn);
            DateTime usEastCoastNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);
            return usEastCoastNow;
        }

    }
}