using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NuGet.Protocol;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class TwilioController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly string filePath;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public TwilioController(IWebHostEnvironment hostingEnvironment, ApplicationDbContext context,
            IConfiguration configuration)
        {
            _hostingEnvironment = hostingEnvironment;
            filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "data.txt");
            _context = context;
            _configuration = configuration;
        }

        public IActionResult BuyNumber()
        {
            return View();
        }

        public IActionResult LiveAvailablePhoneNumbers(int areaCode)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

            TwilioClient.Init(accountSid, authToken);

            var local = LocalResource.Read(areaCode: areaCode, pathCountryCode: "US", limit: 20);


            if (local == null)
            {
                return Json(new
                {
                    aaData = new List<AvailablePhoneNumber>()
                });
            }

            foreach (var record in local)
            {
                Console.WriteLine(record.FriendlyName);
            }

            string jsonResponse = local.ToJson();

            // Deserialize JSON response
            // AvailablePhoneNumbersResponse response = JsonConvert.DeserializeObject<AvailablePhoneNumbersResponse>(jsonResponse);
            List<AvailablePhoneNumber> phoneNumbers = JsonConvert.DeserializeObject<List<AvailablePhoneNumber>>(jsonResponse);

            return Json(new
            {
                aaData = phoneNumbers
            });
            //return phoneNumbers;
        }

        public IActionResult PurchasePhoneNumber(string phoneNumber)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

            TwilioClient.Init(accountSid, authToken);

            phoneNumber = phoneNumber.Trim();
            if (phoneNumber.Length == 10) phoneNumber = "+1" + phoneNumber;
            else if (phoneNumber.Length == 11) phoneNumber = "+" + phoneNumber;

            var incomingPhoneNumber = IncomingPhoneNumberResource.Create(
                smsUrl: new Uri("https://snowbro.azurewebsites.net/Incoming/Received"),
                smsMethod: Twilio.Http.HttpMethod.Post,
                phoneNumber: new Twilio.Types.PhoneNumber(phoneNumber)
            );

            //Console.WriteLine(incomingPhoneNumber.Sid);

            TempData["StMsg"]= phoneNumber + ":  The number was purchased, and configurations were done successfully!";
            return RedirectToAction("BuyNumber");
            //return phoneNumber + " Number purchased and configuraions are performed successfully!\n Number SID: " + incomingPhoneNumber.Sid;
        }



        public IActionResult GetAvailableNumbers(List<AvailablePhoneNumber> phoneNumbers)
        {

            return View();
        }

        public IActionResult StoreData(string data)
        {
            try
            {
                if(data.Trim().Length == 10)
                {
                    data = "+1" + data;
                }
                else
                {
                    TempData["EMsg"] = "Please enter a 10 digit number";
                    return RedirectToAction("Index");
                }
                // Write data to the text file
                System.IO.File.WriteAllText(filePath, data);
                TempData["SMsg"] = "Number added successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["EMsg"] = $"Error storing data: {ex.Message}";
                return RedirectToAction("Index");
            }
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

        public async Task<ActionResult> UpdateData(string data)
        {
                int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                var usr = _context.User.Where(r => r.Id == uId).First();
                usr.Phone = "+1"+GetFormattedNumber(data);
                _context.Update(usr);
                await _context.SaveChangesAsync();
            TempData["SMsg"] = "Number updated successfully";
            return RedirectToAction("Index");

            //// Check if the file exists
            //if (System.IO.File.Exists(filePath))
            //{
            //    // Read existing data
            //    string existingData = System.IO.File.ReadAllText(filePath);

            //    // Update data
            //    existingData = data;

            //    // Write updated data back to the file
            //    System.IO.File.WriteAllText(filePath, existingData);

            //    TempData["SMsg"] = "Number updated successfully";
            //    return RedirectToAction("Index");
            //}
            //else
            //{
            //    TempData["EMsg"] = "File does not exist. No data to update.";
            //    return RedirectToAction("Index");
            //}

        }

        public IActionResult Index()
        {
            if(RetrieveData().Length == 12)
            {
                ViewBag.TwilioNumber = RetrieveData();
            }
            return View();
        }
    }
}
