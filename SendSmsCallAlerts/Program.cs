using Hangfire;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Jobs;
using SendSmsCallAlerts.Services;
using System.Text.Json;

var configuredEnvironment = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build()["Environment"];

if (configuredEnvironment is not ("Development" or "Production"))
{
    throw new InvalidOperationException(
        "Set the Environment value in appsettings.json to either Development or Production.");
}

var isDistanceCommand = args.Length > 0
    && string.Equals(args[0], "distance", StringComparison.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = isDistanceCommand ? Array.Empty<string>() : args,
    EnvironmentName = configuredEnvironment
});

var environmentSettingsPrefix = $"EnvironmentSettings:{configuredEnvironment}:";
var environmentSettings = builder.Configuration
    .GetSection($"EnvironmentSettings:{configuredEnvironment}")
    .AsEnumerable()
    .Where(setting => setting.Value is not null)
    .Select(setting => new KeyValuePair<string, string?>(
        setting.Key[environmentSettingsPrefix.Length..],
        setting.Value))
    .ToArray();

builder.Configuration.AddInMemoryCollection(environmentSettings);
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();
if (!isDistanceCommand)
    builder.Configuration.AddCommandLine(args);
// Optional, self-contained Google Distance Matrix utility. Run with:
// dotnet run --project SendSmsCallAlerts -- distance [fromLocation] [toLocation]
// This path exits before the web host starts and can be removed independently.
if (isDistanceCommand)
{
    var apiKey = builder.Configuration["GoogleMaps:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
        apiKey = builder.Configuration["ROUTE_MAPS_API_KEY"];
    apiKey ??= string.Empty;
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Write("Google Maps API key: ");
        apiKey = (Console.ReadLine() ?? string.Empty).Trim();
    }

    var fromLocation = args.Length > 1 ? args[1] : Ask("From address or ZIP code: ");
    var toLocation = args.Length > 2 ? args[2] : Ask("To address or ZIP code:   ");

    try
    {
        var result = await GetDistanceAsync(apiKey, fromLocation, toLocation);
        Console.WriteLine();
        Console.WriteLine($"From: {fromLocation}");
        Console.WriteLine($"To:   {toLocation}");
        Console.WriteLine($"Distance:             {result.Miles:F1} miles");
        Console.WriteLine($"Drive time (traffic): {result.MinutesWithTraffic:F0} min");
        Console.WriteLine($"Drive time (typical): {result.MinutesTypical:F0} min");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Error: " + ex.Message);
        Environment.ExitCode = 1;
    }

    return;
}

if (builder.Environment.IsProduction())
{
    var missingSettings = new List<string>();

    void RequireSetting(string key)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
            missingSettings.Add(key);
    }

    RequireSetting("ConnectionStrings:DefaultConnection");
    RequireSetting("AppSettings:AccountSid");
    RequireSetting("AppSettings:AuthToken");
    RequireSetting("AppSettings:TwilioNumber");
    RequireSetting("AppSettings:DefaultCountryCode");
    RequireSetting("AppSettings:TmZn");

    var mediaBaseUrl = builder.Configuration["AppSettings:WebAddress"];
    if (!Uri.TryCreate(mediaBaseUrl, UriKind.Absolute, out var mediaUri)
        || mediaUri.Scheme != Uri.UriSchemeHttps
        || mediaUri.IsLoopback)
        missingSettings.Add("AppSettings:WebAddress must be a public HTTPS base URL in Production");

    if (missingSettings.Count > 0)
        throw new InvalidOperationException(
            "Production configuration is incomplete or unsafe: " + string.Join(", ", missingSettings));
}

// Add services to the container.


// was: builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews(options => options.Filters.Add<SendSmsCallAlerts.Filters.StaffActivityFilter>());

builder.Services.AddDbContext<ApplicationDbContext>(option =>
option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// was: builder.Services.AddSession();   (this is where the 20-minute default lived; now one setting)
builder.Services.AddSession(options =>
    options.IdleTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue<int?>("StaffActivity:SessionTimeoutMinutes") ?? 20));

builder.Services.AddHttpClient();

// NEW: Register GoogleDistanceService
builder.Services.AddScoped<GoogleDistanceService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddScoped<TechnicianAvailabilityService>();

builder.Services.AddScoped<AutomatedSmsService>();
builder.Services.AddScoped<AutomatedSmsJob>();
builder.Services.AddScoped<SmsLogService>();
builder.Services.AddScoped<SmsDispatchService>();
builder.Services.AddScoped<SmsDispatchJob>();

builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddSingleton<ISmsGateway, SmsGateway>();
builder.Services.AddSingleton<IMediaUrlFactory, MediaUrlFactory>();
builder.Services.AddScoped<IUploadedMediaStore, UploadedMediaStore>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<TechnicianReplyService>();
builder.Services.AddScoped<RingCentralService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.AddSingleton<ActivityTracker>();
builder.Services.AddScoped<StaffActivityService>();
builder.Services.AddScoped<ActivityRetentionJob>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue; // Set the maximum form size to a large value.
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = long.MaxValue; // Set the maximum request size to a large value.
});

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"))); // Replace with your Hangfire connection string

builder.Services.AddHostedService<HangfireStartUp>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseHangfireDashboard();
app.UseHangfireServer();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=LogIn}/{id?}");
    //pattern: "{controller=Sms}/{action=IncomingLogs}/{id?}");

app.UseSession();
app.Run();

static string Ask(string prompt)
{
    Console.Write(prompt);
    return (Console.ReadLine() ?? string.Empty).Trim();
}

static async Task<DistanceResult> GetDistanceAsync(string apiKey, string fromLocation, string toLocation)
{
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("No API key provided.");
    if (string.IsNullOrWhiteSpace(fromLocation) || string.IsNullOrWhiteSpace(toLocation))
        throw new InvalidOperationException("Both locations are required.");

    var origin = NormalizeDistanceLocation(fromLocation);
    var destination = NormalizeDistanceLocation(toLocation);

    var url = "https://maps.googleapis.com/maps/api/distancematrix/json"
        + "?origins=" + Uri.EscapeDataString(origin)
        + "&destinations=" + Uri.EscapeDataString(destination)
        + "&departure_time=now"
        + "&traffic_model=best_guess"
        + "&units=imperial"
        + "&key=" + Uri.EscapeDataString(apiKey);

    using var http = new HttpClient();
    using var response = await http.GetAsync(url);
    response.EnsureSuccessStatusCode();
    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = doc.RootElement;

    var status = root.TryGetProperty("status", out var statusElement)
        ? statusElement.GetString() ?? string.Empty
        : string.Empty;
    if (!string.Equals(status, "OK", StringComparison.Ordinal))
    {
        var message = root.TryGetProperty("error_message", out var messageElement)
            ? messageElement.GetString() ?? string.Empty
            : string.Empty;
        throw new InvalidOperationException($"Distance Matrix error: {status} {message}".Trim());
    }

    var element = root.GetProperty("rows")[0].GetProperty("elements")[0];
    var elementStatus = element.GetProperty("status").GetString() ?? string.Empty;
    if (!string.Equals(elementStatus, "OK", StringComparison.Ordinal))
        throw new InvalidOperationException($"No route found ({elementStatus}). Check the ZIP codes.");

    var meters = element.GetProperty("distance").GetProperty("value").GetDouble();
    var typicalSeconds = element.GetProperty("duration").GetProperty("value").GetDouble();
    var trafficSeconds = element.TryGetProperty("duration_in_traffic", out var traffic)
        ? traffic.GetProperty("value").GetDouble()
        : typicalSeconds;

    return new DistanceResult(meters / 1609.344, trafficSeconds / 60.0, typicalSeconds / 60.0);
}

static string NormalizeDistanceLocation(string location)
{
    location = location.Trim();
    var isUsZip = location.Length == 5 && location.All(char.IsDigit)
        || location.Length == 10 && location[5] == '-'
            && location.Take(5).All(char.IsDigit)
            && location.Skip(6).All(char.IsDigit);
    return isUsZip ? location + ", USA" : location;
}

record DistanceResult(double Miles, double MinutesWithTraffic, double MinutesTypical);
