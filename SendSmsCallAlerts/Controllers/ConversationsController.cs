using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Migrations;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using System.Xml.Linq;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class ConversationsController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        private readonly string filePath;
        private readonly ApplicationDbContext _context;
        private readonly ISmsGateway _smsGateway;
        private readonly IUploadedMediaStore _uploadedMediaStore;

        public ConversationsController(
            IConfiguration configuration,
            IWebHostEnvironment hostingEnvironment,
            ApplicationDbContext context,
            ISmsGateway smsGateway,
            IUploadedMediaStore uploadedMediaStore)
        {
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "data.txt");
            _context = context;
            _smsGateway = smsGateway;
            _uploadedMediaStore = uploadedMediaStore;
        }

        public IActionResult SMS(string num = "")
        {
            ViewBag.ToNumber = GetFormattedNumber(num);
            ViewBag.FromNumber = _smsGateway.FromNumber.Length > 0
                ? _smsGateway.FromNumber
                : GetTwilioNumber();
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

        [HttpPost]
        [RequestSizeLimit(52 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52 * 1024 * 1024)]
        public async Task<IActionResult> SendSms(Conversation conversation, List<IFormFile> file)
        {
            try
            {
                conversation.toNum = _smsGateway.NormalizePhone(conversation.toNum);
                if (conversation.toNum.Length == 0)
                    throw new InvalidDataException("Enter a valid recipient phone number.");

                var media = await _uploadedMediaStore.SaveAsync(file);
                var fallbackFrom = _smsGateway.NormalizePhone(GetTwilioNumber());
                var send = await _smsGateway.SendAsync(
                    conversation.toNum,
                    conversation.smsBody ?? "",
                    media.Select(item => item.Location),
                    fallbackFrom);

                if (!send.Success)
                    throw new InvalidOperationException(send.Error ?? "The message could not be sent.");

                conversation.fromNum = send.From;
                var smsDetail = new SmsDetail
                {
                    fromNum = send.From,
                    toNum = conversation.toNum,
                    imgPath = "",
                    smsDirection = send.Simulated ? "OutgoingTest" : "Outgoing",
                    smsBody = conversation.smsBody ?? "",
                    createdOn = DateTime.UtcNow
                };
                _context.SmsDetails.Add(smsDetail);
                await _context.SaveChangesAsync();

                if (media.Count > 0)
                {
                    _context.MmsLinkss.AddRange(media.Select(item => new MmsLinks
                    {
                        SmsId = smsDetail.Id,
                        FileName = item.FileName,
                        Location = item.Location.ToString()
                    }));
                    await _context.SaveChangesAsync();
                }

                TempData["StMsg"] = send.Simulated
                    ? $"Development simulation completed; no message was sent. Images: {media.Count}."
                    : "SMS Sent Successfully!";
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }
            catch (Exception e)
            {
                TempData["ErMsg"] = "Error: " + e.Message;
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }
        }
    }
}
