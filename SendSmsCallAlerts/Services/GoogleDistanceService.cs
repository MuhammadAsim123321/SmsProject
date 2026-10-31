using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Driving distance using the Google Distance Matrix API. Full addresses may include
    /// a country; ZIP-only values retain the application's US default.
    /// </summary>
    public class GoogleDistanceService
    {
        private const string Endpoint = "https://maps.googleapis.com/maps/api/distancematrix/json";

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
        public async Task<double> GetDistanceInMiles(string originLocation, string destinationLocation)
            => await GetDistanceInMilesOrNull(originLocation, destinationLocation) ?? 0;

        /// <summary>
        /// Miles between two ZIP codes, or null if it cannot be calculated.
        /// Same ZIP returns 0 without calling Google. Results are cached.
        /// </summary>
        public async Task<double?> GetDistanceInMilesOrNull(string originLocation, string destinationLocation)
        {
            try
            {
                var origin = NormalizeLocation(originLocation);
                var dest = NormalizeLocation(destinationLocation);
                if (origin == null || dest == null)
                {
                    _logger.LogWarning("Invalid locations: Origin={Origin}, Destination={Destination}", originLocation, destinationLocation);
                    return null;
                }

                if (string.Equals(origin, dest, StringComparison.OrdinalIgnoreCase)) return 0;

                var cacheKey = $"distance-matrix:{origin.ToLowerInvariant()}:{dest.ToLowerInvariant()}";
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

                var url = Endpoint
                    + "?origins=" + Uri.EscapeDataString(origin)
                    + "&destinations=" + Uri.EscapeDataString(dest)
                    + "&departure_time=now"
                    + "&traffic_model=best_guess"
                    + "&units=imperial"
                    + "&key=" + Uri.EscapeDataString(apiKey);

                using var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Distance Matrix API HTTP {StatusCode}: {Body}", (int)response.StatusCode, Truncate(json, 500));
                    _cache.Set(cacheKey, new DistanceResult { Miles = null }, TimeSpan.FromMinutes(5));
                    return null;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var apiStatus = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString()
                    : null;
                if (!string.Equals(apiStatus, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    var message = root.TryGetProperty("error_message", out var messageElement)
                        ? messageElement.GetString()
                        : null;
                    _logger.LogError("Distance Matrix API returned {Status}: {Message}", apiStatus, message);
                    _cache.Set(cacheKey, new DistanceResult { Miles = null }, TimeSpan.FromMinutes(5));
                    return null;
                }

                var element = root.GetProperty("rows")[0].GetProperty("elements")[0];
                var elementStatus = element.GetProperty("status").GetString();
                if (!string.Equals(elementStatus, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Distance Matrix API returned no route. Status={Status}", elementStatus);
                    _cache.Set(cacheKey, new DistanceResult { Miles = null }, TimeSpan.FromMinutes(5));
                    return null;
                }

                var meters = element.GetProperty("distance").GetProperty("value").GetDouble();
                var miles = Math.Round(meters / 1609.344, 1);

                // success cached 24h, failure only 5 min
                _cache.Set(cacheKey, new DistanceResult { Miles = miles },
                    TimeSpan.FromHours(24));

                return miles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating distance between {Origin} and {Destination}", originLocation, destinationLocation);
                return null;
            }
        }

        /// <summary>
        /// Joins a record's address fields into one Google Maps location. A ZIP-only value
        /// stays supported for existing US records; include country in CityState for other countries.
        /// </summary>
        public static string BuildLocation(string? zip, string? cityState, string? streetAddress)
        {
            return string.Join(", ", new[] { streetAddress, cityState, zip }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string NormalizeLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            location = string.Join(" ", location.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (location.Length > 250) return null;
            return ZipRegex.IsMatch(location) ? location + ", USA" : location;
        }

        private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        private sealed class DistanceResult { public double? Miles { get; set; } }

    }
}
