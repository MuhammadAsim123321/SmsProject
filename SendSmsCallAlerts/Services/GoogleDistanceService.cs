using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Driving distance between two US ZIP codes using the Google Routes API (computeRouteMatrix).
    /// Same public methods as before, so controllers do not need to change.
    /// </summary>
    public class GoogleDistanceService
    {
        private const string Endpoint = "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix";

        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleDistanceService> _logger;
        private readonly IMemoryCache _cache;

        private static readonly Regex ZipRegex = new(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);

        public GoogleDistanceService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleDistanceService> logger,
            IMemoryCache cache)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
            _logger = logger;
            _cache = cache;
        }

        /// <summary>Backward compatible: returns 0 when distance is unknown.</summary>
        public async Task<double> GetDistanceInMiles(string originZip, string destinationZip)
            => await GetDistanceInMilesOrNull(originZip, destinationZip) ?? 0;

        /// <summary>
        /// Miles between two ZIP codes, or null if it cannot be calculated.
        /// Same ZIP returns 0 without calling Google. Results are cached.
        /// </summary>
        public async Task<double?> GetDistanceInMilesOrNull(string originZip, string destinationZip)
        {
            try
            {
                var origin = NormalizeZip(originZip);
                var dest = NormalizeZip(destinationZip);
                if (origin == null || dest == null)
                {
                    _logger.LogWarning("Invalid ZIP codes: Origin={Origin}, Destination={Destination}", originZip, destinationZip);
                    return null;
                }

                if (origin == dest) return 0;

                var cacheKey = $"routes:{origin}:{dest}";
                if (_cache.TryGetValue(cacheKey, out DistanceResult cached))
                    return cached.Miles;

                // Accepts GoogleMaps:ApiKey (user-secrets / appsettings) or ROUTE_MAPS_API_KEY (env var)
                var apiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                    apiKey = _configuration["ROUTE_MAPS_API_KEY"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("Google API key is not configured (GoogleMaps:ApiKey).");
                    return null;
                }

                var payload = new
                {
                    origins = new[] { new { waypoint = new { address = origin + ", USA" } } },
                    destinations = new[] { new { waypoint = new { address = dest + ", USA" } } },
                    travelMode = "DRIVE"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Goog-Api-Key", apiKey);
                request.Headers.Add("X-Goog-FieldMask", "originIndex,destinationIndex,distanceMeters,status,condition");

                using var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // e.g. 403 PERMISSION_DENIED (API not enabled / key restriction), 429 quota
                    _logger.LogError("Routes API HTTP {StatusCode}: {Body}", (int)response.StatusCode, Truncate(json, 500));
                    _cache.Set(cacheKey, new DistanceResult { Miles = null }, TimeSpan.FromMinutes(5));
                    return null;
                }

                double? miles = null;
                var elements = JsonSerializer.Deserialize<List<RouteElement>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var element = elements?.FirstOrDefault();
                if (element != null && element.DistanceMeters.HasValue
                    && string.Equals(element.Condition, "ROUTE_EXISTS", StringComparison.OrdinalIgnoreCase))
                {
                    miles = Math.Round(element.DistanceMeters.Value / 1609.344, 1);
                }
                else
                {
                    _logger.LogWarning("Routes API returned no route. Condition={Condition}", element?.Condition);
                }

                // success cached 24h, failure only 5 min
                _cache.Set(cacheKey, new DistanceResult { Miles = miles },
                    miles.HasValue ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(5));

                return miles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating distance between {Origin} and {Destination}", originZip, destinationZip);
                return null;
            }
        }

        private static string NormalizeZip(string zip)
        {
            if (string.IsNullOrWhiteSpace(zip)) return null;
            zip = zip.Trim();
            if (!ZipRegex.IsMatch(zip)) return null;
            return zip.Substring(0, 5);
        }

        private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        private sealed class DistanceResult { public double? Miles { get; set; } }

        private class RouteElement
        {
            public int? OriginIndex { get; set; }
            public int? DestinationIndex { get; set; }
            public int? DistanceMeters { get; set; }
            public string Condition { get; set; }
        }
    }
}