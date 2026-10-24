using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SendSmsCallAlerts.Services
{
    public record SmsSendResult(bool Success, bool Simulated, string From, string? Sid, string? Error);

    /// <summary>
    /// Every outgoing SMS from background jobs goes through here.
    ///
    /// appsettings.json (AppSettings section):
    ///   SmsMode            "Test" (default) = nothing is sent to Twilio, the send is simulated and logged.
    ///                      "Live"           = real Twilio send. Needs AccountSid, AuthToken, TwilioNumber.
    ///   AccountSid / AuthToken / TwilioNumber   real Twilio values (existing keys)
    ///   DefaultCountryCode "+1" (default) used when a number has no country code.
    ///
    /// Going live = fill the real values and set SmsMode to "Live". No code change.
    /// </summary>
    public interface ISmsGateway
    {
        bool IsLive { get; }
        string Mode { get; }
        string FromNumber { get; }
        string NormalizePhone(string? raw);
        Task<SmsSendResult> SendAsync(string to, string body, IEnumerable<Uri>? mediaUrls = null, string? fallbackFrom = null);
    }

    public class SmsGateway : ISmsGateway
    {
        private readonly ILogger<SmsGateway> _logger;
        private readonly bool _hasCredentials;
        private readonly string _countryCode;

        public bool IsLive { get; }
        public string Mode { get; }
        public string FromNumber { get; }

        public SmsGateway(IConfiguration config, ILogger<SmsGateway> logger)
        {
            _logger = logger;

            Mode = (config["AppSettings:SmsMode"] ?? "Test").Trim();
            IsLive = string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase);

            var accountSid = config["AppSettings:AccountSid"] ?? "";
            var authToken = config["AppSettings:AuthToken"] ?? "";
            _hasCredentials = accountSid.Length > 0 && authToken.Length > 0;

            var cc = Digits(config["AppSettings:DefaultCountryCode"] ?? "+1");
            _countryCode = "+" + (cc.Length > 0 ? cc : "1");

            FromNumber = NormalizePhone(config["AppSettings:TwilioNumber"]);

            if (IsLive && _hasCredentials) TwilioClient.Init(accountSid, authToken);

            _logger.LogInformation("SMS gateway started in {Mode} mode (credentials present: {HasCreds}).", Mode, _hasCredentials);
        }

        public async Task<SmsSendResult> SendAsync(string to, string body, IEnumerable<Uri>? mediaUrls = null, string? fallbackFrom = null)
        {
            var toNumber = NormalizePhone(to);
            if (toNumber.Length == 0)
                return new SmsSendResult(false, false, "", null, "Invalid recipient phone number.");

            // Sender: configured Twilio number first, otherwise the number the customer texted.
            var from = FromNumber.Length > 0 ? FromNumber : NormalizePhone(fallbackFrom);

            if (!IsLive)
            {
                _logger.LogInformation("[SMS TEST MODE] to {To}: {Body}", toNumber, body);
                return new SmsSendResult(true, true, from.Length > 0 ? from : "TEST", null, null);
            }

            if (!_hasCredentials)
                return new SmsSendResult(false, false, from, null, "SmsMode is Live but AppSettings:AccountSid / AuthToken are missing.");
            if (from.Length == 0)
                return new SmsSendResult(false, false, "", null, "No sender number. Set AppSettings:TwilioNumber.");

            try
            {
                var media = mediaUrls?.ToList();
                var message = media != null && media.Count > 0
                    ? await MessageResource.CreateAsync(
                        body: body,
                        from: new PhoneNumber(from),
                        to: new PhoneNumber(toNumber),
                        mediaUrl: media)
                    : await MessageResource.CreateAsync(
                        body: body,
                        from: new PhoneNumber(from),
                        to: new PhoneNumber(toNumber));

                return new SmsSendResult(true, false, from, message.Sid, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Twilio send to {To} failed", toNumber);
                return new SmsSendResult(false, false, from, null, ex.Message);
            }
        }

        public string NormalizePhone(string? raw)
        {
            raw = (raw ?? "").Trim();
            var d = Digits(raw);
            if (d.Length == 0) return "";
            if (raw.StartsWith("+")) return "+" + d;
            if (d.Length >= 10) return _countryCode + d.Substring(d.Length - 10);
            return "";
        }

        private static string Digits(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());
    }
}