using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using GSIP.Application.Execution;
using GSIP.Application.Identity;
using GSIP.Application.Security;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Execution;
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
    ("service and environment IDOR boundaries stay exact and fail closed", IdorBoundariesRemainExact),
    ("all mutating MVC actions enforce antiforgery", MutatingMvcActionsRequireAntiforgery),
    ("views do not bypass Razor output encoding", ViewsDoNotUseHtmlRaw),
    ("authentication tickets are renewed across restricted and complete sign-in", SessionFixationProtectionIsExplicit),
    ("password brute-force controls remain bounded and timing-equalized", BruteForceControlsRemainBounded),
    ("central secret redaction removes plaintext from text JSON exceptions and log facts", SecretAndLogLeakageIsRedacted),
    ("execution retries timeout and response reads remain bounded", ExecutionResilienceIsBounded),
    ("external API outage paths fail safely without retry storms", ExternalOutageBehaviorIsBounded),
    ("token refresh remains single flight and caller cancellation isolated", TokenRefreshConcurrencyIsBounded),
    ("dynamic execution inputs reject adversarial and unknown values", DynamicInputFuzzingFailsClosed),
    ("logo metadata is bounded and no file-upload surface exists", LogoAndUploadSurfaceIsConstrained),
    ("database upgrade persistence is an exact-candidate P14 gate", MigrationSafetyGateIsWired),
    ("dependency vulnerability deprecation and license review are governed", DependencyReviewGateIsWired),
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

static Task IdorBoundariesRemainExact()
{
    var root = FindRepositoryRoot();
    var gate = File.ReadAllText(Path.Combine(root, "src", "GSIP.Application", "Execution", "ServiceExecutionSecurity.cs"));
    Assert(gate.Contains("service.Id == serviceId", StringComparison.Ordinal)
        && gate.Contains("environment.Id == environmentId", StringComparison.Ordinal)
        && gate.Contains("config.ServiceId == service.Id", StringComparison.Ordinal)
        && gate.Contains("config.EnvironmentId == environmentId", StringComparison.Ordinal),
        "Execution authorization no longer resolves exact ServiceId + EnvironmentId bindings.");
    Assert(gate.Contains("HasServicePermissionAsync", StringComparison.Ordinal)
        && gate.Contains("GsipPermissions.ServicesExecute", StringComparison.Ordinal),
        "Execution authorization no longer applies exact service permission evaluation.");
    Assert(gate.Contains("catch (ServiceExecutionRejectedException)", StringComparison.Ordinal)
        && gate.Contains("catch\n        {\n            throw new ServiceExecutionRejectedException();", StringComparison.Ordinal),
        "Execution authorization no longer normalizes unexpected lookup failures to a safe fail-closed rejection.");
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

static Task SessionFixationProtectionIsExplicit()
{
    var root = FindRepositoryRoot();
    var authentication = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Identity", "AccountAuthenticationService.cs"));
    Assert(authentication.Contains("httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties)", StringComparison.Ordinal),
        "Restricted authentication does not issue a fresh application authentication ticket.");
    Assert(authentication.Contains("await _signInManager.SignInAsync(user, new AuthenticationProperties", StringComparison.Ordinal),
        "Completed authentication does not issue a fresh Identity authentication ticket.");
    Assert(CountOccurrences(authentication, "AllowRefresh = false") >= 2
        && CountOccurrences(authentication, "ExpiresUtc =") >= 2,
        "Restricted/final authentication tickets are not explicitly non-refreshable and time bounded.");
    Assert(authentication.Contains("UpdateSecurityStampAsync(user)", StringComparison.Ordinal),
        "Account disable does not invalidate existing authentication sessions through the security stamp.");
    return Task.CompletedTask;
}

static Task BruteForceControlsRemainBounded()
{
    var root = FindRepositoryRoot();
    var authentication = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Identity", "AccountAuthenticationService.cs"));
    Assert(authentication.Contains("_dummyPasswordHash", StringComparison.Ordinal)
        && authentication.Contains("VerifyHashedPassword(new ApplicationUser(), _dummyPasswordHash", StringComparison.Ordinal),
        "Unknown-user password verification is not timing-equalized.");
    Assert(authentication.Contains("user.AccessFailedCount++", StringComparison.Ordinal)
        && authentication.Contains("policy.MaxFailedAccessAttempts", StringComparison.Ordinal)
        && authentication.Contains("policy.LockoutMinutes", StringComparison.Ordinal)
        && authentication.Contains("AccountSignInStatus.LockedOut", StringComparison.Ordinal),
        "Password brute-force lockout threshold or bounded lockout period is missing.");
    return Task.CompletedTask;
}

static Task SecretAndLogLeakageIsRedacted()
{
    const string secret = "P14-SYNTHETIC-secret+value/only";
    var encoded = Uri.EscapeDataString(secret);
    var text = SecretRedaction.RedactText(
        $"Authorization: Bearer {secret}\npassword={secret}\nx-api-key={secret}\nuri={encoded}",
        new[] { secret });
    Assert(!text.Contains(secret, StringComparison.Ordinal) && !text.Contains(encoded, StringComparison.Ordinal),
        "Central text redaction leaked a synthetic secret representation.");
    Assert(text.Contains(SecretRedaction.Redacted, StringComparison.Ordinal), "Central text redaction did not emit a redaction marker.");

    var json = SecretRedaction.RedactJson($"{{\"password\":\"{secret}\",\"safe\":\"ok\"}}", new[] { secret });
    Assert(!json.Contains(secret, StringComparison.Ordinal) && json.Contains(SecretRedaction.Redacted, StringComparison.Ordinal),
        "JSON redaction leaked a sensitive value.");

    var safeException = SecretRedaction.ToSafeException(new InvalidOperationException($"api_key={secret}"), new[] { secret });
    Assert(!safeException.Message.Contains(secret, StringComparison.Ordinal), "Safe exception projection leaked a synthetic secret.");

    var root = FindRepositoryRoot();
    var execution = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Execution", "GenericServiceExecutionEngine.cs"));
    var logStart = execution.IndexOf("logger.LogInformation(", StringComparison.Ordinal);
    var logEnd = logStart < 0 ? -1 : execution.IndexOf("return new ServiceExecutionResult", logStart, StringComparison.Ordinal);
    Assert(logStart >= 0 && logEnd > logStart, "Execution completion log boundary is missing.");
    var logBlock = execution[logStart..logEnd];
    Assert(!logBlock.Contains("{RawResponse}", StringComparison.OrdinalIgnoreCase)
        && !logBlock.Contains("{Authorization}", StringComparison.OrdinalIgnoreCase)
        && !logBlock.Contains("{Secret}", StringComparison.OrdinalIgnoreCase)
        && !logBlock.Contains("{Token}", StringComparison.OrdinalIgnoreCase),
        "Execution information log template contains secret/raw-response fields.");
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

static Task ExternalOutageBehaviorIsBounded()
{
    var root = FindRepositoryRoot();
    var execution = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Execution", "GenericServiceExecutionEngine.cs"));
    Assert(execution.Contains("catch (HttpRequestException exception) when (IsTlsFailure(exception))", StringComparison.Ordinal)
        && execution.Contains("ServiceExecutionOutcome.TlsFailure", StringComparison.Ordinal),
        "TLS outage classification is missing.");
    Assert(execution.Contains("catch (HttpRequestException) when (retryAllowed && attempts < maxAttempts)", StringComparison.Ordinal)
        && execution.Contains("ServiceExecutionOutcome.NetworkFailure", StringComparison.Ordinal),
        "Network outage bounded retry/final classification is missing.");
    Assert(execution.Contains("ServiceExecutionOutcome.AuthenticationUnavailable", StringComparison.Ordinal)
        && execution.Contains("ServiceExecutionOutcome.Timeout", StringComparison.Ordinal),
        "Authentication/timeout outage classification is missing.");
    Assert(execution.Contains("BodylessMethods.Contains(binding.HttpMethod) || headerPolicy.SafeToRetry", StringComparison.Ordinal),
        "Unsafe POST retry-storm prevention has regressed.");
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

static Task DynamicInputFuzzingFailsClosed()
{
    var validator = typeof(GenericServiceExecutionEngine).GetMethod("ValidateInputs", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Generic input validator could not be reflected.");
    var fields = new[]
    {
        new ServiceFieldDefinition
        {
            Id = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            Key = "CODE",
            LabelAr = "رمز",
            LabelEn = "Code",
            FieldType = "text",
            Required = true,
            Regex = "^[A-Za-z0-9_-]+$",
            MinLength = 1,
            MaxLength = 16,
            OptionsJson = "[]",
            DisplayOrder = 1,
            Sensitive = false,
            Masking = "None"
        }
    };

    var valid = InvokeValidator(validator, fields, new Dictionary<string, string?> { ["CODE"] = "SAFE_123" });
    Assert(valid is IReadOnlyDictionary<string, object?> validValues && validValues.ContainsKey("CODE"),
        "Valid bounded dynamic input was rejected.");

    ExpectValidationError(validator, fields,
        new Dictionary<string, string?> { ["CODE"] = "SAFE", ["FORGED"] = "x" }, "FORGED", "UnknownField");
    ExpectValidationError(validator, fields,
        new Dictionary<string, string?> { ["CODE"] = new string('A', 17) }, "CODE", "MaxLength");
    ExpectValidationError(validator, fields,
        new Dictionary<string, string?> { ["CODE"] = "<script>alert(1)</script>" }, "CODE", "Pattern");
    ExpectValidationError(validator, fields,
        new Dictionary<string, string?> { ["CODE"] = "../../etc/passwd" }, "CODE", "Pattern");

    var root = FindRepositoryRoot();
    var metadata = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Metadata", "MetadataCatalogService.cs"));
    var execution = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Execution", "GenericServiceExecutionEngine.cs"));
    Assert(metadata.Contains("RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)", StringComparison.Ordinal)
        && execution.Contains("RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)", StringComparison.Ordinal)
        && execution.Contains("catch (RegexMatchTimeoutException)", StringComparison.Ordinal),
        "Metadata/runtime regex evaluation is not timeout bounded.");
    return Task.CompletedTask;
}

static Task LogoAndUploadSurfaceIsConstrained()
{
    var root = FindRepositoryRoot();
    var webRoot = Path.Combine(root, "src", "GSIP.Web");
    var uploadTokens = new[] { "IFormFile", "IFormFileCollection", "Request.Form.Files", "OpenReadStream(" };
    var uploadSurface = Directory.EnumerateFiles(webRoot, "*.cs", SearchOption.AllDirectories)
        .Select(path => (Path: path, Text: File.ReadAllText(path)))
        .Where(item => uploadTokens.Any(token => item.Text.Contains(token, StringComparison.Ordinal)))
        .Select(item => Path.GetRelativePath(root, item.Path))
        .ToArray();
    Assert(uploadSurface.Length == 0,
        "Unexpected file-upload surface exists and needs explicit P14 file validation: " + string.Join(", ", uploadSurface));

    var metadata = File.ReadAllText(Path.Combine(root, "src", "GSIP.Infrastructure", "Metadata", "MetadataCatalogService.cs"));
    Assert(metadata.Contains("logo.Length > 500 || logo.Any(char.IsControl)", StringComparison.Ordinal)
        && metadata.Contains("Logo metadata exceeds 500 printable characters.", StringComparison.Ordinal),
        "Logo metadata is not bounded to printable metadata.");
    return Task.CompletedTask;
}

static Task MigrationSafetyGateIsWired()
{
    var root = FindRepositoryRoot();
    var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "p14-security-hardening.yml"));
    Assert(workflow.Contains("migration-safety:", StringComparison.Ordinal)
        && workflow.Contains("runs-on: windows-latest", StringComparison.Ordinal)
        && workflow.Contains("verify-p07-upgrade-persistence.ps1", StringComparison.Ordinal)
        && workflow.Contains("Start SQL Server LocalDB", StringComparison.Ordinal),
        "P14 does not execute the established LocalDB upgrade/data-preservation acceptance on its exact candidate.");
    return Task.CompletedTask;
}

static Task DependencyReviewGateIsWired()
{
    var root = FindRepositoryRoot();
    var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "p14-security-hardening.yml"));
    Assert(workflow.Contains("--vulnerable --include-transitive", StringComparison.Ordinal)
        && workflow.Contains("--deprecated --include-transitive", StringComparison.Ordinal)
        && workflow.Contains("package --include-transitive --format json", StringComparison.Ordinal)
        && workflow.Contains("verify_p14_license_audit.py", StringComparison.Ordinal),
        "P14 dependency vulnerability/deprecation/license review is incomplete.");
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

static object? InvokeValidator(MethodInfo validator, IEnumerable<ServiceFieldDefinition> fields, IReadOnlyDictionary<string, string?> supplied)
{
    try
    {
        return validator.Invoke(null, new object?[] { fields, supplied });
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        throw;
    }
}

static void ExpectValidationError(
    MethodInfo validator,
    IEnumerable<ServiceFieldDefinition> fields,
    IReadOnlyDictionary<string, string?> supplied,
    string key,
    string expectedCode)
{
    try
    {
        _ = InvokeValidator(validator, fields, supplied);
        throw new InvalidOperationException($"Adversarial input '{key}' was unexpectedly accepted.");
    }
    catch (ServiceExecutionValidationException exception)
    {
        Assert(exception.Message == ServiceExecutionValidationException.SafeMessage,
            "Dynamic input rejection leaked unsafe validation detail through the exception message.");
        Assert(exception.Errors.TryGetValue(key, out var actual) && actual == expectedCode,
            $"Dynamic input '{key}' returned unexpected validation code '{actual ?? "<missing>"}'.");
    }
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
