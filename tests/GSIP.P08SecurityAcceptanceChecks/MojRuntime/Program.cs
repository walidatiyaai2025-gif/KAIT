using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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

var username = Synthetic("user");
var password = Synthetic("pass");
var bearer = SyntheticJwt(DateTimeOffset.UtcNow.AddHours(2));
var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "81000000-0000-0000-0000-000000000801")],
    "P08IndependentMojAcceptance"));
var cases = new List<(string Name, bool Passed)>();

await Run("valid-genToken-bearer", ValidTokenFlowAsync);
await Run("missing-invalid-token-path", MissingInvalidTokenPathAsync);
await Run("malformed-empty-token-response", MalformedTokenResponsesAsync);
await Run("token-http-error-matrix", TokenHttpErrorsAsync);
await Run("token-network-tls", TokenNetworkAndTlsAsync);
await Run("token-timeout-cancellation", TokenTimeoutAndCancellationAsync);
await Run("failed-acquisition-atomicity", FailedAcquisitionAtomicityAsync);
await Run("target-401-no-auth-storm", TargetUnauthorizedNoStormAsync);
await Run("token-diagnostics-no-plaintext", TokenDiagnosticsNoLeakAsync);

if (cases.Any(item => !item.Passed))
    throw new InvalidOperationException($"Independent MOJ runtime acceptance failed {cases.Count(item => !item.Passed)} case(s).");

Console.WriteLine($"P08_MOJ_RUNTIME_ACCEPTANCE=PASS;CASES={cases.Count};SYNTHETIC_ONLY=true");
return;

async Task Run(string name, Func<Task> action)
{
    try
    {
        await action();
        cases.Add((name, true));
        Console.WriteLine($"CASE={name};STATUS=PASS");
    }
    catch (Exception exception)
    {
        cases.Add((name, false));
        Console.WriteLine($"CASE={name};STATUS=FAIL;TYPE={exception.GetType().Name}");
    }
}

async Task ValidTokenFlowAsync()
{
    using var fixture = NewFixture(async (_, request, token) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
        {
            Check(request.Method == HttpMethod.Post, "MOJ token request is not POST.");
            Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                "MOJ token request is not form-urlencoded.");
            Check(request.Headers.Authorization is null, "Bearer was attached to /genToken.");
            var form = await request.Content!.ReadAsStringAsync(token);
            Check(FormContains(form, "username", username) && FormContains(form, "password", password),
                "Documented username/password fields were not sent.");
            return Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}");
        }

        Check(request.RequestUri.AbsolutePath == "/moj/business", "Unexpected business target.");
        Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == bearer,
            "Acquired token was not attached as Bearer.");
        Check(!request.Headers.Contains("X-GSIP-TokenEndpointPath"), "Internal token-path metadata leaked as an outbound header.");
        return Json(HttpStatusCode.OK, "{\"ok\":true}");
    });

    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    Check(result.Outcome == ServiceExecutionOutcome.Success, "Valid /genToken flow failed.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 1 && fixture.Resolver.Calls == 2,
        "Valid flow did not perform exactly one acquisition, one business call, and two secret uses.");
}

async Task MissingInvalidTokenPathAsync()
{
    foreach (var metadata in new[] { "{}", "{\"X-GSIP-TokenEndpointPath\":\"\"}", "{\"X-GSIP-TokenEndpointPath\":\"https://evil.invalid/genToken\"}", "{\"X-GSIP-TokenEndpointPath\":\"//evil.invalid/genToken\"}" })
    {
        using var fixture = NewFixture((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")), metadata);
        var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && fixture.Handler.Calls == 0,
            "Missing/invalid token path did not fail closed before transport.");
    }
}

async Task MalformedTokenResponsesAsync()
{
    foreach (var body in new[] { string.Empty, "not-json", "{}", "{\"data\":null}", "{\"data\":\"\"}" })
    {
        using var fixture = NewFixture((_, request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/genToken", "Malformed token response reached business transport.");
            return Task.FromResult(Json(HttpStatusCode.OK, body));
        });
        var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable, "Malformed token response did not fail closed.");
        Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 0 && result.RawResponse.Length == 0,
            "Malformed token response leaked or triggered business transport.");
    }
}

async Task TokenHttpErrorsAsync()
{
    foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
                 (HttpStatusCode)429, HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
    {
        using var fixture = NewFixture((_, request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/genToken", "Token error test reached business transport.");
            return Task.FromResult(Json(status, "{\"error\":\"synthetic\"}"));
        });
        var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
            $"Token endpoint {(int)status} was not fail-closed.");
        Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 0,
            "Token endpoint error caused an authentication retry storm.");
    }
}

async Task TokenNetworkAndTlsAsync()
{
    using (var network = NewFixture((_, _, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic network"))))
    {
        var result = await network.Engine.ExecuteAsync(Command(network.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && network.Handler.TokenCalls == 1,
            "Token network failure did not fail closed exactly once.");
    }

    using (var tls = NewFixture((_, _, _) => Task.FromException<HttpResponseMessage>(
               new HttpRequestException("synthetic tls", new AuthenticationException("synthetic certificate")))))
    {
        var result = await tls.Engine.ExecuteAsync(Command(tls.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && tls.Handler.TokenCalls == 1,
            "Token TLS failure did not fail closed exactly once.");
    }
}

async Task TokenTimeoutAndCancellationAsync()
{
    using (var timeout = NewFixture(async (_, _, token) =>
           {
               await Task.Delay(TimeSpan.FromSeconds(30), token);
               return Json(HttpStatusCode.OK, "{}");
           }, timeoutSeconds: 1))
    {
        var result = await timeout.Engine.ExecuteAsync(Command(timeout.Service));
        Check(result.Outcome == ServiceExecutionOutcome.Timeout, "Token acquisition timeout was not classified as Timeout.");
    }

    using var canceled = NewFixture(async (_, _, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    }, timeoutSeconds: 30);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
    await ExpectThrowsAsync<OperationCanceledException>(() => canceled.Engine.ExecuteAsync(Command(canceled.Service), cts.Token));
}

async Task FailedAcquisitionAtomicityAsync()
{
    var acquisition = 0;
    using var fixture = NewFixture((_, request, _) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
        {
            acquisition++;
            return Task.FromResult(acquisition == 1
                ? Json(HttpStatusCode.ServiceUnavailable, "{}")
                : Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}"));
        }
        return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
    });

    var first = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    var second = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    Check(first.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && second.Outcome == ServiceExecutionOutcome.Success,
        "Failed token acquisition poisoned later recovery.");
    Check(fixture.Handler.TokenCalls == 2 && fixture.Handler.BusinessCalls == 1,
        "Failed token acquisition was cached or internally retried.");
}

async Task TargetUnauthorizedNoStormAsync()
{
    using var fixture = NewFixture((_, request, _) => Task.FromResult(
        request.RequestUri!.AbsolutePath == "/genToken"
            ? Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}")
            : Json(HttpStatusCode.Unauthorized, "{}")));

    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    Check(result.Outcome == ServiceExecutionOutcome.Unauthorized && result.Attempts == 1,
        "Business 401 was retried.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 1,
        "Business 401 caused token reacquisition storm.");
}

async Task TokenDiagnosticsNoLeakAsync()
{
    using var fixture = NewFixture((_, request, _) => Task.FromResult(
        request.RequestUri!.AbsolutePath == "/genToken"
            ? Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}")
            : Json(HttpStatusCode.Forbidden, "{}")));
    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    var diagnostics = string.Join('\n', fixture.Logger.Messages) + "\n" + result;
    Check(!diagnostics.Contains(username, StringComparison.Ordinal)
          && !diagnostics.Contains(password, StringComparison.Ordinal)
          && !diagnostics.Contains(bearer, StringComparison.Ordinal),
        "Credential/token plaintext leaked into diagnostics.");
}

TokenFixture NewFixture(
    Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
    string metadataJson = "{\"X-GSIP-TokenEndpointPath\":\"/genToken\"}",
    int timeoutSeconds = 5)
{
    var service = Service();
    var profileId = Guid.NewGuid();
    var usernameRef = Ref('U');
    var passwordRef = Ref('P');
    var profile = new AuthProfileDescriptor(
        profileId, service.Id, CatalogEnvironmentCodes.UatId, "Synthetic Token Profile", AuthProfileType.TokenEndpoint,
        true, 4, "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new AuthProfileSecretDescriptor("username", usernameRef, 2), new AuthProfileSecretDescriptor("password", passwordRef, 3)],
        [new AuthProfileBindingDescriptor(service.Id, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
    var binding = new AuthorizedServiceExecutionBinding(
        service.Id, service.Code, CatalogEnvironmentCodes.UatId, CatalogEnvironmentCodes.Uat,
        "https://p08-fake.invalid/moj/", "/business", "GET", "application/json", timeoutSeconds,
        "SystemDefault", true, string.Empty, profile.Id, profile.Version, metadataJson);
    var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
        [], [service], [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
    var resolver = new MultiSecretResolver(service.Id, CatalogEnvironmentCodes.UatId, profile.Id,
        new Dictionary<SecretRef, byte[]>
        {
            [usernameRef] = Encoding.UTF8.GetBytes(username),
            [passwordRef] = Encoding.UTF8.GetBytes(password)
        });
    var handler = new CountingHandler(responder);
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var cache = new InMemoryTokenCache(new FixedClock(DateTimeOffset.UtcNow), new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var engine = new GenericServiceExecutionEngine(
        new FixedSecurityGate(binding), metadata, new FixedAuthProfiles(profile), resolver,
        new FixedHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }), logger, cache);
    return new TokenFixture(engine, service, handler, resolver, logger, cache);
}

ServiceExecutionCommand Command(CatalogService service) =>
    new(principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>());

static CatalogService Service() => new()
{
    Id = Guid.Parse("81000000-0000-0000-0000-000000000802"),
    DefinitionKey = Guid.Parse("81000000-0000-0000-0000-000000000803"),
    EntityId = Guid.Parse("81000000-0000-0000-0000-000000000804"),
    Code = "SYNTH-P08-ACCEPT", NameAr = "اختبار قبول", NameEn = "P08 acceptance",
    DescriptionAr = "اصطناعي", DescriptionEn = "synthetic", Active = true, IsCurrent = true, Fields = [], ResultMappings = []
};

static SecretRef Ref(char fill) => SecretRef.Parse("sr1_" + new string(fill, 43));
static string Synthetic(string label) => $"p08-{label}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

static string SyntheticJwt(DateTimeOffset expiry)
{
    static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return $"{B64("{\"alg\":\"none\"}")}.{B64($"{{\"exp\":{expiry.ToUnixTimeSeconds()}}}")}.synthetic";
}

static bool FormContains(string body, string name, string value) => body.Split('&', StringSplitOptions.RemoveEmptyEntries)
    .Select(part => part.Split('=', 2))
    .Any(parts => parts.Length == 2
                  && Uri.UnescapeDataString(parts[0].Replace('+', ' ')) == name
                  && Uri.UnescapeDataString(parts[1].Replace('+', ' ')) == value);

static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task<T> ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T exception) { return exception; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

sealed class TokenFixture(
    GenericServiceExecutionEngine engine,
    CatalogService service,
    CountingHandler handler,
    MultiSecretResolver resolver,
    RecordingLogger<GenericServiceExecutionEngine> logger,
    InMemoryTokenCache cache) : IDisposable
{
    public GenericServiceExecutionEngine Engine { get; } = engine;
    public CatalogService Service { get; } = service;
    public CountingHandler Handler { get; } = handler;
    public MultiSecretResolver Resolver { get; } = resolver;
    public RecordingLogger<GenericServiceExecutionEngine> Logger { get; } = logger;
    public void Dispose() => cache.Dispose();
}

sealed class CountingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public int TokenCalls { get; private set; }
    public int BusinessCalls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (request.RequestUri?.AbsolutePath == "/genToken") TokenCalls++; else BusinessCalls++;
        return await responder(Calls, request, cancellationToken);
    }
}

sealed class MultiSecretResolver(Guid serviceId, Guid environmentId, Guid profileId, IReadOnlyDictionary<SecretRef, byte[]> values)
    : ISecretMaterialResolver
{
    public int Calls { get; private set; }
    public async Task<TResult> UseSecretAsync<TResult>(Guid requestedServiceId, Guid requestedEnvironmentId, Guid requestedProfileId,
        string secretName, SecretRef secretRef, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        if (requestedServiceId != serviceId || requestedEnvironmentId != environmentId || requestedProfileId != profileId
            || !values.TryGetValue(secretRef, out var value))
            throw new SecretReferenceRejectedException();
        return await operation(value, cancellationToken);
    }
}

sealed class FixedSecurityGate(AuthorizedServiceExecutionBinding binding) : IServiceExecutionSecurityGate
{
    public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(ClaimsPrincipal subject, Guid serviceId, Guid environmentId,
        CancellationToken cancellationToken = default) =>
        serviceId == binding.ServiceId && environmentId == binding.EnvironmentId
            ? Task.FromResult(binding)
            : Task.FromException<AuthorizedServiceExecutionBinding>(new ServiceExecutionRejectedException());
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

sealed class FixedAuthProfiles(AuthProfileDescriptor profile) : IAuthProfileService
{
    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        authProfileId == profile.Id ? Task.FromResult(profile) : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthProfileDescriptor?>(profile);
    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName,
        SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => name == "GSIP.Execution" ? client : throw new InvalidOperationException("Unexpected client name.");
}

sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}