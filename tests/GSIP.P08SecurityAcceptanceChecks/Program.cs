using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Authentication;
using GSIP.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var evidenceDirectory = Environment.GetEnvironmentVariable("GSIP_P08_EVIDENCE_DIR");
if (string.IsNullOrWhiteSpace(evidenceDirectory))
    evidenceDirectory = Path.Combine("artifacts", "p08-security-acceptance");
Directory.CreateDirectory(evidenceDirectory);

var apiKeySentinel = SyntheticSecret("api");
var bearerSentinel = SyntheticSecret("bearer");
var tokenSentinel = SyntheticSecret("token");
var results = new List<CaseResult>();

var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "80000000-0000-0000-0000-000000000801")],
    "P08SyntheticAcceptance"));

await RunAsync("official-contract-fixture", OfficialContractFixtureAsync);
await RunAsync("valid-x-api-key", ValidApiKeyAsync);
await RunAsync("valid-bearer", ValidBearerAsync);
await RunAsync("missing-invalid-auth", MissingInvalidAuthAsync);
await RunAsync("forged-authprofile", ForgedAuthProfileAsync);
await RunAsync("forged-secretref", ForgedSecretRefAsync);
await RunAsync("http-status-matrix", HttpStatusMatrixAsync);
await RunAsync("bounded-retry-no-auth-storm", BoundedRetryAndNoAuthStormAsync);
await RunAsync("network-tls-timeout-cancellation", NetworkTlsTimeoutCancellationAsync);
await RunAsync("stale-wrong-scope-token", StaleAndWrongScopeTokenAsync);
await RunAsync("failed-refresh-atomicity", FailedRefreshAtomicityAsync);
await RunAsync("token-cache-single-flight", TokenCacheSingleFlightAsync);
await RunAsync("no-secret-token-diagnostics-leak", NoSecretTokenDiagnosticsLeakAsync);

var manifest = new
{
    CandidateSha = Environment.GetEnvironmentVariable("CANDIDATE_SHA") ?? "local",
    Suite = "P08::security-acceptance-ci",
    SyntheticOnly = true,
    RealCredentials = false,
    Cases = results.Select(x => new { x.Name, x.Status }).ToArray()
};
var evidencePath = Path.Combine(evidenceDirectory, "acceptance-summary.json");
await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

var failed = results.Where(x => x.Status != "PASS").ToArray();
if (failed.Length > 0)
    throw new InvalidOperationException($"P08 security acceptance failed {failed.Length} case(s); sanitized evidence contains case names only.");

Console.WriteLine($"P08_SECURITY_ACCEPTANCE=PASS;CASES={results.Count};SYNTHETIC_ONLY=true");
return;

async Task RunAsync(string name, Func<Task> action)
{
    try
    {
        await action();
        results.Add(new CaseResult(name, "PASS"));
        Console.WriteLine($"CASE={name};STATUS=PASS");
    }
    catch (Exception exception)
    {
        results.Add(new CaseResult(name, "FAIL"));
        Console.WriteLine($"CASE={name};STATUS=FAIL;TYPE={exception.GetType().Name}");
    }
}

Task OfficialContractFixtureAsync()
{
    var path = Path.Combine("docs", "moj-api-reference", "INDEX.md");
    var text = File.ReadAllText(path);
    Require(text.Contains("POST /genToken", StringComparison.Ordinal), "Official fixture lost POST /genToken.");
    Require(text.Contains("application/x-www-form-urlencoded", StringComparison.Ordinal), "Official fixture lost form content type.");
    Require(text.Contains("`username`", StringComparison.Ordinal) && text.Contains("`password`", StringComparison.Ordinal),
        "Official fixture lost documented token request fields.");
    Require(text.Contains("{ \"data\": \"string\" }", StringComparison.Ordinal), "Official fixture lost documented token response shape.");
    Require(text.Contains("x-api-key", StringComparison.OrdinalIgnoreCase), "Official fixture lost x-api-key contract.");
    Require(text.Contains("BearerAuth", StringComparison.Ordinal) && text.Contains("Bearer token", StringComparison.Ordinal),
        "Official fixture lost Bearer contract.");
    return Task.CompletedTask;
}

async Task ValidApiKeyAsync()
{
    var service = SyntheticService();
    var profile = Profile(service, AuthProfileType.ApiKeyHeader, "x-api-key", 'A');
    var resolver = new GuardedSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        profile.Secrets.Single().Reference, Encoding.UTF8.GetBytes(apiKeySentinel));
    var handler = new RecordingHandler((_, request, _) =>
    {
        Require(request.Headers.TryGetValues("x-api-key", out var values) && values.Single() == apiKeySentinel,
            "x-api-key was not attached for the exact request.");
        Require(request.Headers.Authorization is null, "ApiKeyHeader unexpectedly attached Authorization.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
    });
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = profile.Id, AuthProfileVersion = profile.Version },
        handler, profile: profile, secretResolver: resolver);

    var result = await ExecuteAsync(fixture.Engine, service);
    Require(result.Outcome == ServiceExecutionOutcome.Success && handler.CallCount == 1 && resolver.UseCalls == 1,
        "Valid x-api-key control did not execute exactly once.");
}

async Task ValidBearerAsync()
{
    var service = SyntheticService();
    var profile = Profile(service, AuthProfileType.StaticBearer, "bearer", 'B');
    var resolver = new GuardedSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        profile.Secrets.Single().Reference, Encoding.UTF8.GetBytes(bearerSentinel));
    var handler = new RecordingHandler((_, request, _) =>
    {
        Require(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == bearerSentinel,
            "Bearer token was not attached for the exact request.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
    });
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = profile.Id, AuthProfileVersion = profile.Version },
        handler, profile: profile, secretResolver: resolver);

    var result = await ExecuteAsync(fixture.Engine, service);
    Require(result.Outcome == ServiceExecutionOutcome.Success && handler.CallCount == 1 && resolver.UseCalls == 1,
        "Valid Bearer control did not execute exactly once.");
}

async Task MissingInvalidAuthAsync()
{
    var service = SyntheticService();
    var profileId = Guid.Parse("80000000-0000-0000-0000-000000000811");
    var invalid = new AuthProfileDescriptor(profileId, service.Id, CatalogEnvironmentCodes.UatId, "invalid",
        AuthProfileType.None, true, 1, "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new AuthProfileSecretDescriptor("unexpected", Ref('C'), 1)],
        [OwnerBinding(service)]);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = invalid.Id, AuthProfileVersion = invalid.Version }, handler, profile: invalid);

    var result = await ExecuteAsync(fixture.Engine, service);
    Require(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && handler.CallCount == 0,
        "Invalid auth configuration did not fail closed before transport.");
}

async Task ForgedAuthProfileAsync()
{
    var service = SyntheticService();
    var expected = Guid.Parse("80000000-0000-0000-0000-000000000812");
    var wrong = Profile(service, AuthProfileType.StaticBearer, "bearer", 'D') with { };
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = expected, AuthProfileVersion = 1 }, handler, profile: wrong);

    await ExpectThrowsAsync<ServiceExecutionRejectedException>(() => ExecuteAsync(fixture.Engine, service));
    Require(handler.CallCount == 0, "Forged AuthProfile reached transport.");
}

async Task ForgedSecretRefAsync()
{
    var service = SyntheticService();
    var profile = Profile(service, AuthProfileType.StaticBearer, "bearer", 'E');
    var resolver = new GuardedSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        Ref('F'), Encoding.UTF8.GetBytes(bearerSentinel));
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = profile.Id, AuthProfileVersion = profile.Version },
        handler, profile: profile, secretResolver: resolver);

    var result = await ExecuteAsync(fixture.Engine, service);
    Require(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
        "Forged SecretRef did not fail closed as AuthenticationUnavailable.");
    Require(result.RawResponse.Length == 0 && !result.ToString().Contains(bearerSentinel, StringComparison.Ordinal),
        "Secret rejection leaked plaintext material.");
    Require(handler.CallCount == 0, "Forged SecretRef reached transport.");
}

async Task HttpStatusMatrixAsync()
{
    var cases = new (HttpStatusCode Status, ServiceExecutionOutcome Outcome, int Attempts)[]
    {
        (HttpStatusCode.BadRequest, ServiceExecutionOutcome.BadRequest, 1),
        (HttpStatusCode.Unauthorized, ServiceExecutionOutcome.Unauthorized, 1),
        (HttpStatusCode.Forbidden, ServiceExecutionOutcome.Forbidden, 1),
        ((HttpStatusCode)429, ServiceExecutionOutcome.RateLimited, 2),
        (HttpStatusCode.InternalServerError, ServiceExecutionOutcome.ServerError, 2)
    };

    foreach (var item in cases)
    {
        var service = SyntheticService();
        var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(item.Status, "{\"error\":\"synthetic\"}")));
        var fixture = Fixture(service, Binding(service), handler,
            new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 });
        var result = await ExecuteAsync(fixture.Engine, service);
        Require(result.Outcome == item.Outcome && result.Attempts == item.Attempts && handler.CallCount == item.Attempts,
            $"HTTP {(int)item.Status} classification/retry mismatch.");
    }
}

async Task BoundedRetryAndNoAuthStormAsync()
{
    var service = SyntheticService();
    var transientHandler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}")));
    var bounded = Fixture(service, Binding(service), transientHandler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 999, RetryDelayMilliseconds = 0 });
    var transientResult = await ExecuteAsync(bounded.Engine, service);
    Require(transientResult.Attempts == 3 && transientHandler.CallCount == 3,
        "Runtime retry bound exceeded the hard maximum of three attempts.");

    var profile = Profile(service, AuthProfileType.StaticBearer, "bearer", 'G');
    var resolver = new GuardedSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        profile.Secrets.Single().Reference, Encoding.UTF8.GetBytes(bearerSentinel));
    var unauthorizedHandler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, "{}")));
    var unauthorized = Fixture(service,
        Binding(service) with { AuthProfileId = profile.Id, AuthProfileVersion = profile.Version }, unauthorizedHandler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }, profile, resolver);
    var unauthorizedResult = await ExecuteAsync(unauthorized.Engine, service);
    Require(unauthorizedResult.Outcome == ServiceExecutionOutcome.Unauthorized
        && unauthorizedResult.Attempts == 1 && unauthorizedHandler.CallCount == 1 && resolver.UseCalls == 1,
        "401 caused an authentication retry storm.");
}

async Task NetworkTlsTimeoutCancellationAsync()
{
    var service = SyntheticService();
    var network = new RecordingHandler((_, _, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic network")));
    var networkFixture = Fixture(service, Binding(service), network,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 });
    var networkResult = await ExecuteAsync(networkFixture.Engine, service);
    Require(networkResult.Outcome == ServiceExecutionOutcome.NetworkFailure && network.CallCount == 2,
        "Network failure classification/retry is incorrect.");

    var tls = new RecordingHandler((_, _, _) => Task.FromException<HttpResponseMessage>(
        new HttpRequestException("synthetic tls", new AuthenticationException("synthetic certificate"))));
    var tlsFixture = Fixture(service, Binding(service), tls);
    var tlsResult = await ExecuteAsync(tlsFixture.Engine, service);
    Require(tlsResult.Outcome == ServiceExecutionOutcome.TlsFailure && tls.CallCount == 1,
        "TLS failure must fail without generic retry.");

    var timeout = new RecordingHandler(async (_, _, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    });
    var timeoutFixture = Fixture(service, Binding(service) with { TimeoutSeconds = 1 }, timeout);
    var timeoutResult = await ExecuteAsync(timeoutFixture.Engine, service);
    Require(timeoutResult.Outcome == ServiceExecutionOutcome.Timeout, "Runtime timeout was not classified as Timeout.");

    var canceled = new RecordingHandler(async (_, _, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    });
    var canceledFixture = Fixture(service, Binding(service) with { TimeoutSeconds = 30 }, canceled);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
    await ExpectThrowsAsync<OperationCanceledException>(() => ExecuteAsync(canceledFixture.Engine, service, cts.Token));
}

async Task StaleAndWrongScopeTokenAsync()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 9, 9, 4, 45, 0, TimeSpan.Zero));
    using var cache = new InMemoryTokenCache(clock, new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var serviceId = Guid.Parse("80000000-0000-0000-0000-000000000821");
    var envId = Guid.Parse("80000000-0000-0000-0000-000000000822");
    var profileId = Guid.Parse("80000000-0000-0000-0000-000000000823");
    var baseline = TokenCacheIdentity.Create(serviceId, envId, profileId, 1, 1, "aud", ["read"]);
    var wrongScope = TokenCacheIdentity.Create(serviceId, envId, profileId, 1, 1, "aud", ["write"]);
    var calls = 0;

    var first = await cache.GetOrRefreshAsync(baseline, _ =>
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(TokenCacheValue.Create(tokenSentinel, clock.UtcNow.AddSeconds(40)));
    });
    var other = await cache.GetOrRefreshAsync(wrongScope, _ =>
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(TokenCacheValue.Create(SyntheticSecret("scope"), clock.UtcNow.AddMinutes(5)));
    });
    Require(calls == 2 && first.AccessToken != other.AccessToken, "Wrong-scope token was reused across cache identity.");

    clock.Advance(TimeSpan.FromSeconds(11));
    var refreshed = await cache.GetOrRefreshAsync(baseline, _ =>
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(TokenCacheValue.Create(SyntheticSecret("fresh"), clock.UtcNow.AddMinutes(5)));
    });
    Require(calls == 3 && refreshed.AccessToken != first.AccessToken, "Stale/near-expiry token was reused inside safety window.");
}

async Task FailedRefreshAtomicityAsync()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 9, 9, 4, 45, 0, TimeSpan.Zero));
    using var cache = new InMemoryTokenCache(clock, new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var identity = TokenCacheIdentity.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, "aud", ["read"]);
    var calls = 0;

    await ExpectThrowsAsync<InvalidOperationException>(() => cache.GetOrRefreshAsync(identity, _ =>
    {
        Interlocked.Increment(ref calls);
        throw new InvalidOperationException("synthetic acquisition failure");
    }));

    var recovered = await cache.GetOrRefreshAsync(identity, _ =>
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(TokenCacheValue.Create(tokenSentinel, clock.UtcNow.AddMinutes(5)));
    });
    Require(calls == 2 && recovered.AccessToken == tokenSentinel,
        "Failed acquisition poisoned cache or prevented a later safe refresh.");
}

async Task TokenCacheSingleFlightAsync()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 9, 9, 4, 45, 0, TimeSpan.Zero));
    using var cache = new InMemoryTokenCache(clock, new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var identity = TokenCacheIdentity.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, "aud", ["read"]);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0;

    async Task<TokenCacheValue> Refresh(CancellationToken token)
    {
        Interlocked.Increment(ref calls);
        started.TrySetResult();
        await release.Task.WaitAsync(token);
        return TokenCacheValue.Create(tokenSentinel, clock.UtcNow.AddMinutes(5));
    }

    var waiters = Enumerable.Range(0, 24).Select(_ => cache.GetOrRefreshAsync(identity, Refresh)).ToArray();
    await started.Task;
    release.TrySetResult();
    var values = await Task.WhenAll(waiters);
    Require(calls == 1 && values.All(x => x.AccessToken == tokenSentinel),
        "Concurrent token acquisition was not single-flight.");
}

async Task NoSecretTokenDiagnosticsLeakAsync()
{
    var service = SyntheticService();
    var profile = Profile(service, AuthProfileType.StaticBearer, "bearer", 'H');
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var resolver = new GuardedSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        profile.Secrets.Single().Reference, Encoding.UTF8.GetBytes(bearerSentinel));
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, "{}")));
    var fixture = Fixture(service, Binding(service) with { AuthProfileId = profile.Id, AuthProfileVersion = profile.Version },
        handler, profile: profile, secretResolver: resolver, logger: logger);
    var result = await ExecuteAsync(fixture.Engine, service);

    var tokenValue = TokenCacheValue.Create(tokenSentinel, DateTimeOffset.UtcNow.AddMinutes(5));
    var serialized = JsonSerializer.Serialize(tokenValue);
    var diagnostics = string.Join('\n', logger.Messages) + "\n" + result + "\n" + tokenValue + "\n" + serialized;
    Require(!diagnostics.Contains(apiKeySentinel, StringComparison.Ordinal)
        && !diagnostics.Contains(bearerSentinel, StringComparison.Ordinal)
        && !diagnostics.Contains(tokenSentinel, StringComparison.Ordinal),
        "Secret/token plaintext appeared in logs, results, serialization, or string diagnostics.");
    Require(!serialized.Contains("AccessToken", StringComparison.Ordinal), "TokenCacheValue serialized its token-bearing property.");
}

RuntimeFixture Fixture(
    CatalogService service,
    AuthorizedServiceExecutionBinding binding,
    RecordingHandler handler,
    ServiceExecutionRuntimeOptions? options = null,
    AuthProfileDescriptor? profile = null,
    ISecretMaterialResolver? secretResolver = null,
    RecordingLogger<GenericServiceExecutionEngine>? logger = null)
{
    var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
        [], [service], [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
    var engine = new GenericServiceExecutionEngine(
        new FakeSecurityGate(binding), metadata, new FakeAuthProfiles(profile), secretResolver ?? new GuardedSecretResolver(),
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(options ?? new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 }),
        logger ?? new RecordingLogger<GenericServiceExecutionEngine>());
    return new RuntimeFixture(engine);
}

Task<ServiceExecutionResult> ExecuteAsync(GenericServiceExecutionEngine engine, CatalogService service, CancellationToken token = default) =>
    engine.ExecuteAsync(new ServiceExecutionCommand(principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?>()), token);

static CatalogService SyntheticService() => new()
{
    Id = Guid.Parse("80000000-0000-0000-0000-000000000802"),
    DefinitionKey = Guid.Parse("80000000-0000-0000-0000-000000000803"),
    EntityId = Guid.Parse("80000000-0000-0000-0000-000000000804"),
    Code = "SYNTH-P08", NameAr = "خدمة اصطناعية", NameEn = "Synthetic P08",
    DescriptionAr = "اختبار قبول فقط", DescriptionEn = "Acceptance only", Active = true, IsCurrent = true, Version = 1,
    Fields = [], ResultMappings = []
};

static AuthorizedServiceExecutionBinding Binding(CatalogService service) => new(
    service.Id, service.Code, CatalogEnvironmentCodes.UatId, CatalogEnvironmentCodes.Uat,
    "https://p08-fake.invalid", "/synthetic", "GET", "application/json", 5,
    "SystemDefault", true, string.Empty, null, null, "{}");

static AuthProfileDescriptor Profile(CatalogService service, AuthProfileType type, string secretName, char refFill)
{
    var id = Guid.NewGuid();
    return new AuthProfileDescriptor(id, service.Id, CatalogEnvironmentCodes.UatId, "Synthetic P08", type,
        true, 1, "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new AuthProfileSecretDescriptor(secretName, Ref(refFill), 1)], [OwnerBinding(service)]);
}

static AuthProfileBindingDescriptor OwnerBinding(CatalogService service) =>
    new(service.Id, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch);

static SecretRef Ref(char fill) => SecretRef.Parse("sr1_" + new string(fill, 43));

static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static string SyntheticSecret(string label) =>
    $"P08-{label}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

sealed record CaseResult(string Name, string Status);
sealed record RuntimeFixture(GenericServiceExecutionEngine Engine);

sealed class RecordingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return responder(CallCount, request, cancellationToken);
    }
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name != "GSIP.Execution") throw new InvalidOperationException("Unexpected HttpClient name.");
        return client;
    }
}

sealed class FakeSecurityGate(AuthorizedServiceExecutionBinding binding) : IServiceExecutionSecurityGate
{
    public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(ClaimsPrincipal principal, Guid serviceId, Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        if (serviceId != binding.ServiceId || environmentId != binding.EnvironmentId)
            throw new ServiceExecutionRejectedException();
        return Task.FromResult(binding);
    }
}

sealed class FakeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
{
    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string> ExportJsonAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MetadataImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeAuthProfiles(AuthProfileDescriptor? profile) : IAuthProfileService
{
    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        profile is not null && profile.Id == authProfileId ? Task.FromResult(profile) : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) => Task.FromResult(profile);
    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName,
        SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class GuardedSecretResolver : ISecretMaterialResolver
{
    private readonly Guid serviceId;
    private readonly Guid environmentId;
    private readonly Guid profileId;
    private readonly SecretRef expectedRef;
    private readonly byte[] material;
    private readonly bool configured;

    public GuardedSecretResolver()
    {
        material = [];
    }

    public GuardedSecretResolver(Guid serviceId, Guid environmentId, Guid profileId, SecretRef expectedRef, byte[] material)
    {
        this.serviceId = serviceId;
        this.environmentId = environmentId;
        this.profileId = profileId;
        this.expectedRef = expectedRef;
        this.material = material;
        configured = true;
    }

    public int UseCalls { get; private set; }

    public async Task<TResult> UseSecretAsync<TResult>(Guid requestedServiceId, Guid requestedEnvironmentId, Guid requestedProfileId,
        string secretName, SecretRef secretRef, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        UseCalls++;
        if (!configured || requestedServiceId != serviceId || requestedEnvironmentId != environmentId
            || requestedProfileId != profileId || secretRef != expectedRef)
            throw new SecretReferenceRejectedException();
        return await operation(material, cancellationToken);
    }
}

sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}

sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}