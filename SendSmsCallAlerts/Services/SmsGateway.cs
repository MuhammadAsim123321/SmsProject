using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SendSmsCallAlerts.Services
{
    public record SmsSendResult(bool Success, bool Simulated, string From, string? Sid, string? Error);

    /// <summary>
    /// Every outgoing SMS from background jobs goes through here.
    ///
    /// The selected host environment determines whether messages are simulated or sent through Twilio.
    ///   AccountSid / AuthToken / TwilioNumber   real Twilio values (existing keys)
    ///   DefaultCountryCode is required and used when a number has no country code.
    /// </summary>
    public interface ISmsGateway
    {
        bool IsLive { get; }
        string Mode { get; }
        string FromNumber { get; }
        string NormalizePhone(string? raw);
        Task<SmsSendResult> SendAsync(
            string to,
            string body,
            IEnumerable<Uri>? mediaUrls = null,
            string? fallbackFrom = null,
            string? senderOverride = null);
    }

    public class SmsGateway : ISmsGateway
    {
        private readonly ILogger<SmsGateway> _logger;
        private readonly bool _hasCredentials;
        private readonly string _countryCode;

        public bool IsLive { get; }
        public string Mode { get; }
        public string FromNumber { get; }

        public SmsGateway(IConfiguration config, IHostEnvironment environment, ILogger<SmsGateway> logger)
        {
            _logger = logger;

            IsLive = environment.IsProduction();
            Mode = IsLive ? "Production" : "Development";

            var accountSid = config["AppSettings:AccountSid"] ?? "";
            var authToken = config["AppSettings:AuthToken"] ?? "";
            _hasCredentials = accountSid.Length > 0 && authToken.Length > 0;

            var cc = Digits(config["AppSettings:DefaultCountryCode"]);
            if (cc.Length == 0)
                throw new InvalidOperationException("AppSettings:DefaultCountryCode must be configured.");
            _countryCode = "+" + cc;

            FromNumber = NormalizePhone(config["AppSettings:TwilioNumber"]);

            if (IsLive && _hasCredentials) TwilioClient.Init(accountSid, authToken);

            _logger.LogInformation("SMS gateway started in {Mode} mode.", Mode);
        }

        public async Task<SmsSendResult> SendAsync(
            string to,
            string body,
            IEnumerable<Uri>? mediaUrls = null,
            string? fallbackFrom = null,
            string? senderOverride = null)
        {
            var toNumber = NormalizePhone(to);
            if (toNumber.Length == 0)
                return new SmsSendResult(false, false, "", null, "Invalid recipient phone number.");

            var from = !string.IsNullOrWhiteSpace(senderOverride)
                ? NormalizePhone(senderOverride)
                : FromNumber.Length > 0 ? FromNumber : NormalizePhone(fallbackFrom);

            if (!IsLive)
            {
                var mediaCount = mediaUrls?.Count() ?? 0;
                _logger.LogInformation("[DEVELOPMENT SIMULATION] Simulated a message with {MediaCount} media attachment(s).", mediaCount);
                return new SmsSendResult(true, true, from.Length > 0 ? from : "TEST", null, null);
            }

            if (!_hasCredentials)
                return new SmsSendResult(false, false, from, null, "Production SMS sending requires AppSettings:AccountSid and AppSettings:AuthToken.");
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