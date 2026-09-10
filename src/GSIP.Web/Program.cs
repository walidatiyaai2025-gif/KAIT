using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using GSIP.Application.Configuration;
using GSIP.Application.Setup;
using GSIP.Infrastructure;
using GSIP.Integrations;
using GSIP.Web;
using GSIP.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

var keyDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keyDirectory);

var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
    .SetApplicationName("GSIP");

// Explicit filesystem persistence requires explicit key-at-rest protection.
// GSIP targets Windows/IIS deployment, so bind persisted Data Protection keys
// to the deployment identity with DPAPI. Non-Windows CI/dev hosts keep the
// existing filesystem provider without attempting to invoke Windows-only APIs.
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<ShellText>();
builder.Services.AddScoped<IdentityText>();
builder.Services.AddScoped<MetadataText>();
builder.Services.AddScoped<ExecutionText>();
builder.Services.AddScoped<SecuritySensitiveAuditFilter>();
builder.Services
    .AddControllersWithViews(options => options.Filters.AddService<SecuritySensitiveAuditFilter>())
    .AddViewLocalization();
builder.Services.Configure<PortalShellOptions>(builder.Configuration.GetSection(PortalShellOptions.SectionName));
builder.Services.AddGsipInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddGsipIntegrations();
builder.Services.AddHealthChecks();

var loginPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("IdentitySecurity:LoginRateLimitPermitCount") ?? 10);
var loginWindowSeconds = Math.Clamp(builder.Configuration.GetValue<int?>("IdentitySecurity:LoginRateLimitWindowSeconds") ?? 60, 1, 3600);
var challengePermitLimit = Math.Clamp(builder.Configuration.GetValue<int?>("IdentitySecurity:ChallengeRateLimitPermitCount") ?? 6, 1, 30);
var challengeWindowSeconds = Math.Clamp(builder.Configuration.GetValue<int?>("IdentitySecurity:ChallengeRateLimitWindowSeconds") ?? 60, 1, 3600);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // MFA and forced-password challenges are authenticated restricted sessions.
    // Partition them by exact user identity after authentication, with IP as a
    // fail-closed fallback. Other routes are not globally throttled here.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var challengePost = HttpMethods.IsPost(context.Request.Method)
            && (string.Equals(path, "/mfa/enroll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/mfa/verify", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/password/change", StringComparison.OrdinalIgnoreCase));
        if (!challengePost)
        {
            return RateLimitPartition.GetNoLimiter("non-challenge");
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = challengePermitLimit,
                Window = TimeSpan.FromSeconds(challengeWindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromSeconds(loginWindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }));
});

var app = builder.Build();

var supportedCultures = new[]
{
    CultureInfo.GetCultureInfo("en"),
    CultureInfo.GetCultureInfo("ar-KW")
};

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures.Select(culture => culture.Name).ToArray())
    .AddSupportedUICultures(supportedCultures.Select(culture => culture.Name).ToArray());
localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());

app.UseRequestLocalization(localizationOptions);
app.UseGsipSecurityHeaders();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var bypassForClosedP01Regression = app.Environment.IsEnvironment("RegressionTesting")
        && app.Configuration.GetValue<bool>("Setup:BypassGateForRegression");
    var path = context.Request.Path;
    var exempt = path.StartsWithSegments("/setup", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    if (!bypassForClosedP01Regression && !exempt)
    {
        var setup = context.RequestServices.GetRequiredService<ISetupService>();
        var status = await setup.GetStatusAsync(context.RequestAborted);
        if (!status.IsCompleted)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            context.Response.Redirect($"/setup?culture={Uri.EscapeDataString(culture)}");
            return;
        }
    }

    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseRestrictedSessionBoundary();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    AllowCachingResponses = false
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Shell}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
}
