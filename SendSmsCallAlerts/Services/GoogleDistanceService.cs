using System.Text.Json;

namespace SendSmsCallAlerts.Services
{
    public class GoogleDistanceService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleDistanceService> _logger;

        public GoogleDistanceService(
            IConfiguration configuration, 
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleDistanceService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        /// <summary>
        /// Gets the distance in miles between two ZIP codes using Google Distance Matrix API
        /// </summary>
        /// <param name="originZip">Origin ZIP code</param>
        /// <param name="destinationZip">Destination ZIP code</param>
        /// <returns>Distance in miles, or 0 if unable to calculate</returns>
        public async Task<double> GetDistanceInMiles(string originZip, string destinationZip)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(originZip) || string.IsNullOrWhiteSpace(destinationZip))
                {
                    _logger.LogWarning("Invalid ZIP codes provided: Origin={Origin}, Destination={Destination}", 
                        originZip, destinationZip);
                    return 0;
                }

                // Get API key from configuration
                var apiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("Google Maps API key not configured in appsettings.json");
                    return 0;
                }

                // Build API URL
                var url = $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                         $"?origins={Uri.EscapeDataString(originZip)}" +
                         $"&destinations={Uri.EscapeDataString(destinationZip)}" +
                         $"&units=imperial" +
                         $"&key={apiKey}";

                // Make API request
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Google Distance Matrix API returned status code: {StatusCode}", 
                        response.StatusCode);
                    return 0;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<GoogleDistanceMatrixResponse>(jsonContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Parse response
                if (data?.Status == "OK" && 
                    data.Rows?.Count > 0 && 
                    data.Rows[0].Elements?.Count > 0)
                {
                    var element = data.Rows[0].Elements[0];
                    
                    if (element.Status == "OK" && element.Distance != null)
                    {
                        // Distance text is in format "X.X mi"
                        var distanceText = element.Distance.Text;
                        var distanceValue = element.Distance.Value; // meters
                        
                        // Convert meters to miles (1 mile = 1609.34 meters)
                        var miles = distanceValue / 1609.34;
                        
                        _logger.LogInformation("Distance calculated: {Origin} to {Destination} = {Miles:F2} miles", 
                            originZip, destinationZip, miles);
                        
                        return Math.Round(miles, 1);
                    }
                    else
                    {
                        _logger.LogWarning("Unable to calculate distance. Element status: {Status}", 
                            element.Status);
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid response from Google Distance Matrix API. Status: {Status}", 
                        data?.Status);
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating distance between {Origin} and {Destination}", 
                    originZip, destinationZip);
                return 0;
            }
        }

        // Response models for Google Distance Matrix API
        private class GoogleDistanceMatrixResponse
        {
            public string Status { get; set; }
            public List<Row> Rows { get; set; }
        }

        private class Row
        {
            public List<Element> Elements { get; set; }
        }

        private class Element
        {
            public string Status { get; set; }
            public DistanceInfo Distance { get; set; }
            public DurationInfo Duration { get; set; }
        }

        private class DistanceInfo
        {
            public string Text { get; set; }
            public int Value { get; set; } // in meters
        }

        private class DurationInfo
        {
            public string Text { get; set; }
            public int Value { get; set; } // in seconds
        }
    }
}
