using System.Net;
using System.Security.Claims;
using System.Text;
using GSIP.Application.Authentication;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

const string apiKeySentinel = "SYNTHETIC_P08_API_KEY_31f7";
const string usernameSentinel = "synthetic-user-p08";
const string passwordSentinel = "SYNTHETIC_P08_PASSWORD_9c4d";
const string accessTokenSentinel = "eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.synthetic-signature";

var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "80000000-0000-0000-0000-000000000801")],
    "SyntheticP08"));

await ApiKeyUsesExactHeaderAndExecutionScopeAsync();
await TokenEndpointAcquiresCachesAndAttachesBearerAsync();
await MalformedTokenResponseFailsClosedWithoutLeakAsync();
await MissingTokenCredentialFailsBeforeTransportAsync();

Console.WriteLine("P08 MOJ authentication runtime checks passed.");
return;

async Task ApiKeyUsesExactHeaderAndExecutionScopeAsync()
{
    var service = SyntheticService();
    var profileId = Guid.Parse("80000000-0000-0000-0000-000000000811");
    var profile = Profile(
        profileId,
        service.Id,
        AuthProfileType.ApiKeyHeader,
        [Secret("x-api-key", 'A', 3)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["x-api-key"] = apiKeySentinel
    });
    var handler = new RecordingHandler(async (_, request, cancellationToken) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/runtime", "API-key request reached an unexpected path.");
        Check(request.Headers.TryGetValues("x-api-key", out var values)
              && values.Single() == apiKeySentinel,
            "Official x-api-key header was not attached with the resolved value.");
        await Task.Yield();
        return Json(HttpStatusCode.OK, "{\"ok\":true}");
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, resolver, handler, logger, new FakeTokenCache());

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "API-key execution must succeed.");
    Check(resolver.Calls.Count == 1, "API-key execution must resolve one secret.");
    var scope = resolver.Calls.Single();
    Check(scope.ServiceId == service.Id && scope.EnvironmentId == CatalogEnvironmentCodes.UatId && scope.AuthProfileId == profileId,
        "Secret resolution must use the exact execution Service + Environment + AuthProfile scope.");
    Check(logger.Messages.All(message => !message.Contains(apiKeySentinel, StringComparison.Ordinal)),
        "API key leaked into runtime logs.");
    Check(!result.ToString().Contains(apiKeySentinel, StringComparison.Ordinal), "API key leaked into execution result diagnostics.");
}

async Task TokenEndpointAcquiresCachesAndAttachesBearerAsync()
{
    var service = SyntheticService();
    var profileId = Guid.Parse("80000000-0000-0000-0000-000000000812");
    var profile = Profile(
        profileId,
        service.Id,
        AuthProfileType.TokenEndpoint,
        [Secret("username", 'B', 4), Secret("password", 'C', 7)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = usernameSentinel,
        ["password"] = passwordSentinel
    });
    var tokenCache = new FakeTokenCache();
    var tokenCalls = 0;
    var serviceCalls = 0;
    var handler = new RecordingHandler(async (_, request, cancellationToken) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
        {
            tokenCalls++;
            Check(request.Method == HttpMethod.Post, "MOJ token acquisition must use POST /genToken.");
            Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                "MOJ token acquisition must use application/x-www-form-urlencoded.");
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Check(body.Contains("username=" + Uri.EscapeDataString(usernameSentinel), StringComparison.Ordinal),
                "MOJ token request is missing the documented username field.");
            Check(body.Contains("password=" + Uri.EscapeDataString(passwordSentinel), StringComparison.Ordinal),
                "MOJ token request is missing the documented password field.");
            Check(request.Headers.Authorization is null, "Bearer must not be attached to /genToken acquisition.");
            return Json(HttpStatusCode.OK, $"{{\"data\":\"{accessTokenSentinel}\"}}");
        }

        serviceCalls++;
        Check(request.RequestUri.AbsolutePath == "/runtime", "Authenticated service request reached an unexpected path.");
        Check(request.Headers.Authorization?.Scheme == "Bearer"
              && request.Headers.Authorization.Parameter == accessTokenSentinel,
            "Acquired MOJ token was not attached as Bearer for the service call.");
        return Json(HttpStatusCode.OK, "{\"ok\":true}");
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, resolver, handler, logger, tokenCache);

    var first = await fixture.Engine.ExecuteAsync(Command(service));
    var second = await fixture.Engine.ExecuteAsync(Command(service));

    Check(first.Outcome == ServiceExecutionOutcome.Success && second.Outcome == ServiceExecutionOutcome.Success,
        "MOJ token endpoint execution must succeed.");
    Check(tokenCalls == 1 && serviceCalls == 2,
        "Canonical token cache must reuse an unexpired token for the same exact identity.");
    Check(tokenCache.RefreshCalls == 1 && tokenCache.GetCalls == 2, "Unexpected token-cache acquisition behavior.");
    Check(tokenCache.LastIdentity is not null
          && tokenCache.LastIdentity.ServiceId == service.Id
          && tokenCache.LastIdentity.EnvironmentId == CatalogEnvironmentCodes.UatId
          && tokenCache.LastIdentity.AuthProfileId == profileId
          && tokenCache.LastIdentity.AuthProfileVersion == profile.Version
          && tokenCache.LastIdentity.SecretGeneration == 7,
        "Token cache identity must include exact Service + Environment + AuthProfile/version/secret generation.");
    Check(resolver.Calls.All(call => call.ServiceId == service.Id
                                     && call.EnvironmentId == CatalogEnvironmentCodes.UatId
                                     && call.AuthProfileId == profileId),
        "Token credentials must resolve under exact execution scope.");
    Check(logger.Messages.All(message => !ContainsAnySecret(message)), "Credential/token plaintext leaked to runtime logs.");
    Check(!ContainsAnySecret(first.ToString()) && !ContainsAnySecret(second.ToString()),
        "Credential/token plaintext leaked to execution result diagnostics.");
}

async Task MalformedTokenResponseFailsClosedWithoutLeakAsync()
{
    var service = SyntheticService();
    var profile = Profile(
        Guid.Parse("80000000-0000-0000-0000-000000000813"),
        service.Id,
        AuthProfileType.TokenEndpoint,
        [Secret("username", 'D', 1), Secret("password", 'E', 1)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = usernameSentinel,
        ["password"] = passwordSentinel
    });
    var handler = new RecordingHandler((_, request, _) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "Malformed-token test must stop at token acquisition.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"data\":\"\"}"));
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, resolver, handler, logger, new FakeTokenCache());

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
        "Empty/malformed token responses must fail closed as AuthenticationUnavailable.");
    Check(handler.CallCount == 1, "Malformed token response must never reach the target service.");
    Check(result.StatusCode is null && result.RawResponse.Length == 0, "Auth failure must not expose token endpoint response details.");
    Check(logger.Messages.All(message => !ContainsAnySecret(message)), "Auth failure logging disclosed secret/token plaintext.");
}

async Task MissingTokenCredentialFailsBeforeTransportAsync()
{
    var service = SyntheticService();
    var profile = Profile(
        Guid.Parse("80000000-0000-0000-0000-000000000814"),
        service.Id,
        AuthProfileType.TokenEndpoint,
        [Secret("username", 'F', 1)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = usernameSentinel
    });
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, profile, resolver, handler, new RecordingLogger<GenericServiceExecutionEngine>(), new FakeTokenCache());

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
        "Missing token credential configuration must fail closed.");
    Check(handler.CallCount == 0 && resolver.Calls.Count == 0,
        "Invalid token profile configuration must fail before secret resolution or transport.");
}

RuntimeFixture Fixture(
    CatalogService service,
    AuthProfileDescriptor profile,
    ISecretMaterialResolver resolver,
    RecordingHandler handler,
    RecordingLogger<GenericServiceExecutionEngine> logger,
    ITokenCache tokenCache)
{
    var binding = Binding(service, profile);
    var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
        [], [service], [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
    var engine = new GenericServiceExecutionEngine(
        new FakeSecurityGate(binding),
        metadata,
        new FakeAuthProfiles(profile),
        resolver,
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }),
        logger,
        tokenCache);
    return new RuntimeFixture(engine);
}

ServiceExecutionCommand Command(CatalogService service) => new(
    principal,
    service.Id,
    CatalogEnvironmentCodes.UatId,
    new Dictionary<string, string?>());

static AuthProfileDescriptor Profile(
    Guid profileId,
    Guid serviceId,
    AuthProfileType type,
    IReadOnlyList<AuthProfileSecretDescriptor> secrets) => new(
        profileId,
        serviceId,
        CatalogEnvironmentCodes.UatId,
        "Synthetic P08 auth",
        type,
        true,
        11,
        "synthetic",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        secrets,
        [new AuthProfileBindingDescriptor(serviceId, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);

static AuthProfileSecretDescriptor Secret(string name, char seed, int generation) =>
    new(name, SecretRef.Parse("sr1_" + new string(seed, 43)), generation);

static CatalogService SyntheticService() => new()
{
    Id = Guid.Parse("80000000-0000-0000-0000-000000000802"),
    DefinitionKey = Guid.Parse("80000000-0000-0000-0000-000000000803"),
    EntityId = Guid.Parse("80000000-0000-0000-0000-000000000804"),
    Code = "SYNTH-P08-MOJ",
    NameAr = "خدمة تحقق اصطناعية",
    NameEn = "Synthetic P08 MOJ",
    DescriptionAr = "اختبار فقط",
    DescriptionEn = "Synthetic test only",
    Active = true,
    IsCurrent = true,
    Version = 1,
    Fields = [],
    ResultMappings = []
};

static AuthorizedServiceExecutionBinding Binding(CatalogService service, AuthProfileDescriptor profile) => new(
    service.Id,
    service.Code,
    CatalogEnvironmentCodes.UatId,
    CatalogEnvironmentCodes.Uat,
    "https://synthetic.invalid/api/family/",
    "/runtime",
    "GET",
    "application/json",
    5,
    "SystemDefault",
    true,
    string.Empty,
    profile.Id,
    profile.Version,
    "{}");

static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

bool ContainsAnySecret(string value) =>
    value.Contains(apiKeySentinel, StringComparison.Ordinal)
    || value.Contains(usernameSentinel, StringComparison.Ordinal)
    || value.Contains(passwordSentinel, StringComparison.Ordinal)
    || value.Contains(accessTokenSentinel, StringComparison.Ordinal);

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record RuntimeFixture(GenericServiceExecutionEngine Engine);
sealed record SecretScope(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string Name);

sealed class RecordingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return await responder(CallCount, request, cancellationToken);
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
    public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        Check(serviceId == binding.ServiceId && environmentId == binding.EnvironmentId,
            "Security gate must receive exact requested scope.");
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

sealed class FakeAuthProfiles(AuthProfileDescriptor profile) : IAuthProfileService
{
    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        authProfileId == profile.Id ? Task.FromResult(profile) : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) => Task.FromResult<AuthProfileDescriptor?>(profile);
    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeSecretResolver(IReadOnlyDictionary<string, string> values) : ISecretMaterialResolver
{
    public List<SecretScope> Calls { get; } = [];

    public async Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new SecretScope(serviceId, environmentId, authProfileId, secretName));
        if (!values.TryGetValue(secretName, out var value)) throw new SecretReferenceRejectedException();
        var material = Encoding.UTF8.GetBytes(value);
        try
        {
            return await operation(material, cancellationToken);
        }
        finally
        {
            Array.Clear(material);
        }
    }
}

sealed class FakeTokenCache : ITokenCache
{
    private string? key;
    private TokenCacheValue? value;
    public int GetCalls { get; private set; }
    public int RefreshCalls { get; private set; }
    public TokenCacheIdentity? LastIdentity { get; private set; }

    public async Task<TokenCacheValue> GetOrRefreshAsync(
        TokenCacheIdentity identity,
        Func<CancellationToken, Task<TokenCacheValue>> refreshFactory,
        CancellationToken cancellationToken = default)
    {
        GetCalls++;
        LastIdentity = identity;
        var currentKey = identity.ToCacheKey();
        if (value is not null && string.Equals(key, currentKey, StringComparison.Ordinal)) return value;
        RefreshCalls++;
        value = await refreshFactory(cancellationToken);
        key = currentKey;
        return value;
    }
}

sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
