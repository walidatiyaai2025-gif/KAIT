using System.Globalization;
using GSIP.Application.Configuration;
using GSIP.Infrastructure;
using GSIP.Integrations;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization();
builder.Services.Configure<PortalShellOptions>(builder.Configuration.GetSection(PortalShellOptions.SectionName));
builder.Services.AddGsipInfrastructure();
builder.Services.AddGsipIntegrations();
builder.Services.AddHealthChecks();

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
app.UseStaticFiles();

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
