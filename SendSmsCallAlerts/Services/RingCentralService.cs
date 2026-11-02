using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Authenticates with RingCentral via the JWT grant and retrieves the company call log.
    /// </summary>
    public class RingCentralService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<RingCentralService> _logger;

        public RingCentralService(IConfiguration config, IHttpClientFactory httpFactory,
            ILogger<RingCentralService> logger)
        {
            _config = config;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches ALL outbound call records for the given date range by walking
        /// through every page RingCentral returns (250 records per request).
        /// </summary>
        /// <param name="dateFrom">Inclusive start (UTC).</param>
        /// <param name="dateTo">Exclusive end (UTC).</param>
        public async Task<List<RcCallRecord>> GetAllOutboundCallsAsync(
            DateTime dateFrom,
            DateTime dateTo)
        {
            var token     = await GetAccessTokenAsync();
            var serverUrl = _config["RingCentral:ServerUrl"]!.TrimEnd('/');
            var from      = dateFrom.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var to        = dateTo.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // 250 is the maximum perPage RingCentral allows, minimising round-trips.
            const int PerPage = 250;

            var allRecords = new List<RcCallRecord>();
            var client = _httpFactory.CreateClient("RingCentral");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            int currentPage = 1;
            int totalPages  = 1;   // updated after the first response

            do
            {
                var url = $"{serverUrl}/restapi/v1.0/account/~/call-log" +
                          $"?direction=Outbound&type=Voice&dateFrom={from}&dateTo={to}" +
                          $"&page={currentPage}&perPage={PerPage}&view=Detailed";

                var response = await client.GetAsync(url);
                var body     = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("RingCentral call-log request failed {Status}: {Body}",
                        response.StatusCode, body);
                    throw new InvalidOperationException(
                        $"RingCentral API error ({(int)response.StatusCode}): {body}");
                }

                var page = JsonConvert.DeserializeObject<RcCallLogPage>(body) ?? new RcCallLogPage();
                allRecords.AddRange(page.Records);

                // RingCentral returns totalPages in the paging object.
                if (page.Paging != null && page.Paging.TotalPages > 0)
                    totalPages = page.Paging.TotalPages;

                currentPage++;

            } while (currentPage <= totalPages);

            return allRecords;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private async Task<string> GetAccessTokenAsync()
        {
            var serverUrl    = _config["RingCentral:ServerUrl"]!.TrimEnd('/');
            var clientId     = _config["RingCentral:ClientId"]!;
            var clientSecret = _config["RingCentral:ClientSecret"]!;
            var jwt          = _config["RingCentral:Jwt"]!;

            // Basic auth header = Base64(clientId:clientSecret)
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var client = _httpFactory.CreateClient("RingCentral");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", jwt)
            });

            var tokenUrl = $"{serverUrl}/restapi/oauth/token";
            var response = await client.PostAsync(tokenUrl, body);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("RingCentral token request failed {Status}: {Body}",
                    response.StatusCode, responseBody);
                throw new InvalidOperationException(
                    $"RingCentral authentication failed ({(int)response.StatusCode}): {responseBody}");
            }

            var token = JsonConvert.DeserializeObject<RcTokenResponse>(responseBody);
            return token?.AccessToken
                   ?? throw new InvalidOperationException("RingCentral token response contained no access_token.");
        }
    }

    // ── RingCentral JSON models ─────────────────────────────────────────────────

    public class RcTokenResponse
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string? TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class RcCallLogPage
    {
        [JsonProperty("records")]
        public List<RcCallRecord> Records { get; set; } = new();

        [JsonProperty("navigation")]
        public RcNavigation? Navigation { get; set; }

        [JsonProperty("paging")]
        public RcPaging? Paging { get; set; }
    }

    public class RcNavigation
    {
        [JsonProperty("firstPage")]
        public RcPageRef? FirstPage { get; set; }

        [JsonProperty("nextPage")]
        public RcPageRef? NextPage { get; set; }

        [JsonProperty("previousPage")]
        public RcPageRef? PreviousPage { get; set; }

        [JsonProperty("lastPage")]
        public RcPageRef? LastPage { get; set; }
    }

    public class RcPageRef
    {
        [JsonProperty("uri")]
        public string? Uri { get; set; }
    }

    public class RcPaging
    {
        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("perPage")]
        public int PerPage { get; set; }

        [JsonProperty("pageStart")]
        public int PageStart { get; set; }

        [JsonProperty("pageEnd")]
        public int PageEnd { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }
    }

    public class RcCallRecord
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }

        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }   // seconds

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("direction")]
        public string? Direction { get; set; }

        [JsonProperty("result")]
        public string? Result { get; set; }

        [JsonProperty("from")]
        public RcParty? From { get; set; }

        [JsonProperty("to")]
        public RcParty? To { get; set; }

        [JsonProperty("legs")]
        public List<RcLeg>? Legs { get; set; }
    }

    public class RcParty
    {
        [JsonProperty("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonProperty("extensionNumber")]
        public string? ExtensionNumber { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }
    }

    public class RcLeg
    {
        [JsonProperty("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("result")]
        public string? Result { get; set; }

        [JsonProperty("from")]
        public RcParty? From { get; set; }

        [JsonProperty("to")]
        public RcParty? To { get; set; }
    }
}
