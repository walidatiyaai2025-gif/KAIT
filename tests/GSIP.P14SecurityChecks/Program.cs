using System.Reflection;
using System.Security.Claims;
using GSIP.Application.Identity;
using GSIP.Web.Controllers;
using GSIP.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

var checks = new List<(string Name, Func<Task> Body)>
{
    ("known restricted-session claims are explicit", KnownRestrictionClaimsAreExplicit),
    ("restricted GET cannot reach normal application route", RestrictedGetCannotReachApplication),
    ("restricted POST fails closed", RestrictedPostFailsClosed),
    ("exact restricted challenge remains reachable", ExactChallengeRemainsReachable),
    ("cross-challenge access is rejected", CrossChallengeAccessIsRejected),
    ("unknown restriction fails closed", UnknownRestrictionFailsClosed),
    ("pipeline enforces auth then limiter then restriction then authorization", PipelineOrderIsFailClosed),
    ("RBAC independently rejects restricted principals", RbacRejectsRestrictedPrincipals),
    ("all mutating MVC actions enforce antiforgery", MutatingMvcActionsRequireAntiforgery),
    ("views do not bypass Razor output encoding", ViewsDoNotUseHtmlRaw),
    ("execution retries timeout and response reads remain bounded", ExecutionResilienceIsBounded),
    ("token refresh remains single flight and caller cancellation isolated", TokenRefreshConcurrencyIsBounded),
    ("security headers preserve script and framing hardening", SecurityHeadersRemainHardened)
};

var failures = new List<string>();
foreach (var (name, body) in checks)
{
    try
    {
        await body();
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
    Console.Error.WriteLine($"P14 security acceptance failed: {failures.Count} checks failed.");
    return 1;
}

Console.WriteLine($"P14_SECURITY_ACCEPTANCE=PASS checks={checks.Count}");
return 0;

static Task KnownRestrictionClaimsAreExplicit()
{
    Assert(GsipSessionRestrictions.IsKnown(GsipSessionRestrictions.MfaEnrollment), "MFA enrollment restriction is not canonical.");
    Assert(GsipSessionRestrictions.IsKnown(GsipSessionRestrictions.MfaVerification), "MFA verification restriction is not canonical.");
    Assert(GsipSessionRestrictions.IsKnown(GsipSessionRestrictions.PasswordChange), "Password-change restriction is not canonical.");
    Assert(!GsipSessionRestrictions.IsKnown("forged-restriction"), "Unknown restriction was accepted as canonical.");
    return Task.CompletedTask;
}

static async Task RestrictedGetCannotReachApplication()
{
    var (context, downstream) = await InvokeBoundaryAsync(GsipSessionRestrictions.MfaVerification, HttpMethods.Get, "/permissions");
    Assert(!downstream(), "Restricted MFA principal reached a normal application route.");
    Assert(context.Response.StatusCode == StatusCodes.Status302Found, "Restricted GET was not redirected to its exact challenge.");
    Assert(context.Response.Headers.Location == "/mfa/verify", "Restricted GET was redirected to the wrong challenge.");
    Assert(context.Response.Headers.CacheControl == "no-store", "Restricted redirect is cacheable.");
}

static async Task RestrictedPostFailsClosed()
{
    var (context, downstream) = await InvokeBoundaryAsync(GsipSessionRestrictions.MfaVerification, HttpMethods.Post, "/permissions/save");
    Assert(!downstream(), "Restricted MFA principal reached a state-changing route.");
    Assert(context.Response.StatusCode == StatusCodes.Status403Forbidden, "Restricted cross-boundary POST did not fail with 403.");
}

static async Task ExactChallengeRemainsReachable()
{
    var (context, downstream) = await InvokeBoundaryAsync(GsipSessionRestrictions.MfaVerification, HttpMethods.Post, "/mfa/verify");
    Assert(downstream(), "Exact MFA verification challenge was blocked.");
    Assert(context.Response.StatusCode == StatusCodes.Status200OK, "Exact challenge response was unexpectedly rewritten.");

    (context, downstream) = await InvokeBoundaryAsync(GsipSessionRestrictions.PasswordChange, HttpMethods.Get, "/password/change");
    Assert(downstream(), "Exact forced-password challenge was blocked.");
}

static async Task CrossChallengeAccessIsRejected()
{
    var (context, downstream) = await InvokeBoundaryAsync(GsipSessionRestrictions.MfaEnrollment, HttpMethods.Get, "/mfa/verify");
    Assert(!downstream(), "MFA enrollment session crossed into MFA verification flow.");
    Assert(context.Response.Headers.Location == "/mfa/enroll", "Cross-challenge redirect did not return to the exact required flow.");
}

static async Task UnknownRestrictionFailsClosed()
{
    var (context, downstream) = await InvokeBoundaryAsync("forged-restriction", HttpMethods.Get, "/");
    Assert(!downstream(), "Unknown restriction claim reached downstream authorization.");
    Assert(context.Response.StatusCode == StatusCodes.Status403Forbidden, "Unknown restriction claim did not fail closed.");
}

static Task PipelineOrderIsFailClosed()
{
    var root = FindRepositoryRoot();
    var program = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Program.cs"));
    var authenticationIndex = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
    var limiterIndex = program.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
    var restrictionIndex = program.IndexOf("app.UseRestrictedSessionBoundary();", StringComparison.Ordinal);
    var authorizationIndex = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
    Assert(authenticationIndex >= 0 && limiterIndex > authenticationIndex && restrictionIndex > limiterIndex && authorizationIndex > restrictionIndex,
        "Authentication/rate-limit/restricted-session/authorization middleware order is not fail closed.");
    Assert(program.Contains("ChallengeRateLimitPermitCount", StringComparison.Ordinal)
        && program.Contains("ChallengeRateLimitWindowSeconds", StringComparison.Ordinal)
        && program.Contains("Status429TooManyRequests", StringComparison.Ordinal),
        "Authenticated challenge rate limiting or HTTP 429 rejection is missing.");
    Assert(program.Contains("/mfa/enroll", StringComparison.Ordinal)
        && program.Contains("/mfa/verify", StringComparison.Ordinal)
        && program.Contains("/password/change", StringComparison.Ordinal),
        "A restricted authentication challenge is missing from the global limiter boundary.");
    return Task.CompletedTask;
}

static Task RbacRejectsRestrictedPrincipals()
{
    var root = FindRepositoryRoot();
    var authorization = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Authorization", "RbacAuthorization.cs"));
    var occurrences = CountOccurrences(authorization, "GsipSessionRestrictions.IsRestricted(principal)");
    Assert(occurrences >= 2, "Both global and service-scoped RBAC evaluation must reject restricted principals.");
    return Task.CompletedTask;
}

static Task MutatingMvcActionsRequireAntiforgery()
{
    var mutationVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };
    var controllerAssembly = typeof(ShellController).Assembly;
    var violations = new List<string>();

    foreach (var type in controllerAssembly.GetTypes()
                 .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type)))
    {
        var classProtected = type.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), inherit: true);
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            var httpAttributes = method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().ToArray();
            if (!httpAttributes.SelectMany(attribute => attribute.HttpMethods).Any(mutationVerbs.Contains))
            {
                continue;
            }

            var methodProtected = method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), inherit: true);
            var ignored = method.IsDefined(typeof(IgnoreAntiforgeryTokenAttribute), inherit: true)
                || type.IsDefined(typeof(IgnoreAntiforgeryTokenAttribute), inherit: true);
            if ((!classProtected && !methodProtected) || ignored)
            {
                violations.Add($"{type.Name}.{method.Name}");
            }
        }
    }

    Assert(violations.Count == 0, "Mutating MVC actions without antiforgery: " + string.Join(", ", violations));
    return Task.CompletedTask;
}

static Task ViewsDoNotUseHtmlRaw()
{
    var root = FindRepositoryRoot();
    var views = Path.Combine(root, "src", "GSIP.Web", "Views");
    var violations = Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
        .Where(path => File.ReadAllText(path).Contains("Html.Raw(", StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(root, path))
        .ToArray();
    Assert(violations.Length == 0, "Razor output encoding bypass detected: " + string.Join(", ", violations));
    return Task.CompletedTask;
}

static Task ExecutionResilienceIsBounded()
{
    var root = FindRepositoryRoot();
    var execution = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Execution", "GenericServiceExecutionEngine.cs"));
    Assert(execution.Contains("Math.Clamp(_options.MaxAttempts, 1, 3)", StringComparison.Ordinal), "Execution retry attempts are not capped at three.");
    Assert(execution.Contains("delay > TimeSpan.FromSeconds(2)", StringComparison.Ordinal), "Retry-After delay is not bounded.");
    Assert(execution.Contains("CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)", StringComparison.Ordinal)
        && execution.Contains("timeout.CancelAfter", StringComparison.Ordinal), "Execution timeout is not linked to caller cancellation.");
    Assert(execution.Contains("ResponseTooLarge", StringComparison.Ordinal), "Bounded-response rejection is missing.");
    Assert(execution.Contains("when (cancellationToken.IsCancellationRequested)", StringComparison.Ordinal), "Caller cancellation is not distinguished from timeout.");
    return Task.CompletedTask;
}

static Task TokenRefreshConcurrencyIsBounded()
{
    var root = FindRepositoryRoot();
    var cache = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Authentication", "InMemoryTokenCache.cs"));
    Assert(cache.Contains("ConcurrentDictionary<string, Task<TokenCacheValue>> refreshes", StringComparison.Ordinal), "Single-flight refresh registry is missing.");
    Assert(cache.Contains("refreshTask.WaitAsync(cancellationToken)", StringComparison.Ordinal), "Caller cancellation is not isolated to the wait.");
    Assert(cache.Contains("ReferenceEquals(current, completion.Task)", StringComparison.Ordinal), "Refresh cleanup can remove a newer flight.");
    return Task.CompletedTask;
}

static Task SecurityHeadersRemainHardened()
{
    var root = FindRepositoryRoot();
    var headers = File.ReadAllText(Path.Combine(root, "src", "GSIP.Web", "Security", "SecurityHeadersMiddleware.cs"));
    Assert(headers.Contains("script-src 'self'", StringComparison.Ordinal), "CSP script policy is not self-only.");
    Assert(!headers.Contains("'unsafe-eval'", StringComparison.Ordinal), "CSP allows unsafe-eval.");
    Assert(headers.Contains("object-src 'none'", StringComparison.Ordinal)
        && headers.Contains("frame-ancestors 'none'", StringComparison.Ordinal)
        && headers.Contains("X-Content-Type-Options", StringComparison.Ordinal), "Framing/content hardening regressed.");
    return Task.CompletedTask;
}

static async Task<(DefaultHttpContext Context, Func<bool> Downstream)> InvokeBoundaryAsync(
    string restriction,
    string method,
    string path)
{
    var reached = false;
    var middleware = new RestrictedSessionBoundaryMiddleware(_ =>
    {
        reached = true;
        return Task.CompletedTask;
    });
    var context = new DefaultHttpContext();
    context.Request.Method = method;
    context.Request.Path = path;
    context.User = new ClaimsPrincipal(new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim(GsipIdentityClaims.SessionRestriction, restriction)
        },
        authenticationType: "P14Synthetic"));

    await middleware.InvokeAsync(context);
    return (context, () => reached);
}

static int CountOccurrences(string value, string needle)
{
    var count = 0;
    var start = 0;
    while ((start = value.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
    {
        count++;
        start += needle.Length;
    }
    return count;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "global.json"))
            && File.Exists(Path.Combine(directory.FullName, "CURRENT_PHASE.md")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Repository root could not be located.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
