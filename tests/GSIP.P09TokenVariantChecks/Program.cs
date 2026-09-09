using System.Net;
using System.Text;
using System.Text.Json;
using GSIP.Application.Authentication;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;

const string Username = "SYNTHETIC_P09_USER";
const string Password = "SYNTHETIC_P09_PASSWORD";
const string Geha = "SYNTHETIC_P09_GEHA";
const string ApiKey = "SYNTHETIC_P09_API_KEY";
const string Token = "SYNTHETIC_P09_TOKEN";

await MarriageCompositeTokenAsync();
await FamilyJudgmentJsonTokenAsync();
await ProcurationJsonGehaTokenAsync();
await RequiredApiKeyFailsClosedAsync();

Console.WriteLine("P09_TOKEN_VARIANT_ACCEPTANCE=PASS");
return;

async Task MarriageCompositeTokenAsync()
{
    var serviceId = Guid.Parse("91000000-0000-0000-0000-000000000129");
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/genToken",
        ["X-GSIP-TokenRequestContentType"] = "application/x-www-form-urlencoded",
        ["X-GSIP-TokenResponsePath"] = "data",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password",
        ["X-GSIP-TokenApiKeyRequired"] = true
    });
    var profile = Profile(serviceId, [Secret("username", 'A'), Secret("password", 'B'), Secret("x-api-key", 'C')]);
    var service = Service(serviceId, "https://synthetic.invalid/WSWEB/WS/v1/marriage", metadata, profile.Id);
    var handler = new RecordingHandler(async request =>
    {
        Check(request.RequestUri!.AbsolutePath == "/WSWEB/WS/v1/marriage/genToken", "Marriage token URI composition drifted.");
        Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded", "Marriage token content type drifted.");
        var body = await request.Content!.ReadAsStringAsync();
        Check(body.Contains("username=" + Uri.EscapeDataString(Username), StringComparison.Ordinal)
            && body.Contains("password=" + Uri.EscapeDataString(Password), StringComparison.Ordinal),
            "Marriage token form fields drifted.");
        Check(request.Headers.TryGetValues("x-api-key", out var values) && values.Single() == ApiKey,
            "Marriage /genToken lost proven x-api-key.");
        return Json(HttpStatusCode.OK, $"{{\"data\":\"{Token}\"}}");
    });
    var resolver = Resolver(("username", Username), ("password", Password), ("x-api-key", ApiKey));
    await Probe(service, profile, resolver, handler).TestTokenGenerationAsync(serviceId, CatalogEnvironmentCodes.UatId, profile.Id);
    Check(handler.Calls == 1, "Marriage token probe must issue exactly one request.");
    Check(resolver.Calls.All(call => call.ServiceId == serviceId && call.EnvironmentId == CatalogEnvironmentCodes.UatId && call.AuthProfileId == profile.Id),
        "Marriage token secret resolution escaped exact scope.");
}

async Task FamilyJudgmentJsonTokenAsync()
{
    var serviceId = Guid.Parse("91000000-0000-0000-0000-000000000196");
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/token",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "token",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password"
    });
    var profile = Profile(serviceId, [Secret("username", 'D'), Secret("password", 'E')]);
    var service = Service(serviceId, "https://synthetic.invalid/WSWEB/WS/v1/verdict", metadata, profile.Id);
    var handler = new RecordingHandler(async request =>
    {
        Check(request.RequestUri!.AbsolutePath == "/WSWEB/WS/v1/verdict/token", "API196 token URI composition drifted.");
        Check(request.Content?.Headers.ContentType?.MediaType == "application/json", "API196 token content type must be JSON.");
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        var root = body.RootElement;
        Check(root.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["username", "password"]),
            "API196 token JSON fields drifted.");
        Check(root.GetProperty("username").GetString() == Username && root.GetProperty("password").GetString() == Password,
            "API196 token JSON values drifted.");
        Check(!request.Headers.Contains("x-api-key"), "API196 operation-level x-api-key was invented.");
        return Json(HttpStatusCode.OK, $"{{\"token\":\"{Token}\"}}");
    });
    await Probe(service, profile, Resolver(("username", Username), ("password", Password)), handler)
        .TestTokenGenerationAsync(serviceId, CatalogEnvironmentCodes.UatId, profile.Id);
    Check(handler.Calls == 1, "API196 token probe must issue exactly one request.");
}

async Task ProcurationJsonGehaTokenAsync()
{
    var serviceId = Guid.Parse("91000000-0000-0000-0000-000000000134");
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/Authenticate/Token",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "token",
        ["X-GSIP-TokenUsernameField"] = "UserName",
        ["X-GSIP-TokenPasswordField"] = "Password",
        ["X-GSIP-TokenGehaField"] = "Geha",
        ["X-GSIP-TokenDocumentedTtlSeconds"] = 21600
    });
    var profile = Profile(serviceId, [Secret("username", 'F'), Secret("password", 'G'), Secret("geha", 'H')]);
    var service = Service(serviceId, "https://synthetic.invalid/MOJProcuration/V1/api", metadata, profile.Id);
    var handler = new RecordingHandler(async request =>
    {
        Check(request.RequestUri!.AbsolutePath == "/MOJProcuration/V1/api/Authenticate/Token", "API134 token URI composition drifted.");
        Check(request.Content?.Headers.ContentType?.MediaType == "application/json", "API134 token content type must be JSON.");
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        var root = body.RootElement;
        Check(root.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["UserName", "Password", "Geha"]),
            "API134 token JSON wire casing drifted.");
        Check(root.GetProperty("UserName").GetString() == Username
            && root.GetProperty("Password").GetString() == Password
            && root.GetProperty("Geha").GetString() == Geha,
            "API134 token JSON values drifted.");
        Check(!request.Headers.Contains("x-api-key"), "API134 operation-level x-api-key was invented.");
        return Json(HttpStatusCode.OK, $"{{\"token\":\"{Token}\"}}");
    });
    var resolver = Resolver(("username", Username), ("password", Password), ("geha", Geha));
    await Probe(service, profile, resolver, handler).TestTokenGenerationAsync(serviceId, CatalogEnvironmentCodes.UatId, profile.Id);
    Check(handler.Calls == 1, "API134 token probe must issue exactly one request.");
    Check(resolver.Calls.Select(call => call.Name).SequenceEqual(["username", "password", "geha"]),
        "API134 canonical secret resolution order/names drifted.");
}

async Task RequiredApiKeyFailsClosedAsync()
{
    var serviceId = Guid.Parse("91000000-0000-0000-0000-000000000999");
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/genToken",
        ["X-GSIP-TokenApiKeyRequired"] = true
    });
    var profile = Profile(serviceId, [Secret("username", 'I'), Secret("password", 'J')]);
    var service = Service(serviceId, "https://synthetic.invalid/scope", metadata, profile.Id);
    var handler = new RecordingHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, $"{{\"data\":\"{Token}\"}}")));
    try
    {
        await Probe(service, profile, Resolver(("username", Username), ("password", Password)), handler)
            .TestTokenGenerationAsync(serviceId, CatalogEnvironmentCodes.UatId, profile.Id);
        throw new InvalidOperationException("Missing required API key did not fail closed.");
    }
    catch (AuthenticationProbeRejectedException)
    {
        Check(handler.Calls == 0, "Missing required API key reached outbound transport.");
    }
}

MojAuthenticationProbeService Probe(CatalogService service, AuthProfileDescriptor profile, FakeSecretResolver resolver, RecordingHandler handler)
{
    var snapshot = new MetadataCatalogSnapshot(
        [], [service], [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]);
    return new MojAuthenticationProbeService(
        new FakeMetadata(snapshot),
        new FakeAuthProfiles(profile),
        resolver,
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)));
}

static CatalogService Service(Guid serviceId, string baseUrl, string metadata, Guid profileId)
{
    var service = new CatalogService
    {
        Id = serviceId,
        DefinitionKey = Guid.NewGuid(),
        EntityId = Guid.NewGuid(),
        Code = "SYNTH-P09-TOKEN",
        NameAr = "اختبار",
        NameEn = "Synthetic",
        DescriptionAr = "اختبار",
        DescriptionEn = "Synthetic",
        Active = true,
        IsCurrent = true,
        Version = 2
    };
    service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
    {
        Id = Guid.NewGuid(),
        ServiceId = serviceId,
        EnvironmentId = CatalogEnvironmentCodes.UatId,
        BaseUrl = baseUrl,
        RelativePath = "/unused-target",
        HttpMethod = "POST",
        ContentType = "application/json",
        NonSecretHeadersJson = metadata,
        TimeoutSeconds = 5,
        TlsPolicy = "SystemDefault",
        ValidateServerCertificate = true,
        ProxyUrl = string.Empty,
        HealthPath = string.Empty,
        HealthMethod = string.Empty,
        Active = true,
        LastTestStatus = string.Empty,
        AuthProfileId = profileId
    });
    return service;
}

static AuthProfileDescriptor Profile(Guid serviceId, IReadOnlyList<AuthProfileSecretDescriptor> secrets)
{
    var id = Guid.NewGuid();
    return new AuthProfileDescriptor(
        id, serviceId, CatalogEnvironmentCodes.UatId, "Synthetic P09 token", AuthProfileType.TokenEndpoint, true, 2,
        "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, secrets,
        [new AuthProfileBindingDescriptor(serviceId, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
}

static AuthProfileSecretDescriptor Secret(string name, char seed) =>
    new(name, SecretRef.Parse("sr1_" + new string(seed, 43)), 1);

static FakeSecretResolver Resolver(params (string Name, string Value)[] values) =>
    new(values.ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase));

static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record SecretScope(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string Name);

sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return await responder(request);
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

sealed class FakeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
{
    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
