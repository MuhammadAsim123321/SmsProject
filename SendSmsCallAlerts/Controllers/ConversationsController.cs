using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Migrations;
using SendSmsCallAlerts.Models;
using System.Xml.Linq;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class ConversationsController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        private readonly string filePath;
        private readonly ApplicationDbContext _context;

        public ConversationsController(IConfiguration configuration, IWebHostEnvironment hostingEnvironment, ApplicationDbContext context)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "data.txt");
            _context = context;
        }

        public IActionResult SMS(string num = "")
        {
            ViewBag.ToNumber = GetFormattedNumber(num);
            ViewBag.FromNumber = GetTwilioNumber();
            return View();
        }

        private string GetFormattedNumber(string num)
        {
            num = (num ?? "").Trim();
            if (num.Length <= 10) return num;
            return num.Substring(num.Length - 10);
        }

        // Task 5: Chat view — kept for backward compatibility but SMS.cshtml is the main UI
        public IActionResult Chat(string num)
        {
            ViewBag.CustomerNumber = num;
            var convs = GetConversation(num);
            return View(convs ?? new List<SmsDetail>());
        }

        private string GetTwilioNumber()
        {
            return RetrieveData();
        }

        public string RetrieveData()
        {
            try
            {
                int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                return _context.User.Where(r => r.Id == uId).First().Phone;
            }
            catch (Exception)
            {
                return "Error";
            }
        }

        // Task 5: Fixed — queries SmsDetails for all messages with this customer phone number.
        // Looks at both fromNum and toNum so the full thread is captured regardless of direction.
        private List<SmsDetail> GetConversation(string customerNum)
        {
            if (string.IsNullOrWhiteSpace(customerNum)) return new List<SmsDetail>();

            // Normalise: ensure E.164 with +1 prefix for comparison
            string e164 = NormaliseToE164(customerNum);

            return _context.SmsDetails
                .Where(s => s.fromNum == e164 || s.toNum == e164)
                .OrderBy(s => s.createdOn)
                .ToList();
        }

        // Task 5: JSON endpoint consumed by SMS.cshtml via AJAX to load the conversation thread.
        // Returns messages plus the customer's ContactDetail name if available.
        [HttpGet]
        public async Task<IActionResult> GetConversationJson(string num)
        {
            if (string.IsNullOrWhiteSpace(num))
                return Json(new { success = false, messages = new List<object>(), customerName = "" });

            string e164 = NormaliseToE164(num);
            string twilioNum = GetTwilioNumber();
            string twilioE164 = NormaliseToE164(twilioNum ?? "");

            var messages = await _context.SmsDetails
                .Where(s => s.fromNum == e164 || s.toNum == e164)
                .OrderBy(s => s.createdOn)
                .ToListAsync();

            // Resolve customer name from ContactDetail
            var contact = await _context.ContactDetail
                .FirstOrDefaultAsync(c => c.PhoneNumber == e164);

            // Attach MMS link counts
            var msgIds = messages.Select(m => m.Id).ToList();
            var mmsCounts = await _context.MmsLinkss
                .Where(m => msgIds.Contains(m.SmsId))
                .GroupBy(m => m.SmsId)
                .Select(g => new { SmsId = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = messages.Select(m => new
            {
                id           = m.Id,
                body         = m.smsBody ?? "",
                direction    = m.smsDirection,          // "Incoming" | "Outgoing"
                date         = m.createdOn.ToString("MM-dd-yyyy"),
                time         = m.createdOn.ToString("hh:mm tt"),
                dateTimeFull = m.createdOn.ToString("MM-dd-yyyy hh:mm tt"),
                fromNum      = m.fromNum,
                toNum        = m.toNum,
                attachments  = mmsCounts.FirstOrDefault(x => x.SmsId == m.Id)?.Count ?? 0
            }).ToList();

            return Json(new
            {
                success      = true,
                messages     = result,
                customerName = contact?.Name ?? "",
                customerNum  = e164,
                twilioNum    = twilioE164
            });
        }

        // Normalise any phone format to E.164 (+1XXXXXXXXXX for US numbers)
        private static string NormaliseToE164(string num)
        {
            if (string.IsNullOrWhiteSpace(num)) return num ?? "";
            num = num.Trim();
            // Already E.164
            if (num.StartsWith("+")) return num;
            // 10-digit US number
            if (num.Length == 10) return "+1" + num;
            // 11-digit starting with 1
            if (num.Length == 11 && num.StartsWith("1")) return "+" + num;
            return num;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Conversation conversation)
        {
            try
            {
                conversation.fromNum = GetTwilioNumber();
                TempData["StMsg"] = "SMS Sent Successfully!";
                return RedirectToAction(nameof(Chat), new { num = conversation.toNum });
            }
            catch (Exception e)
            {
                TempData["ErMsg"] = "Error: " + e.ToString();
            }
            return View(conversation);
        }

        private List<string> StoreImageAndGetAddress(List<IFormFile> files)
        {
            var uploads = Path.Combine(_hostingEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var fileNames = new List<string>();
            foreach (var file in files)
            {
                if (file != null)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var fp = Path.Combine(uploads, fileName);
                    using (var fs = new FileStream(fp, FileMode.Create))
                        file.CopyTo(fs);
                    fileNames.Add(fileName);
                }
            }
            return fileNames;
        }

        [HttpPost]
        [RequestSizeLimit(9223372036854775807)]
        [RequestFormLimits(MultipartBodyLengthLimit = 9223372036854775807)]
        public async Task<IActionResult> SendSms(Conversation conversation, List<IFormFile> file)
        {
            try
            {
                conversation.toNum = "+1" + GetFormattedNumber(conversation.toNum);

                string accountSid = _configuration["AppSettings:AccountSid"];
                string authToken  = _configuration["AppSettings:AuthToken"];
                string WebAddress = _configuration["AppSettings:WebAddress"];

                List<string> mmsImg = null;
                if (file != null && file.Count > 0)
                    mmsImg = StoreImageAndGetAddress(file);

                conversation.fromNum = "+1" + GetFormattedNumber(GetTwilioNumber());
                TwilioClient.Init(accountSid, authToken);

                if (mmsImg != null)
                {
                    var mediaUrl  = mmsImg.Select(r => new Uri("https://" + WebAddress + "/uploads/" + r)).ToList();
                    var mediaName = mmsImg.ToList();

                    MessageResource.Create(
                        body:     conversation.smsBody,
                        from:     new Twilio.Types.PhoneNumber(conversation.fromNum),
                        to:       new Twilio.Types.PhoneNumber(conversation.toNum),
                        mediaUrl: mediaUrl);

                    var smsDetail = new SmsDetail
                    {
                        fromNum      = conversation.fromNum,
                        toNum        = conversation.toNum,
                        imgPath      = "",
                        smsDirection = "Outgoing",
                        smsBody      = conversation.smsBody,
                        createdOn    = DateTime.UtcNow
                    };
                    _context.Add(smsDetail);
                    _context.SaveChanges();

                    var imgUrlList = mediaUrl.Select((u, i) => new MmsLinks
                    {
                        SmsId    = smsDetail.Id,
                        FileName = mediaName[i],
                        Location = u.ToString()
                    }).ToList();

                    _context.AddRange(imgUrlList);
                    _context.SaveChanges();
                }
                else
                {
                    MessageResource.Create(
                        body: conversation.smsBody,
                        from: new Twilio.Types.PhoneNumber(conversation.fromNum),
                        to:   new Twilio.Types.PhoneNumber(conversation.toNum));

                    var smsDetail = new SmsDetail
                    {
                        fromNum      = conversation.fromNum,
                        toNum        = conversation.toNum,
                        imgPath      = "",
                        smsDirection = "Outgoing",
                        smsBody      = conversation.smsBody,
                        createdOn    = DateTime.UtcNow
                    };
                    _context.Add(smsDetail);
                    _context.SaveChanges();
                }

                TempData["StMsg"] = "SMS Sent Successfully!";
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }
            catch (Exception e)
            {
                TempData["ErMsg"] = "Error: " + e.ToString();
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }
        }
    }
}
