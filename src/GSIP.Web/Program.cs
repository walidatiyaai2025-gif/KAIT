using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    AllowCachingResponses = false
});

app.MapGet("/", () =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    return Results.Json(new
    {
        product = "GSIP",
        phase = "P00",
        status = "baseline-ready",
        version
    });
});

app.Run();

public partial class Program
{
}
