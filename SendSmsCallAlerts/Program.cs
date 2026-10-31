using Hangfire;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Jobs;
using SendSmsCallAlerts.Services;

var configuredEnvironment = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build()["Environment"];

if (configuredEnvironment is not ("Development" or "Production"))
{
    throw new InvalidOperationException(
        "Set the Environment value in appsettings.json to either Development or Production.");
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
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
builder.Configuration.AddCommandLine(args);

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


builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(option =>
option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSession();
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

// NEW: password hashing
builder.Services.AddSingleton<IPasswordService, PasswordService>();

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

// NEW: one-time conversion of legacy plaintext passwords to hashes (idempotent, runs on every start).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var converted = LegacyPasswordMigration.HashPlaintextPasswords(db, passwords);
        if (converted > 0)
            app.Logger.LogInformation("Converted {Count} plaintext password(s) to hashes.", converted);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Legacy password conversion failed. Plaintext passwords are still accepted and upgraded at login.");
    }
}

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

app.UseSession(); // MOVED UP (was the last line): the Hangfire dashboard filter reads the session

app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminFilter() } // Admin only (was: local requests only)
});
app.UseHangfireServer();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=LogIn}/{id?}");
//pattern: "{controller=Sms}/{action=IncomingLogs}/{id?}");

app.Run();