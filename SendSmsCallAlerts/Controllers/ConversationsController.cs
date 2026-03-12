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
        // private const MaxFileSize = 10L * 1024L * 1024L * 1024L; // 10GB, adjust to your need

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
            ViewBag.FromNumber= GetTwilioNumber();
            return View();
        }

        private string GetFormattedNumber(string num)
        {
            num = num.Trim();
            if (num.Length < 10)
            {
                return num;
            }
            if (num.Length == 10)
            {
                return num;
            }

            return num.Substring(num.Length - 10);

        }

        public IActionResult Chat(string num)
        {
            
            ViewBag.CustomerNumber = num;
            
            List<Conversation> convs = GetConversation(num);

            return convs != null ?
                        View(convs) :
                        Problem("Entity set 'ApplicationDbContext.Conversations'  is null.");
        }

        private string GetTwilioNumber()
        {
            return RetrieveData();
           // return _configuration["AppSettings:TwilioNumber"];   
        }

        public string RetrieveData()
        {
            try
            {
                int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                return _context.User.Where(r => r.Id == uId).First().Phone;

                //// Read data from the text file
                //if (System.IO.File.Exists(filePath))
                //{
                //    string data = System.IO.File.ReadAllText(filePath);
                //    return data;
                //}
                //else
                //{
                //    return "File does not exist. No data to retrieve.";
                //}
            }
            catch (Exception ex)
            {
                return "Error";
            }
        }


        private List<Conversation> GetConversation(string num)
        {
            
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Conversation conversation)
        {
            try
            {
                string accountSid = _configuration["AppSettings:AccountSid"];
                string authToken = _configuration["AppSettings:AuthToken"];
                conversation.fromNum = GetTwilioNumber();
                //TwilioClient.Init(accountSid, authToken);

                //var message = MessageResource.Create(
                //    body: conversation.smsBody,
                //    from: new Twilio.Types.PhoneNumber(conversation.fromNum),
                //    to: new Twilio.Types.PhoneNumber(conversation.toNum)
                //);

                TempData["StMsg"] = "SMS Sent Successfully!";
                return RedirectToAction(nameof(Chat), new { num = conversation.toNum });
            }
            catch (Exception e)
            {
                TempData["ErMsg"] = "Error: "+e.ToString();
            }



            return View(conversation);
        }

        private List<string> StoreImageAndGetAddress(List<IFormFile> files)
        {
            var uploads = Path.Combine(_hostingEnvironment.WebRootPath, "uploads");

            // Ensure the directory exists
            if (!Directory.Exists(uploads))
            {
                Directory.CreateDirectory(uploads);
            }

            List<string> fileNames = new List<string>();
            foreach (var file in files)
            {
                // Check for null (no file selected for this iteration)
                if (file != null)
                {
                    // Create a unique file name
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploads, fileName);

                    // Save the file to the server
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    fileNames.Add(fileName);
                    //var relativePath = Path.GetRelativePath(_hostingEnvironment.ContentRootPath, filePath);

                }
            }


            return fileNames;
        }

       

        [HttpPost]

        //[DisableFormValueModelBinding]
        [RequestSizeLimit(9223372036854775807)]
        [RequestFormLimits(MultipartBodyLengthLimit = 9223372036854775807)]
        public async Task<IActionResult> SendSms(Conversation conversation, List<IFormFile> file)
        {
            try
            {
                conversation.toNum = "+1"+GetFormattedNumber(conversation.toNum);
                

                string accountSid = _configuration["AppSettings:AccountSid"];
                string authToken = _configuration["AppSettings:AuthToken"];
                string WebAddress = _configuration["AppSettings:WebAddress"];
                List<string> mmsImg = null;

                if (file != null && file.Count() > 0)
                {
                    mmsImg = StoreImageAndGetAddress(file);
                }


                    conversation.fromNum = "+1" + GetFormattedNumber(GetTwilioNumber());
                TwilioClient.Init(accountSid, authToken);

                if (mmsImg != null)
                {
                    var mediaUrl = new List<Uri>();
                    var mediaName = new List<string>();

                    foreach (var rec in mmsImg)
                    {
                        mediaUrl.Add(new Uri("https://" + WebAddress + "/uploads/" + rec));
                        mediaName.Add(rec);
                    }
                    //var mediaUrl = new[] {
                    //            new Uri("https://"+WebAddress+"/uploads/" + mmsImg)
                    //        }.ToList();

                    var message = MessageResource.Create(
                                  body: conversation.smsBody,
                                  from: new Twilio.Types.PhoneNumber(conversation.fromNum),
                                  to: new Twilio.Types.PhoneNumber(conversation.toNum),
                                  mediaUrl: mediaUrl
                                  );

                    List<MmsLinks> imgUrlList = new List<MmsLinks>();
                    for(int i=0 ; i<mediaUrl.Count() ; i++)
                    {
                        MmsLinks mmsLnk = new MmsLinks();
                        mmsLnk.FileName = mediaName[i];
                        mmsLnk.Location = mediaUrl[i].ToString();
                        imgUrlList.Add(mmsLnk);
                    }

                    SmsDetail smsDetail = new SmsDetail();
                    smsDetail.fromNum = conversation.fromNum;
                    smsDetail.toNum = conversation.toNum;
                    smsDetail.imgPath = "";
                    smsDetail.smsDirection = "Outgoing";
                    smsDetail.smsBody = conversation.smsBody;
                    smsDetail.createdOn = DateTime.UtcNow;

                    _context.Add(smsDetail);
                    _context.SaveChanges();

                    if (imgUrlList != null && imgUrlList.Count() > 0)
                    {
                        // List<MmsLinks> mmsLinks = new List<MmsLinks>();

                        foreach (var rec in imgUrlList)
                        {
                            rec.SmsId = smsDetail.Id;
                            // MmsLinks mmsLinks1 = new MmsLinks();
                            //mmsLinks1.SmsId = smsDetail.Id;
                            //mmsLinks1.Location = rec;

                            //mmsLinks.Add(mmsLinks1);
                        }

                        _context.AddRange(imgUrlList);
                        _context.SaveChanges();

                    }

                }
                else
                {
                    var message = MessageResource.Create(
                                   body: conversation.smsBody,
                                   from: new Twilio.Types.PhoneNumber(conversation.fromNum),
                                   to: new Twilio.Types.PhoneNumber(conversation.toNum)
                                    );

                    SmsDetail smsDetail = new SmsDetail();
                    smsDetail.fromNum = conversation.fromNum;
                    smsDetail.toNum = conversation.toNum;
                    smsDetail.imgPath = "";
                    smsDetail.smsDirection = "Outgoing";
                    smsDetail.smsBody = conversation.smsBody;
                    smsDetail.createdOn = DateTime.UtcNow;

                    _context.Add(smsDetail);
                    _context.SaveChanges();

                }

                TempData["StMsg"] = "SMS Sent Successfully!";
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }
            catch (Exception e)
            {
                TempData["ErMsg"] = "Error: "+e.ToString();
                return RedirectToAction(nameof(SMS), new { num = conversation.toNum });
            }



            
        }


    }
}
