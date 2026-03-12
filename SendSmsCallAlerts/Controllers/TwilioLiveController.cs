using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;
using Twilio;
using SendSmsCallAlerts.Models;
using Newtonsoft.Json;
using NuGet.Protocol;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace SendSmsCallAlerts.Controllers
{
    public class TwilioLiveController : Twilio.AspNet.Core.TwilioController
    {
        private readonly IConfiguration _configuration;

        public TwilioLiveController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<AvailablePhoneNumber> LiveAvailablePhoneNumbers(int areaCode = 510)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

            TwilioClient.Init(accountSid, authToken);

            var local = LocalResource.Read(areaCode: areaCode, pathCountryCode: "US", limit: 20);


            if (local == null) return new List<AvailablePhoneNumber>();

            foreach (var record in local)
            {
                Console.WriteLine(record.FriendlyName);
            }

            string jsonResponse = local.ToJson();

            // Deserialize JSON response
            // AvailablePhoneNumbersResponse response = JsonConvert.DeserializeObject<AvailablePhoneNumbersResponse>(jsonResponse);
            List<AvailablePhoneNumber> phoneNumbers = JsonConvert.DeserializeObject<List<AvailablePhoneNumber>>(jsonResponse);


            return phoneNumbers;
        }


        public string PurchasePhoneNumber(string phoneNumber)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];

            TwilioClient.Init(accountSid, authToken);

            phoneNumber = phoneNumber.Trim();
            if(phoneNumber.Length == 10 ) phoneNumber = "+1" + phoneNumber;
            else if (phoneNumber.Length == 11) phoneNumber = "+" + phoneNumber;

            var incomingPhoneNumber = IncomingPhoneNumberResource.Create(
                smsUrl: new Uri("https://snowbro.azurewebsites.net/Incoming/Received"),
                smsMethod:Twilio.Http.HttpMethod.Post,
                phoneNumber: new Twilio.Types.PhoneNumber(phoneNumber)
            );

            //Console.WriteLine(incomingPhoneNumber.Sid);

            return phoneNumber + ":  The number was purchased, and configurations were done successfully!";
            //return phoneNumber + " Number purchased and configuraions are performed successfully!\n Number SID: " + incomingPhoneNumber.Sid;
        }

        public string SetIncSmsURL(string phoneSid, string url)
        {
            string accountSid = _configuration["AppSettings:AccountSid"];
            string authToken = _configuration["AppSettings:AuthToken"];
            url = _configuration["AppSettings:WebAddress"];

            TwilioClient.Init(accountSid, authToken);

            try
            {
                var incomingPhoneNumber = IncomingPhoneNumberResource.Update(
                    // voiceUrl: new Uri("https://www.your-new-voice-url.com/example"),
                    voiceUrl: new Uri("https://" + url + "/call/ReceiveCall"),
                    voiceMethod: Twilio.Http.HttpMethod.Post,
                    pathSid: phoneSid
                    );

                return "Incoming URL set successfully!";
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.ToString();
            }

            //Console.WriteLine(incomingPhoneNumber.Sid);


        }


        //public string SetIncomingURL(string phoneSid, string url)
        //{
        //    string accountSid = _configuration["AppSettings:AccountSid"];
        //    string authToken = _configuration["AppSettings:AuthToken"];

        //    TwilioClient.Init(accountSid, authToken);

        //    try
        //    {
        //        var incomingPhoneNumber = IncomingPhoneNumberResource.Update(
        //            // voiceUrl: new Uri("https://www.your-new-voice-url.com/example"),
        //            voiceUrl: new Uri(url + "/call/ReceiveCall"),
        //            voiceMethod: Twilio.Http.HttpMethod.Post,
        //            pathSid: phoneSid
        //            );

        //        return "Incoming URL set successfully!";
        //    }
        //    catch (Exception ex)
        //    {
        //        return "ERROR: " + ex.ToString();
        //    }

        //    //Console.WriteLine(incomingPhoneNumber.Sid);


        //}


    }
}
