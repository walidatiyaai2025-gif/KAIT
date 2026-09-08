using System.Globalization;
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

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
    .SetApplicationName("GSIP");
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<ShellText>();
builder.Services.AddScoped<IdentityText>();
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization();
builder.Services.Configure<PortalShellOptions>(builder.Configuration.GetSection(PortalShellOptions.SectionName));
builder.Services.AddGsipInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddGsipIntegrations();
builder.Services.AddHealthChecks();

var loginPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("IdentitySecurity:LoginRateLimitPermitCount") ?? 10);
var loginWindowSeconds = Math.Clamp(builder.Configuration.GetValue<int?>("IdentitySecurity:LoginRateLimitWindowSeconds") ?? 60, 1, 3600);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
app.UseRateLimiter();
app.UseAuthentication();
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
