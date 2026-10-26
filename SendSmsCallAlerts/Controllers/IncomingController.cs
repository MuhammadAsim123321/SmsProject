using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Identity.Client;
using Microsoft.Win32;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using Twilio.TwiML;
//using Twilio.TwiML.Voice;
using SendSmsCallAlerts.Services;

using Job = SendSmsCallAlerts.Models.Job;

namespace SendSmsCallAlerts.Controllers
{
    public class IncomingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        private readonly JobService _jobService;
        private readonly ISmsGateway _smsGateway;
        private readonly ILogger<IncomingController> _logger;
        private readonly IMediaUrlFactory _mediaUrlFactory;

        public IncomingController(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment,
     IConfiguration configuration, JobService jobService, ISmsGateway smsGateway, ILogger<IncomingController> logger,
     IMediaUrlFactory mediaUrlFactory)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _configuration = configuration;
            _jobService = jobService;
            _smsGateway = smsGateway;
            _logger = logger;
            _mediaUrlFactory = mediaUrlFactory;
        }
        private string ForwardingNumber => (_configuration["AppSettings:ForwardingNumber"] ?? "").Trim();

        [HttpPost]
        public async Task<IActionResult> Received(SmsRequest request, int numMedia)
        {
            List<MmsLinks> imgUrlList = new List<MmsLinks>();

            try
            {
                if (numMedia > 0)
                {
                    for (var i = 0; i < numMedia; i++)
                    {
                        MmsLinks mmsLnk = new MmsLinks();

                        var mediaUrl = Request.Form[$"MediaUrl{i}"];
                        var contentType = Request.Form[$"MediaContentType{i}"];

                        string flName = "downloads/" + Path.GetFileName(mediaUrl) + "_" + DateTime.Now.ToString("hh_mm_ss") + "_" + GetDefaultExtension(contentType);
                        string filePath = GetMediaFileName(flName);
                        await DownloadUrlToFileAsync(mediaUrl, filePath);

                        mmsLnk.FileName = flName;
                        mmsLnk.Location = _mediaUrlFactory.CreateUrl(flName).ToString();
                        imgUrlList.Add(mmsLnk);
                    }
                }
            }
            catch (Exception e) { }

            SmsDetail smsDetail = new SmsDetail();
            smsDetail.fromNum = Request.Form["From"];
            smsDetail.toNum = Request.Form["To"];
            smsDetail.imgPath = "";
            smsDetail.smsDirection = "Incoming";
            smsDetail.smsBody = Request.Form["Body"];
            smsDetail.createdOn = DateTime.UtcNow;
            smsDetail.IsReadYesOrNo = "No";

            // link the message to the customer right away when the number is already known
            var knownContactId = await _context.ContactDetail.AsNoTracking()
                .Where(c => c.PhoneNumber == smsDetail.fromNum)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
            if (knownContactId.HasValue) smsDetail.ContactDetailId = knownContactId;

            _context.Add(smsDetail);
            _context.SaveChanges();

            if (imgUrlList != null && imgUrlList.Count() > 0)
            {
                foreach (var rec in imgUrlList)
                    rec.SmsId = smsDetail.Id;

                _context.AddRange(imgUrlList);
                _context.SaveChanges();
            }

            await ProcessSmsBody(smsDetail);

            try
            {
                if (ForwardingNumber.Length > 0 && (smsDetail.fromNum == ForwardingNumber || smsDetail.toNum == ForwardingNumber))
                    await ForwardMessage(smsDetail, imgUrlList);
            }
            catch (Exception ex) { }

            try
            {
                if (smsDetail.fromNum == "+18557233022" || smsDetail.toNum == "+18557233022")
                    await ResendWithMask(smsDetail, imgUrlList);
            }
            catch (Exception ex) { }

            await IvrSms(smsDetail);

            return Ok();
        }


        private async Task ProcessSmsBody(SmsDetail smsDetail)
        {
            string body = smsDetail.smsBody?.Trim().ToLower() ?? "";

            if (body == "cancel")
            {
                await HandleCancel(smsDetail);
            }
            else if (body == "c")
            {
                await HandleConfirm(smsDetail);
            }
            else if (body == "stop")
            {
                await HandleStop(smsDetail);
            }
        }


        private async Task HandleCancel(SmsDetail smsDetail)
        {
            try
            {
                var contact = await _context.ContactDetail.FirstOrDefaultAsync(c => c.PhoneNumber == smsDetail.fromNum);
                if (contact == null) return;
                var job = await _jobService.CancelNextUpcomingAsync(smsDetail.fromNum);
                if (job != null) await SendNotification("cancel", contact, job);
            }
            catch (Exception ex) { }
        }

        private async Task HandleConfirm(SmsDetail smsDetail)
        {
            try
            {
                var contact = await _context.ContactDetail.FirstOrDefaultAsync(c => c.PhoneNumber == smsDetail.fromNum);
                if (contact == null) return;
                var job = await _jobService.ConfirmNextUpcomingAsync(smsDetail.fromNum);
                if (job != null) await SendNotification("confirm", contact, job);
            }
            catch (Exception ex) { }
        }

        private async Task HandleStop(SmsDetail smsDetail)
        {
            try
            {
                bool alreadyOptedOut = await _context.OptOuts.AnyAsync(o => o.PhoneNumber == smsDetail.fromNum);
                if (!alreadyOptedOut)
                {
                    _context.OptOuts.Add(new OptOut { PhoneNumber = smsDetail.fromNum, ReceivedAt = DateTime.UtcNow });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex) { }
        }

        // builds the staff message; still not delivered anywhere (same as before)
        private Task SendNotification(string type, ContactDetail contact, Job job)
        {
            string category = job.Category?.keyword ?? "service";
            string when = $"{job.StartAt:dd-MMM-yyyy} at {job.StartAt:hh:mm tt}";
            string message = type == "cancel"
                ? $"Customer {contact.Name} cancelled {category} on {when}, reschedule customer"
                : $"Customer {contact.Name} confirmed {category} on {when}";
            return Task.CompletedTask;
        }

        private async Task<bool> IvrSms(SmsDetail smsDetail)
        {
            //if (smsDetail.toNum == "+18557233022")
            if (IsThisIvrNum(smsDetail.toNum))
            {
                try
                {
                    int uId = GetIvrNumUserId(smsDetail.toNum);

                    int num = Convert.ToInt32(smsDetail.smsBody);
                    if (_context.IvrOption.Where(r => r.UserId == uId).Any(r => r.Num == num))
                    {
                        await SendOptSelectedSms(num, smsDetail.toNum, smsDetail.fromNum);
                        //await SendOptSelectedSms(num, "+18557233022", smsDetail.fromNum);
                    }
                    else
                    {
                        if (!_context.IntroSmsHistory.Any(r => r.FromNum == smsDetail.fromNum && r.CreatedOn > DateTime.UtcNow.AddDays(-30)))
                        {
                            // SaveIntroSmsHistory(smsDetail.fromNum, "+18557233022");
                            SaveIntroSmsHistory(smsDetail.fromNum, smsDetail.toNum);
                            //await SendWelcomeSms("+18557233022", smsDetail.fromNum);
                            await SendWelcomeSms(smsDetail.toNum, smsDetail.fromNum);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!_context.IntroSmsHistory.Any(r => r.FromNum == smsDetail.fromNum && r.CreatedOn > DateTime.UtcNow.AddDays(-30)))
                    {
                        //SaveIntroSmsHistory(smsDetail.fromNum, "+18557233022");
                        SaveIntroSmsHistory(smsDetail.fromNum, smsDetail.toNum);
                        // await SendWelcomeSms("+18557233022", smsDetail.fromNum);
                        await SendWelcomeSms(smsDetail.toNum, smsDetail.fromNum);

                    }
                }

            }

            return true;
        }

        private bool IsThisIvrNum(string twNum)
        {
            if (_context.IvrOption.Any(r => r.TwilioNum == twNum))
                return true;
            return false;
        }

        private int GetIvrNumUserId(string twNum)
        {
            if (_context.IvrOption.Any(r => r.TwilioNum == twNum))
            {
                return _context.IvrOption.First().UserId;
            }

            return 0;
        }

        private bool SaveIntroSmsHistory(string fromNum, string to)
        {
            try
            {
                IntroSmsHistory ish = new IntroSmsHistory();
                ish.FromNum = fromNum;
                ish.ToNum = to;
                ish.CreatedOn = DateTime.UtcNow;

                _context.IntroSmsHistory.Add(ish);
                _context.SaveChanges();

                return true;
            }
            catch (Exception e)
            {
                return false;
            }


        }

        private List<IvrOptions> GetIvrOptions(string twNo)
        {
            return _context.IvrOption.Where(r => r.TwilioNum == twNo).OrderBy(r => r.Num).ToList();
        }

        private IvrOptions GetSelectedIvrOption(int num, string twNum)
        {
            return _context.IvrOption.Where(r => r.Num == num && r.TwilioNum == twNum).First();
        }

        private List<OptionInstructions> GetOptionInstructions(int ivrId, string twNum)
        {
            return _context.OptionInstruction.Where(r => r.IvrId == ivrId && r.TwilioNum == twNum).OrderBy(o => o.InstructionOrder).ToList();
        }

        private async Task<bool> SendWelcomeSms(string from, string to)
        {
            try
            {
                // string intro = "Thanks for Contacting ReviveCarpetRepair.com, LET’S GET STARTED! For Pricing and availability on the CORRECT service you’re looking for, Please respond with the # of your interested service.\n";
                string intro = GetIntroSMS(from) + "\n";
                var opts = GetIvrOptions(from);
                foreach (var rec in opts)
                {
                    intro = intro + " " + rec.Num + "- " + rec.keyword + ";\n";
                }

                if (from != to)
                {
                    var result = await _smsGateway.SendAsync(to, intro, fallbackFrom: from);
                    if (!result.Success)
                        throw new InvalidOperationException(result.Error ?? "The welcome message could not be sent.");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Welcome SMS processing failed.");
                return false;
            }
        }


        public string GetIntroSMS(string twNum)
        {
            var rec = _context.IntroSms.Where(r => r.TwNum == twNum).FirstOrDefault();
            if (rec != null)
            {
                return rec.Message.ToString();
            }
            return "";
        }

        private async Task<bool> SendOptSelectedSms(int selectedNum, string from, string to)
        {
            try
            {
                var selectedOpt = GetSelectedIvrOption(selectedNum, from);

                string intro = "Ok, great! You’ve Pressed " + selectedNum + " for \"" + selectedOpt.keyword + "\".\n";
                intro = intro + "INSTRUCTIONS:\n";

                var opts = GetOptionInstructions(selectedNum, from);

                foreach (var rec in opts)
                {
                    intro = intro + " (" + rec.InstructionOrder + ") " + rec.Instruction + ";\n";
                }

                if (from != to)
                {
                    var result = await _smsGateway.SendAsync(to, intro, fallbackFrom: from);
                    if (!result.Success)
                        throw new InvalidOperationException(result.Error ?? "The selected-option message could not be sent.");
                }


                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Selected-option SMS processing failed.");
                return false;
            }
        }


        private async Task<bool> ResendWithMask(SmsDetail smsDetail, List<MmsLinks> tempImgPath)
        {
            string Number = "+1" + smsDetail.smsBody.Substring(0, 10);

            smsDetail.smsBody = smsDetail.smsBody.Substring(10);

            if (Number.Length == 12)
            {
                await SendSmsMms(smsDetail, Number, tempImgPath);

            }
            return true;
        }


        private async Task<bool> ForwardMessage(SmsDetail smsDetail, List<MmsLinks> tempImgPath)
        {
            var lst = _context.ForwardNum.ToList();
            if (lst != null && lst.Count() > 0)
            {
                foreach (var rec in lst)
                {
                    await SendSmsMms(smsDetail, rec.Number, tempImgPath);
                }
            }
            return true;
        }

        private async Task<bool> SendSmsMms(SmsDetail smsDetail, string num, List<MmsLinks> tempImgPath)
        {
            try
            {
                if (smsDetail.toNum == num)
                    return true;

                var mediaUrl = tempImgPath?.Select(image => new Uri(image.Location)).ToList();
                var fromDigits = new string((smsDetail.fromNum ?? "").Where(char.IsDigit).ToArray());
                var sourceNumber = fromDigits.Length > 10
                    ? fromDigits.Substring(fromDigits.Length - 10)
                    : fromDigits;
                var body = (sourceNumber.Length > 0 ? sourceNumber + " " : "") + (smsDetail.smsBody ?? "");
                var result = await _smsGateway.SendAsync(
                    num,
                    body,
                    mediaUrl,
                    senderOverride: ForwardingNumber);
                if (!result.Success)
                {
                    _logger.LogWarning("Forwarded SMS/MMS send failed: {Error}", result.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forwarded SMS/MMS processing failed.");
                return false;
            }
            return true;
        }

        private string GetMediaFileName(string flName)
        {


            //return "https://" + WebAddress + "/wwwroot/downloads/" + Path.GetFileName(mediaUrl)+"_"+DateTime.Now.ToString("hh_mm_ss")+"_"+ GetDefaultExtension(contentType);

            return Path.Combine(_hostingEnvironment.WebRootPath, flName);

            //return Server.MapPath(
            //    // e.g. ~/App_Data/MExxxx.jpg
            //    SavePath +
            //    Path.GetFileName(mediaUrl) +
            //    GetDefaultExtension(contentType)
            //);
        }

        private async Task DownloadUrlToFileAsync(string mediaUrl,
            string filePath)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

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
            // NOTE: This implementation is Windows specific (uses Registry)
            // Platform independent way might be to download a known list of
            // mime type mappings like: http://bit.ly/2gJYKO0
            var key = Registry.ClassesRoot.OpenSubKey(
                @"MIME\Database\Content Type\" + mimeType, false);
            var ext = key?.GetValue("Extension", null)?.ToString();
            return ext ?? "application/octet-stream";
        }


    }
}