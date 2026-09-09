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

var username = SyntheticValue("user");
var password = SyntheticValue("pass");
var bearer = BuildSyntheticJwt(DateTimeOffset.UtcNow.AddHours(2));
var results = new List<(string Name, bool Passed)>();
var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "81000000-0000-0000-0000-000000000801")],
    "P08IndependentMojAcceptance"));

await Run("valid-genToken-bearer", ValidTokenFlowAsync);
await Run("malformed-empty-token-response", MalformedTokenResponsesAsync);
await Run("token-http-error-matrix", TokenHttpErrorsAsync);
await Run("token-network-tls", TokenNetworkAndTlsAsync);
await Run("token-timeout-cancellation", TokenTimeoutAndCancellationAsync);
await Run("failed-acquisition-atomicity", FailedAcquisitionIsNotCachedAsync);
await Run("target-401-no-auth-storm", TargetUnauthorizedDoesNotReacquireAsync);
await Run("token-diagnostics-no-plaintext", TokenDiagnosticsDoNotLeakAsync);

if (results.Any(x => !x.Passed))
    throw new InvalidOperationException($"Independent MOJ runtime acceptance failed {results.Count(x => !x.Passed)} case(s).");

Console.WriteLine($"P08_MOJ_RUNTIME_ACCEPTANCE=PASS;CASES={results.Count};SYNTHETIC_ONLY=true");
return;

async Task Run(string name, Func<Task> action)
{
    try
    {
        await action();
        results.Add((name, true));
        Console.WriteLine($"CASE={name};STATUS=PASS");
    }
    catch (Exception ex)
    {
        results.Add((name, false));
        Console.WriteLine($"CASE={name};STATUS=FAIL;TYPE={ex.GetType().Name}");
    }
}

async Task ValidTokenFlowAsync()
{
    var fixture = NewTokenFixture(async (_, request, token) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
        {
            Check(request.Method == HttpMethod.Post, "Token acquisition must POST /genToken.");
            Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                "Token acquisition content type is not form-urlencoded.");
            Check(request.Headers.Authorization is null, "Bearer was attached to token acquisition.");
            var body = await request.Content!.ReadAsStringAsync(token);
            Check(FormContains(body, "username", username) && FormContains(body, "password", password),
                "Token request omitted documented username/password fields.");
            return Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}");
        }

        Check(request.RequestUri.AbsolutePath == "/business", "Authenticated request reached unexpected target path.");
        Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == bearer,
            "Acquired token was not attached as Bearer.");
        return Json(HttpStatusCode.OK, "{\"ok\":true}");
    });

    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    Check(result.Outcome == ServiceExecutionOutcome.Success, "Valid token flow did not succeed.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 1,
        "Valid token flow must acquire once and call business endpoint once.");
    Check(fixture.Resolver.Calls == 2, "Token flow must resolve exactly username and password.");
}

async Task MalformedTokenResponsesAsync()
{
    var bodies = new[]
    {
        string.Empty,
        "not-json",
        "{}",
        "{\"data\":null}",
        "{\"data\":\"\"}"
    };

    foreach (var body in bodies)
    {
        var fixture = NewTokenFixture((_, request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/genToken", "Malformed token response reached business endpoint.");
            return Task.FromResult(Json(HttpStatusCode.OK, body));
        });
        var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
            "Malformed/empty token response did not fail closed.");
        Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 0,
            "Malformed token response must stop before business transport.");
        Check(result.RawResponse.Length == 0, "Token endpoint response leaked through execution result.");
    }
}

async Task TokenHttpErrorsAsync()
{
    foreach (var status in new[]
             {
                 HttpStatusCode.BadRequest,
                 HttpStatusCode.Unauthorized,
                 HttpStatusCode.Forbidden,
                 (HttpStatusCode)429,
                 HttpStatusCode.InternalServerError,
                 HttpStatusCode.ServiceUnavailable
             })
    {
        var fixture = NewTokenFixture((_, request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/genToken", "Token HTTP error reached business endpoint.");
            return Task.FromResult(Json(status, "{\"error\":\"synthetic\"}"));
        });
        var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
        Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
            $"Token endpoint {(int)status} did not fail as authentication unavailable.");
        Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 0,
            "Token endpoint error caused retry storm or business call.");
    }
}

async Task TokenNetworkAndTlsAsync()
{
    var network = NewTokenFixture((_, request, _) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "Network failure test reached business endpoint.");
        return Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic network failure"));
    });
    var networkResult = await network.Engine.ExecuteAsync(Command(network.Service));
    Check(networkResult.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && network.Handler.TokenCalls == 1,
        "Token network failure must fail closed without retry storm.");

    var tls = NewTokenFixture((_, request, _) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "TLS failure test reached business endpoint.");
        return Task.FromException<HttpResponseMessage>(
            new HttpRequestException("synthetic tls failure", new AuthenticationException("synthetic certificate failure")));
    });
    var tlsResult = await tls.Engine.ExecuteAsync(Command(tls.Service));
    Check(tlsResult.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && tls.Handler.TokenCalls == 1,
        "Token TLS failure must fail closed without retry storm.");
}

async Task TokenTimeoutAndCancellationAsync()
{
    var timeout = NewTokenFixture(async (_, request, token) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "Timeout test reached business endpoint.");
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    }, timeoutSeconds: 1);
    var timeoutResult = await timeout.Engine.ExecuteAsync(Command(timeout.Service));
    Check(timeoutResult.Outcome == ServiceExecutionOutcome.Timeout, "Token acquisition timeout was not classified as Timeout.");

    var canceled = NewTokenFixture(async (_, request, token) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "Cancellation test reached business endpoint.");
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    }, timeoutSeconds: 30);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
    await ExpectThrowsAsync<OperationCanceledException>(() => canceled.Engine.ExecuteAsync(Command(canceled.Service), cts.Token));
}

async Task FailedAcquisitionIsNotCachedAsync()
{
    var acquisition = 0;
    var fixture = NewTokenFixture((_, request, _) =>
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
        "Failed token acquisition poisoned subsequent recovery.");
    Check(fixture.Handler.TokenCalls == 2 && fixture.Handler.BusinessCalls == 1,
        "Failed acquisition was cached or retried internally instead of remaining atomic.");
}

async Task TargetUnauthorizedDoesNotReacquireAsync()
{
    var fixture = NewTokenFixture((_, request, _) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
            return Task.FromResult(Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}"));
        return Task.FromResult(Json(HttpStatusCode.Unauthorized, "{}"));
    });

    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    Check(result.Outcome == ServiceExecutionOutcome.Unauthorized && result.Attempts == 1,
        "Business 401 triggered generic/auth retry.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.BusinessCalls == 1,
        "Business 401 caused token reacquisition storm.");
}

async Task TokenDiagnosticsDoNotLeakAsync()
{
    var fixture = NewTokenFixture((_, request, _) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
            return Task.FromResult(Json(HttpStatusCode.OK, $"{{\"data\":\"{bearer}\"}}"));
        return Task.FromResult(Json(HttpStatusCode.Forbidden, "{}"));
    });
    var result = await fixture.Engine.ExecuteAsync(Command(fixture.Service));
    var diagnostics = string.Join('\n', fixture.Logger.Messages) + "\n" + result;
    Check(!diagnostics.Contains(username, StringComparison.Ordinal)
          && !diagnostics.Contains(password, StringComparison.Ordinal)
          && !diagnostics.Contains(bearer, StringComparison.Ordinal),
        "Credential/token plaintext leaked into logs or result diagnostics.");
}

TokenFixture NewTokenFixture(
    Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
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
        "SystemDefault", true, string.Empty, profile.Id, profile.Version, "{}");
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
    var clock = new FixedClock(DateTimeOffset.UtcNow);
    var cache = new InMemoryTokenCache(clock, new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var engine = new GenericServiceExecutionEngine(
        new FixedSecurityGate(binding), metadata, new FixedAuthProfiles(profile), resolver,
        new FixedHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }),
        logger, cache);
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
    DescriptionAr = "اصطناعي", DescriptionEn = "synthetic", Active = true, IsCurrent = true,
    Fields = [], ResultMappings = []
};

static SecretRef Ref(char fill) => SecretRef.Parse("sr1_" + new string(fill, 43));

static string SyntheticValue(string label) =>
    $"p08-{label}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

static string BuildSyntheticJwt(DateTimeOffset expiry)
{
    static string B64Url(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return $"{B64Url("{\"alg\":\"none\"}")}.{B64Url($"{{\"exp\":{expiry.ToUnixTimeSeconds()}}}")}.synthetic";
}

static bool FormContains(string formBody, string name, string value) =>
    formBody.Split('&', StringSplitOptions.RemoveEmptyEntries)
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
    catch (T ex) { return ex; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

sealed record TokenFixture(
    GenericServiceExecutionEngine Engine,
    CatalogService Service,
    CountingHandler Handler,
    MultiSecretResolver Resolver,
    RecordingLogger<GenericServiceExecutionEngine> Logger,
    InMemoryTokenCache Cache);

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
    public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(ClaimsPrincipal principal, Guid serviceId, Guid environmentId,
        CancellationToken cancellationToken = default) =>
        serviceId == binding.ServiceId && environmentId == binding.EnvironmentId
            ? Task.FromResult(binding)
            : Task.FromException<AuthorizedServiceExecutionBinding>(new ServiceExecutionRejectedException());
}

sealed class FixedMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
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
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) => Task.FromResult<AuthProfileDescriptor?>(profile);
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
