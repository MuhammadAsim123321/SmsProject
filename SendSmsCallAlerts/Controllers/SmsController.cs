//using Hangfire.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.Message;

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
        private readonly JobService _jobService;
        private readonly IAppClock _clock;

        public SmsController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment,
            IHttpClientFactory httpClientFactory, ApplicationDbContext dbContext,
            JobService jobService, IAppClock clock)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "data.txt");
            _httpClient = httpClientFactory.CreateClient();
            _context = dbContext;
            _jobService = jobService;
            _clock = clock;
        }

        public IActionResult IncomingLogs()
        {
            return View();
        }

        // =====================================================================
        // MEDIA HELPERS (unchanged)
        // =====================================================================
        private async System.Threading.Tasks.Task GetMMSLink(string sid)
        {
            var media = MediaResource.Read(pathMessageSid: sid, limit: 20);
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
            catch (Exception)
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

        // =====================================================================
        // MESSAGE DELETE (unchanged)
        // =====================================================================
        public async Task<string> DeleteMsg(int id)
        {
            var sms = await _context.SmsDetails.FindAsync(id);
            if (sms != null)
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
                _context.SmsDetails.Remove(sms);
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
            var sms = await _context.SmsDetails.FindAsync(id);
            if (sms != null)
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
                _context.SmsDetails.Remove(sms);
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

        // =====================================================================
        // LOOKUPS FOR THE MODAL
        // =====================================================================
        [HttpGet]
        public IActionResult GetCategories()
        {
            var categories = _context.IvrOption
                .Select(x => x.keyword)
                .Distinct()
                .ToList();

            return Json(categories);
        }

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
                    HourCount = s.TimeToRun != null ? s.TimeToRun.HourCount : 0
                })
                .ToList();

            return Json(schedulers);
        }

        [HttpGet]
        public IActionResult GetIvrOptions()
        {
            var options = _context.IvrOption.AsNoTracking()
                .Select(i => new { i.Id, i.keyword })
                .ToList()
                .Select(i => new
                {
                    i.Id,
                    i.keyword,
                    asksDonorCarpet = JobService.IsDonorCarpetCategory(i.keyword)
                });

            return Json(options);
        }

        // =====================================================================
        // LOG DELETE / CSV EXPORT BY DATE
        // =====================================================================
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

        /// <summary>CSV export. Category / Scheduler now come from the customer's real jobs.</summary>
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
                                  select new
                                  {
                                      Id = sms.Id,
                                      CreatedOn = sms.createdOn,
                                      From = sms.fromNum,
                                      Direction = sms.smsDirection,
                                      Body = (sms.smsBody ?? "").Replace(",", " ").Replace("\r", " ").Replace("\n", " "),
                                      ContactId = c != null ? (int?)c.Id : null,
                                      Name = c != null ? c.Name : "N/A"
                                  }).ToListAsync();

                var contactIds = logs.Where(l => l.ContactId.HasValue).Select(l => l.ContactId!.Value).Distinct().ToList();
                var jobs = await _jobService.LoadJobsForContactsAsync(contactIds);
                var jobsByContact = jobs.GroupBy(j => j.ContactDetailId).ToDictionary(g => g.Key, g => g.ToList());

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

                    string category = "N/A", scheduler = "N/A";
                    if (log.Direction == "Incoming" && log.ContactId.HasValue
                        && jobsByContact.TryGetValue(log.ContactId.Value, out var cj) && cj.Count > 0)
                    {
                        category = string.Join("; ", cj.Select(j => j.Category?.keyword ?? "N/A"));
                        scheduler = string.Join("; ", cj.Select(j => j.ScheduledJobs.FirstOrDefault()?.Scheduler?.name ?? "N/A"));
                    }

                    builder.AppendLine($"{log.CreatedOn:MM-dd-yyyy HH:mm},{log.From},{log.Body},{attachmentsString},{log.Name},{category},{scheduler}");
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

        // =====================================================================
        // TECHNICIAN JOBS PAGE (sorted by technician, with customer + job details)
        // =====================================================================
        public IActionResult TechnicianJobs()
        {
            return View();
        }

        /// <summary>
        /// Returns all jobs grouped by technician, sorted by technician name then job start time.
        /// Includes customer details, quoted amount, distance and job order number.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTechnicianJobsData(string? status, string? dateFrom, string? dateTo)
        {
            try
            {
                var query = _context.Jobs.AsNoTracking()
                    .Include(j => j.ContactDetail)
                    .Include(j => j.Technician)
                    .Include(j => j.Category)
                    .AsQueryable();

                // optional date filter
                if (DateTime.TryParse(dateFrom, out var dFrom))
                    query = query.Where(j => j.StartAt >= dFrom.Date);
                if (DateTime.TryParse(dateTo, out var dTo))
                    query = query.Where(j => j.StartAt < dTo.Date.AddDays(1));

                var jobs = await query
                    .OrderBy(j => j.Technician != null ? j.Technician.FirstName : "")
                    .ThenBy(j => j.Technician != null ? j.Technician.LastName : "")
                    .ThenBy(j => j.StartAt)
                    .ToListAsync();

                var now = _clock.BusinessNow;

                // apply status filter after effective-status calculation
                var rows = jobs
                    .Select(j => new
                    {
                        jobId         = j.Id,
                        technicianId  = j.TechnicianId,
                        technicianName = j.Technician?.FullName ?? "Unassigned",
                        customerName  = j.ContactDetail?.Name ?? "N/A",
                        phoneNumber   = j.ContactDetail?.PhoneNumber ?? "N/A",
                        address       = j.ContactDetail?.Address ?? "N/A",
                        distanceMiles = j.DistanceMiles.HasValue
                                        ? Math.Round(j.DistanceMiles.Value, 1).ToString("F1") + " mi"
                                        : "N/A",
                        distanceRaw   = j.DistanceMiles,
                        quotedAmount  = j.QuotedAmount.HasValue
                                        ? j.QuotedAmount.Value.ToString("F2")
                                        : "",
                        category      = j.Category?.keyword ?? "N/A",
                        startAt       = j.StartAt.ToString("MM/dd/yyyy hh:mm tt"),
                        endAt         = j.EndAt.ToString("hh:mm tt"),
                        startDate     = j.StartAt.ToString("MM/dd/yyyy"),
                        startTime     = j.StartAt.ToString("hh:mm tt"),
                        effectiveStatus = j.GetEffectiveStatus(now),
                        storedStatus  = j.Status
                    })
                    .Where(r => string.IsNullOrEmpty(status) || status == "All" || r.effectiveStatus == status)
                    .ToList();

                return Json(new { aaData = rows });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // =====================================================================
        // SCHEDULED JOBS PAGE (jobs with their message plans)
        // =====================================================================
        public IActionResult ScheduledJobs()
        {
            return View();
        }

        /// <summary>One row per message plan; a job without a plan still shows as one row.</summary>
        [HttpGet]
        public async Task<IActionResult> GetScheduledJobsData()
        {
            try
            {
                var jobs = await _context.Jobs.AsNoTracking()
                    .Include(j => j.ContactDetail)
                    .Include(j => j.Technician)
                    .Include(j => j.Category)
                    .Include(j => j.ScheduledJobs).ThenInclude(p => p.Scheduler)
                    .OrderByDescending(j => j.Id)
                    .ToListAsync();

                var now = _clock.BusinessNow;
                var rows = new List<object>();

                foreach (var j in jobs)
                {
                    string effective = j.GetEffectiveStatus(now);
                    var plans = j.ScheduledJobs.OrderBy(p => p.Id).Select(p => (AllScheduledJob?)p).ToList();
                    if (plans.Count == 0) plans.Add(null);

                    foreach (var p in plans)
                    {
                        bool once = string.Equals(p?.Scheduler?.onceOrRepeat, "Once", StringComparison.OrdinalIgnoreCase);
                        bool isSent = p != null && once && p.LastExecutedAt != null;

                        string status;
                        if (effective == JobStatus.Cancelled) status = "Cancelled";
                        else if (effective == JobStatus.Completed) status = "Completed";
                        else if (p == null) status = effective;               // Scheduled / Confirmed
                        else if (p.IsPaused) status = "Paused";
                        else if (isSent) status = "Sent";
                        else status = "Active";

                        rows.Add(new
                        {
                            jobId = j.Id,
                            planId = p?.Id,
                            phoneNumber = j.ContactDetail?.PhoneNumber ?? "N/A",
                            name = j.ContactDetail?.Name ?? "N/A",
                            category = j.Category?.keyword ?? "N/A",
                            technician = j.Technician?.FullName ?? "N/A",
                            schedulerName = p?.Scheduler?.name ?? "N/A",
                            onceOrRepeat = p?.Scheduler?.onceOrRepeat ?? "N/A",
                            startAt = j.StartAt.ToString("dd-MMM-yyyy hh:mm tt"),
                            endAt = j.EndAt.ToString("dd-MMM-yyyy hh:mm tt"),
                            lastExecutedAt = p?.LastExecutedAt,
                            status,
                            isPaused = p?.IsPaused ?? false,
                            isSent
                        });
                    }
                }

                return Json(new { aaData = rows });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TogglePausePlan(int planId)
        {
            var plan = await _context.AllScheduledJobs.FindAsync(planId);
            if (plan == null) return Json(new { success = false, message = "Scheduled message not found." });

            plan.IsPaused = !plan.IsPaused;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isPaused = plan.IsPaused });
        }

        /// <summary>Removes the message plan only. The job (appointment) stays; cancel it from the Incoming SMS modal.</summary>
        [HttpPost]
        public async Task<IActionResult> DeletePlan(int planId)
        {
            var plan = await _context.AllScheduledJobs.FindAsync(planId);
            if (plan == null) return Json(new { success = false, message = "Scheduled message not found." });

            _context.AllScheduledJobs.Remove(plan);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // =====================================================================
        // INCOMING SMS GRID
        // =====================================================================
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

            var jobs = await _jobService.LoadJobsForContactsAsync(contactIds);
            var jobsByContact = jobs.GroupBy(j => j.ContactDetailId).ToDictionary(g => g.Key, g => g.ToList());

            // response-time matching: first outgoing reply to the same customer after the incoming message
            var allOutgoing = await _context.SmsDetails
                .Where(s => s.smsDirection == "Outgoing")
                .Select(s => new { s.toNum, s.createdOn })
                .ToListAsync();

            var result = new List<SmsDetailsVm>();

            foreach (var item in rawData)
            {
                bool isIncoming = item.sms.smsDirection == "Incoming";
                var contactJobs = item.contact != null && jobsByContact.TryGetValue(item.contact.Id, out var found)
                    ? found
                    : new List<Job>();

                DateTime? respondedOn = null;
                double? waitingMinutes = null;
                if (isIncoming)
                {
                    var customerNum = item.sms.fromNum;
                    var incomingTime = item.sms.createdOn;
                    var reply = allOutgoing
                        .Where(o => o.toNum == customerNum && o.createdOn > incomingTime)
                        .OrderBy(o => o.createdOn)
                        .FirstOrDefault();

                    if (reply != null)
                    {
                        respondedOn = reply.createdOn;
                        waitingMinutes = (reply.createdOn - incomingTime).TotalMinutes;
                    }
                }

                var vm = new SmsDetailsVm
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
                    name = item.contact?.Name ?? "N/A",
                    address = item.contact?.Address ?? "N/A",
                    streetAddress = item.contact?.StreetAddress ?? "N/A",
                    cityState = item.contact?.CityState ?? "N/A",
                    zipCode = item.contact?.ZipCode ?? "N/A",
                    email = item.contact?.Email ?? "N/A",
                    RespondedOn = respondedOn,
                    WaitingMinutes = waitingMinutes
                };

                if (contactJobs.Any())
                {
                    var first = contactJobs[0];
                    vm.category = string.Join(", ", contactJobs.Select(j => j.Category?.keyword ?? "N/A"));
                    vm.scheduler = string.Join(", ", contactJobs.Select(j => j.ScheduledJobs.FirstOrDefault()?.Scheduler?.name ?? "N/A"));
                    vm.jobBookDatesList = string.Join(", ", contactJobs.Select(j => j.StartAt.ToString("dd/MM/yyyy")));
                    vm.jobBookTimesList = string.Join(", ", contactJobs.Select(j => j.StartAt.ToString("hh:mm tt")));
                    vm.JobBookDate = first.StartAt;
                    vm.SchedulerId = first.ScheduledJobs.FirstOrDefault()?.SchedulerId ?? 0;
                }
                else
                {
                    vm.category = "N/A";
                    vm.scheduler = "N/A";
                    vm.jobBookDatesList = "N/A";
                    vm.jobBookTimesList = "N/A";
                    vm.JobBookDate = null;
                    vm.SchedulerId = 0;
                }

                result.Add(vm);
            }

            return result.OrderByDescending(x => x.createdOn).ToList();
        }

        public async Task<JsonResult> GetData()
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];
            if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken))
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
                conversation.streetAddress,
                conversation.cityState,
                conversation.zipCode,
                conversation.email,
                conversation.category,
                conversation.scheduler,
                conversation.jobBookDatesList,
                conversation.jobBookTimesList,
                conversation.JobBookDate,
                conversation.SchedulerId,
                incomingDate = conversation.smsDirection == "Incoming"
                    ? conversation.createdOn.ToString("MM-dd-yyyy") : "",
                incomingTime = conversation.smsDirection == "Incoming"
                    ? conversation.createdOn.ToString("hh:mm:ss tt") : "",
                respondedDate = conversation.RespondedOn.HasValue
                    ? conversation.RespondedOn.Value.ToString("MM-dd-yyyy") : "",
                respondedTime = conversation.RespondedOn.HasValue
                    ? conversation.RespondedOn.Value.ToString("hh:mm:ss tt") : "",
                waitingDisplay = conversation.WaitingMinutes.HasValue
                    ? FormatWaitingTime(conversation.WaitingMinutes.Value) : (conversation.smsDirection == "Incoming" ? "Pending" : ""),
                waitingColor = conversation.WaitingColor
            });

            return Json(new
            {
                aaData = serializedStats
            });
        }

        // =====================================================================
        // MODAL: LOAD + SAVE (all rules live in JobService)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> GetContactDetailsByPhone(string phoneNumber, int? smsId = null)
        {
            try
            {
                // If an incoming smsId is given, use the message-centric lookup
                // (returns the job linked via SourceSmsId + all jobs for history).
                if (smsId.HasValue && smsId.Value > 0)
                {
                    var view = await _jobService.GetContactViewBySmsAsync(smsId.Value);
                    return Json(view);
                }

                // Fallback: outgoing rows (no smsId) load by phone number
                var viewByPhone = await _jobService.GetContactViewAsync(phoneNumber);
                return Json(viewByPhone);
            }
            catch (Exception ex)
            {
                return Json(new { found = false, error = ex.Message });
            }
        }

        /// <summary>Returns all jobs for a customer (by phone), newest first — drives the history popup.</summary>
        [HttpGet]
        public async Task<IActionResult> GetJobHistory(string phoneNumber)
        {
            try
            {
                var view = await _jobService.GetContactViewAsync(phoneNumber);
                return Json(new { success = true, jobs = view.Jobs });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>The single Update button: contact + job + message plan, all or nothing.</summary>
        [HttpPost]
        public async Task<IActionResult> SaveContactJob([FromBody] SaveContactJobRequest request)
        {
            if (request == null)
                return Json(new SaveResult { Success = false, Message = "Invalid request." });

            var result = await _jobService.SaveAsync(request);
            return Json(result);
        }

        // =====================================================================
        // OTHER
        // =====================================================================
        public async System.Threading.Tasks.Task SaveAndSendSMS(Scheduler sch)
        {
            try
            {
                var locLst = _context.MmsLinkss.Where(r => r.SchedulerId == sch.Id).ToList();
                if (locLst.Count() > 0)
                {
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

        private async System.Threading.Tasks.Task<int> SaveSmsLog(string? toNum, string? fromNum, string body, string direction)
        {
            var newSms = new SmsDetail
            {
                toNum = toNum ?? "",
                fromNum = fromNum ?? "",
                smsBody = body ?? "",
                smsDirection = direction,
                createdOn = _clock.UtcNow,
                IsReadYesOrNo = "No"
            };
            _context.SmsDetails.Add(newSms);
            await _context.SaveChangesAsync();
            return newSms.Id;
        }

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

                string? phoneNumber = scheduler.toNum;

                _context.Schedulers.Remove(scheduler);
                await _context.SaveChangesAsync();

                return Json(new { success = true, phoneNumber = phoneNumber });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static string FormatWaitingTime(double totalMinutes)
        {
            int minutes = (int)Math.Floor(totalMinutes);
            int seconds = (int)Math.Round((totalMinutes - minutes) * 60);
            return $"{minutes}m {seconds}s";
        }
    }
}