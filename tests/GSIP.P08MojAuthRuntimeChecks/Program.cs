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

const string ApiKey = "SYNTHETIC_P08_API_KEY_31f7";
const string Username = "synthetic-user-p08";
const string Password = "SYNTHETIC_P08_PASSWORD_9c4d";
const string AccessToken = "eyJhbGciOiJub25lIn0.eyJleHAiOjQxMDI0NDQ4MDB9.synthetic-signature";
const string TokenMetadataKey = "X-GSIP-TokenEndpointPath";

var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "80000000-0000-0000-0000-000000000801")],
    "SyntheticP08"));

await ApiKeyUsesExactHeaderAndExecutionScopeAsync();
await OfficialTokenContractUsesMetadataPathAndBearerAsync();
await NonDefaultMetadataTokenPathIsHonoredAsync();
await MalformedTokenResponseFailsClosedAsync();
await MissingTokenPathAndCredentialFailBeforeTransportAsync();

Console.WriteLine("P08 MOJ authentication runtime checks passed.");
return;

async Task ApiKeyUsesExactHeaderAndExecutionScopeAsync()
{
    var service = SyntheticService();
    var profile = Profile(AuthProfileType.ApiKeyHeader, [Secret("x-api-key", 'A', 3)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["x-api-key"] = ApiKey
    });
    var handler = new RecordingHandler((_, request, _) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/runtime", "API-key request reached an unexpected path.");
        Check(request.Headers.TryGetValues("x-api-key", out var values) && values.Single() == ApiKey,
            "Official x-api-key header was not attached.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, resolver, handler, logger, new FakeTokenCache(), null);

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "API-key execution must succeed.");
    var scope = resolver.Calls.Single();
    Check(scope.ServiceId == service.Id && scope.EnvironmentId == CatalogEnvironmentCodes.UatId && scope.AuthProfileId == profile.Id,
        "API-key resolution must use exact execution scope.");
    Check(logger.Messages.All(message => !message.Contains(ApiKey, StringComparison.Ordinal)), "API key leaked to logs.");
}

async Task OfficialTokenContractUsesMetadataPathAndBearerAsync()
{
    var service = SyntheticService();
    var profile = Profile(AuthProfileType.TokenEndpoint, [Secret("username", 'B', 4), Secret("password", 'C', 7)]);
    var resolver = TokenResolver();
    var cache = new FakeTokenCache();
    var tokenCalls = 0;
    var serviceCalls = 0;
    var handler = new RecordingHandler(async (_, request, cancellationToken) =>
    {
        if (request.RequestUri!.AbsolutePath == "/genToken")
        {
            tokenCalls++;
            Check(request.Method == HttpMethod.Post, "MOJ token acquisition must POST the configured /genToken path.");
            Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded",
                "MOJ token content type must be form-urlencoded.");
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Check(body.Contains("username=" + Uri.EscapeDataString(Username), StringComparison.Ordinal)
                  && body.Contains("password=" + Uri.EscapeDataString(Password), StringComparison.Ordinal),
                "MOJ token request omitted documented username/password fields.");
            Check(request.Headers.Authorization is null, "Bearer must not be attached to token acquisition.");
            return Json(HttpStatusCode.OK, $"{{\"data\":\"{AccessToken}\"}}");
        }

        serviceCalls++;
        Check(request.RequestUri.AbsolutePath == "/runtime", "Authenticated request reached an unexpected target path.");
        Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == AccessToken,
            "Acquired MOJ token was not attached as Bearer.");
        return Json(HttpStatusCode.OK, "{\"ok\":true}");
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, resolver, handler, logger, cache, "/genToken");

    var first = await fixture.Engine.ExecuteAsync(Command(service));
    var second = await fixture.Engine.ExecuteAsync(Command(service));

    Check(first.Outcome == ServiceExecutionOutcome.Success && second.Outcome == ServiceExecutionOutcome.Success,
        "MOJ token flow must succeed.");
    Check(tokenCalls == 1 && serviceCalls == 2 && cache.RefreshCalls == 1,
        "Canonical cache must reuse the exact-scope unexpired token.");
    Check(cache.LastIdentity is not null
          && cache.LastIdentity.ServiceId == service.Id
          && cache.LastIdentity.EnvironmentId == CatalogEnvironmentCodes.UatId
          && cache.LastIdentity.AuthProfileId == profile.Id
          && cache.LastIdentity.AuthProfileVersion == profile.Version
          && cache.LastIdentity.SecretGeneration == 7,
        "Token cache identity is missing scope/version/generation isolation.");
    Check(resolver.Calls.All(call => call.ServiceId == service.Id
                                     && call.EnvironmentId == CatalogEnvironmentCodes.UatId
                                     && call.AuthProfileId == profile.Id),
        "Token credentials were resolved outside exact execution scope.");
    Check(logger.Messages.All(message => !ContainsSecret(message)), "Credential/token plaintext leaked to logs.");
    Check(!ContainsSecret(first.ToString()) && !ContainsSecret(second.ToString()), "Credential/token plaintext leaked to result diagnostics.");
}

async Task NonDefaultMetadataTokenPathIsHonoredAsync()
{
    const string configuredPath = "/synthetic-auth-route";
    var service = SyntheticService();
    var profile = Profile(AuthProfileType.TokenEndpoint, [Secret("username", 'D', 1), Secret("password", 'E', 1)]);
    var handler = new RecordingHandler((_, request, _) =>
    {
        if (request.RequestUri!.AbsolutePath == configuredPath)
            return Task.FromResult(Json(HttpStatusCode.OK, $"{{\"data\":\"{AccessToken}\"}}"));
        Check(request.RequestUri.AbsolutePath == "/runtime", "Runtime ignored metadata-driven token path.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
    });
    var fixture = Fixture(service, profile, TokenResolver(), handler,
        new RecordingLogger<GenericServiceExecutionEngine>(), new FakeTokenCache(), configuredPath);

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "Non-default metadata token path was not honored.");
    Check(handler.Paths.Count(path => path == configuredPath) == 1, "Configured synthetic token path was not requested exactly once.");
}

async Task MalformedTokenResponseFailsClosedAsync()
{
    var service = SyntheticService();
    var profile = Profile(AuthProfileType.TokenEndpoint, [Secret("username", 'F', 1), Secret("password", 'G', 1)]);
    var handler = new RecordingHandler((_, request, _) =>
    {
        Check(request.RequestUri!.AbsolutePath == "/genToken", "Malformed token response must stop at acquisition.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"data\":\"\"}"));
    });
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var fixture = Fixture(service, profile, TokenResolver(), handler, logger, new FakeTokenCache(), "/genToken");

    var result = await fixture.Engine.ExecuteAsync(Command(service));

    Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable, "Malformed token must fail closed.");
    Check(handler.CallCount == 1 && result.StatusCode is null && result.RawResponse.Length == 0,
        "Malformed token details must not reach target or result diagnostics.");
    Check(logger.Messages.All(message => !ContainsSecret(message)), "Malformed auth path leaked plaintext.");
}

async Task MissingTokenPathAndCredentialFailBeforeTransportAsync()
{
    var service = SyntheticService();
    var complete = Profile(AuthProfileType.TokenEndpoint, [Secret("username", 'H', 1), Secret("password", 'I', 1)]);
    var noPathHandler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var noPath = Fixture(service, complete, TokenResolver(), noPathHandler,
        new RecordingLogger<GenericServiceExecutionEngine>(), new FakeTokenCache(), null);
    var noPathResult = await noPath.Engine.ExecuteAsync(Command(service));
    Check(noPathResult.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable && noPathHandler.CallCount == 0,
        "Missing token endpoint metadata must fail before transport.");

    var incomplete = Profile(AuthProfileType.TokenEndpoint, [Secret("username", 'J', 1)]);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["username"] = Username });
    var missingCredentialHandler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var missingCredential = Fixture(service, incomplete, resolver, missingCredentialHandler,
        new RecordingLogger<GenericServiceExecutionEngine>(), new FakeTokenCache(), "/genToken");
    var missingCredentialResult = await missingCredential.Engine.ExecuteAsync(Command(service));
    Check(missingCredentialResult.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable
          && missingCredentialHandler.CallCount == 0 && resolver.Calls.Count == 0,
        "Missing token credential must fail before secret material or transport.");
}

FakeSecretResolver TokenResolver() => new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["username"] = Username,
    ["password"] = Password
});

RuntimeFixture Fixture(
    CatalogService service,
    AuthProfileDescriptor profile,
    ISecretMaterialResolver resolver,
    RecordingHandler handler,
    RecordingLogger<GenericServiceExecutionEngine> logger,
    ITokenCache tokenCache,
    string? tokenPath)
{
    var configuredMetadata = tokenPath is null ? "{}" : $"{{\"{TokenMetadataKey}\":\"{tokenPath}\"}}";
    var binding = new AuthorizedServiceExecutionBinding(
        service.Id, service.Code, CatalogEnvironmentCodes.UatId, CatalogEnvironmentCodes.Uat,
        "https://synthetic.invalid/api/family/", "/runtime", "GET", "application/json", 5,
        "SystemDefault", true, string.Empty, profile.Id, profile.Version, configuredMetadata);
    var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
        [], [service], [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
    var engine = new GenericServiceExecutionEngine(
        new FakeSecurityGate(binding), metadata, new FakeAuthProfiles(profile), resolver,
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }),
        logger, tokenCache);
    return new RuntimeFixture(engine);
}

ServiceExecutionCommand Command(CatalogService service) => new(
    principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>());

static AuthProfileDescriptor Profile(AuthProfileType type, IReadOnlyList<AuthProfileSecretDescriptor> secrets)
{
    var profileId = Guid.NewGuid();
    var serviceId = Guid.Parse("80000000-0000-0000-0000-000000000802");
    return new AuthProfileDescriptor(
        profileId, serviceId, CatalogEnvironmentCodes.UatId, "Synthetic P08 auth", type, true, 11,
        "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, secrets,
        [new AuthProfileBindingDescriptor(serviceId, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
}

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

static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

bool ContainsSecret(string value) =>
    value.Contains(ApiKey, StringComparison.Ordinal)
    || value.Contains(Username, StringComparison.Ordinal)
    || value.Contains(Password, StringComparison.Ordinal)
    || value.Contains(AccessToken, StringComparison.Ordinal);

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record RuntimeFixture(GenericServiceExecutionEngine Engine);
sealed record SecretScope(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string Name);

sealed class RecordingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public List<string> Paths { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        Paths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
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
        if (serviceId != binding.ServiceId || environmentId != binding.EnvironmentId)
            throw new InvalidOperationException("Security gate received wrong scope.");
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
        try { return await operation(material, cancellationToken); }
        finally { Array.Clear(material); }
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
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
