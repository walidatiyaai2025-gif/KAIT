using GSIP.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var checks = new List<(string Name, Action Body)>
{
    ("production https emits hardened headers", ProductionHttpsEmitsHardenedHeaders),
    ("production http omits hsts", ProductionHttpOmitsHsts),
    ("development https omits hsts", DevelopmentHttpsOmitsHsts),
    ("downstream observes headers before execution", DownstreamObservesHeaders),
    ("middleware replaces weaker preexisting policy", ReplacesWeakerPreexistingPolicy)
};

var failures = new List<string>();
foreach (var (name, body) in checks)
{
    try
    {
        body();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
        Console.Error.WriteLine($"FAIL: {name}: {exception.Message}");
    }
}

if (failures.Count != 0)
{
    Console.Error.WriteLine($"P03 security header checks failed: {failures.Count}");
    return 1;
}

Console.WriteLine($"P03 security header checks passed: {checks.Count}");
return 0;

static void ProductionHttpsEmitsHardenedHeaders()
{
    var context = Invoke("Production", isHttps: true);
    AssertHeader(context, "X-Content-Type-Options", "nosniff");
    AssertHeader(context, "X-Frame-Options", "DENY");
    AssertHeader(context, "Referrer-Policy", "no-referrer");
    AssertHeader(context, "Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
    AssertHeader(context, "Cross-Origin-Opener-Policy", "same-origin");
    AssertHeader(context, "Cross-Origin-Resource-Policy", "same-origin");
    AssertHeader(context, "Strict-Transport-Security", "max-age=31536000; includeSubDomains");

    AssertHardenedCsp(context.Response.Headers["Content-Security-Policy"].ToString());
}

static void ProductionHttpOmitsHsts()
{
    var context = Invoke("Production", isHttps: false);
    AssertAbsent(context, "Strict-Transport-Security");
    AssertHeader(context, "X-Content-Type-Options", "nosniff");
}

static void DevelopmentHttpsOmitsHsts()
{
    var context = Invoke(Environments.Development, isHttps: true);
    AssertAbsent(context, "Strict-Transport-Security");
    AssertHardenedCsp(context.Response.Headers["Content-Security-Policy"].ToString());
}

static void DownstreamObservesHeaders()
{
    var observed = false;
    var environment = new TestHostEnvironment { EnvironmentName = "Production" };
    var middleware = new SecurityHeadersMiddleware(
        context =>
        {
            observed = context.Response.Headers["X-Frame-Options"] == "DENY"
                && context.Response.Headers.ContainsKey("Content-Security-Policy");
            return Task.CompletedTask;
        },
        environment);
    var context = new DefaultHttpContext();
    context.Request.Scheme = Uri.UriSchemeHttps;
    middleware.InvokeAsync(context).GetAwaiter().GetResult();
    Assert(observed, "Downstream pipeline did not observe the security headers.");
}

static void ReplacesWeakerPreexistingPolicy()
{
    var environment = new TestHostEnvironment { EnvironmentName = "Production" };
    var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, environment);
    var context = new DefaultHttpContext();
    context.Request.Scheme = Uri.UriSchemeHttps;
    context.Response.Headers["Content-Security-Policy"] = "default-src *";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

    middleware.InvokeAsync(context).GetAwaiter().GetResult();

    var csp = context.Response.Headers["Content-Security-Policy"].ToString();
    Assert(!string.Equals(csp, "default-src *", StringComparison.Ordinal), "Weak CSP was not replaced.");
    AssertHardenedCsp(csp);
    AssertHeader(context, "X-Frame-Options", "DENY");
}

static DefaultHttpContext Invoke(string environmentName, bool isHttps)
{
    var environment = new TestHostEnvironment { EnvironmentName = environmentName };
    var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, environment);
    var context = new DefaultHttpContext();
    context.Request.Scheme = isHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
    middleware.InvokeAsync(context).GetAwaiter().GetResult();
    return context;
}

static void AssertHardenedCsp(string csp)
{
    AssertContains(csp, "default-src 'self'");
    AssertContains(csp, "object-src 'none'");
    AssertContains(csp, "frame-ancestors 'none'");
    AssertContains(csp, "form-action 'self'");
    AssertContains(csp, "script-src 'self'");
}

static void AssertHeader(HttpContext context, string name, string expected)
{
    var actual = context.Response.Headers[name].ToString();
    Assert(string.Equals(actual, expected, StringComparison.Ordinal), $"Header {name} expected '{expected}' but was '{actual}'.");
}

static void AssertAbsent(HttpContext context, string name)
    => Assert(!context.Response.Headers.ContainsKey(name), $"Header {name} must be absent.");

static void AssertContains(string actual, string expectedFragment)
    => Assert(actual.Contains(expectedFragment, StringComparison.Ordinal), $"Expected '{actual}' to contain '{expectedFragment}'.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "GSIP.P03SecurityChecks";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
