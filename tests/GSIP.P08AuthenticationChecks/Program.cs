using System.Net;
using System.Text;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;

const string Username = "SYNTHETIC_P08_ADMIN_USER";
const string Password = "SYNTHETIC_P08_ADMIN_PASSWORD";
const string TokenPathKey = "X-GSIP-TokenEndpointPath";

await SuccessfulProbeUsesExactContractAndScopeAsync();
await UnauthorizedProbeFailsClosedWithoutLeakAsync();
await ExpiredJwtProbeFailsClosedAsync();
await ForgedBindingFailsBeforeSecretsOrTransportAsync();

Console.WriteLine("P08 captain authentication probe checks passed.");
return;

async Task SuccessfulProbeUsesExactContractAndScopeAsync()
{
    var fixture = CreateFixture(HttpStatusCode.OK, FutureJwt(), exactBinding: true);
    await fixture.Probe.TestTokenGenerationAsync(fixture.ServiceId, fixture.EnvironmentId, fixture.ProfileId);

    Check(fixture.Handler.CallCount == 1, "Successful probe must call token endpoint exactly once.");
    Check(fixture.Handler.LastPath == "/genToken", "Probe ignored metadata token path.");
    Check(fixture.Handler.LastMethod == HttpMethod.Post, "Probe must POST token request.");
    Check(fixture.Handler.LastContentType == "application/x-www-form-urlencoded", "Probe content type is not form-urlencoded.");
    Check(fixture.Handler.LastBody?.Contains("username=" + Uri.EscapeDataString(Username), StringComparison.Ordinal) == true,
        "Probe omitted username form field.");
    Check(fixture.Handler.LastBody?.Contains("password=" + Uri.EscapeDataString(Password), StringComparison.Ordinal) == true,
        "Probe omitted password form field.");
    Check(fixture.Resolver.Calls.Count == 2, "Probe must resolve exactly username and password.");
    Check(fixture.Resolver.Calls.All(call =>
            call.ServiceId == fixture.ServiceId
            && call.EnvironmentId == fixture.EnvironmentId
            && call.AuthProfileId == fixture.ProfileId),
        "Probe resolved secret outside exact Service + Environment + AuthProfile scope.");
}

async Task UnauthorizedProbeFailsClosedWithoutLeakAsync()
{
    var fixture = CreateFixture(HttpStatusCode.Unauthorized, string.Empty, exactBinding: true);
    var exception = await CaptureRejectedAsync(() =>
        fixture.Probe.TestTokenGenerationAsync(fixture.ServiceId, fixture.EnvironmentId, fixture.ProfileId));

    Check(exception.Message == AuthenticationProbeRejectedException.SafeMessage, "401 probe exposed unexpected diagnostic detail.");
    Check(!exception.ToString().Contains(Username, StringComparison.Ordinal)
          && !exception.ToString().Contains(Password, StringComparison.Ordinal),
        "401 probe disclosed credential material.");
    Check(fixture.Handler.CallCount == 1, "401 probe must not retry authentication.");
}

async Task ExpiredJwtProbeFailsClosedAsync()
{
    var fixture = CreateFixture(HttpStatusCode.OK, JwtWithExp(1), exactBinding: true);
    await CaptureRejectedAsync(() =>
        fixture.Probe.TestTokenGenerationAsync(fixture.ServiceId, fixture.EnvironmentId, fixture.ProfileId));

    Check(fixture.Handler.CallCount == 1, "Expired token probe must make exactly one acquisition attempt.");
}

async Task ForgedBindingFailsBeforeSecretsOrTransportAsync()
{
    var fixture = CreateFixture(HttpStatusCode.OK, FutureJwt(), exactBinding: false);
    await CaptureRejectedAsync(() =>
        fixture.Probe.TestTokenGenerationAsync(fixture.ServiceId, fixture.EnvironmentId, fixture.ProfileId));

    Check(fixture.Resolver.Calls.Count == 0, "Forged binding reached secret resolution.");
    Check(fixture.Handler.CallCount == 0, "Forged binding reached token transport.");
}

static async Task<AuthenticationProbeRejectedException> CaptureRejectedAsync(Func<Task> operation)
{
    try
    {
        await operation();
    }
    catch (AuthenticationProbeRejectedException exception)
    {
        return exception;
    }

    throw new InvalidOperationException("Expected authentication probe rejection.");
}

static ProbeFixture CreateFixture(HttpStatusCode statusCode, string token, bool exactBinding)
{
    var serviceId = Guid.Parse("88000000-0000-0000-0000-000000000801");
    var environmentId = Guid.Parse("88000000-0000-0000-0000-000000000802");
    var profileId = Guid.Parse("88000000-0000-0000-0000-000000000803");
    var configuredProfileId = exactBinding ? profileId : Guid.Parse("88000000-0000-0000-0000-000000000899");

    var service = new CatalogService
    {
        Id = serviceId,
        DefinitionKey = Guid.Parse("88000000-0000-0000-0000-000000000804"),
        EntityId = Guid.Parse("88000000-0000-0000-0000-000000000805"),
        Code = "SYNTH-P08-ADMIN",
        NameAr = "اختبار مصادقة",
        NameEn = "Authentication probe",
        Active = true,
        IsCurrent = true,
        Version = 1,
        EnvironmentConfigs =
        [
            new ServiceEnvironmentConfig
            {
                Id = Guid.Parse("88000000-0000-0000-0000-000000000806"),
                ServiceId = serviceId,
                EnvironmentId = environmentId,
                BaseUrl = "https://synthetic.invalid/moj/",
                RelativePath = "/service",
                HttpMethod = "GET",
                ContentType = "application/json",
                NonSecretHeadersJson = $"{{\"{TokenPathKey}\":\"/genToken\"}}",
                TimeoutSeconds = 5,
                TlsPolicy = "SystemDefault",
                ValidateServerCertificate = true,
                Active = true,
                AuthProfileId = configuredProfileId
            }
        ]
    };

    var environment = new CatalogEnvironment
    {
        Id = environmentId,
        Code = "UAT",
        NameAr = "اختبار",
        NameEn = "UAT",
        Active = true,
        DisplayOrder = 1
    };

    var profile = new AuthProfileDescriptor(
        profileId,
        serviceId,
        environmentId,
        "Synthetic P08 token",
        AuthProfileType.TokenEndpoint,
        true,
        3,
        "synthetic",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        [
            new AuthProfileSecretDescriptor("username", SecretRef.Parse("sr1_" + new string('U', 43)), 1),
            new AuthProfileSecretDescriptor("password", SecretRef.Parse("sr1_" + new string('P', 43)), 2)
        ],
        [new AuthProfileBindingDescriptor(serviceId, environmentId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);

    var metadata = new FakeMetadata(new MetadataCatalogSnapshot([], [service], [environment]));
    var profiles = new FakeAuthProfiles(profile);
    var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = Username,
        ["password"] = Password
    });
    var handler = new RecordingHandler(statusCode, token);
    var clientFactory = new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false));
    var probe = new MojAuthenticationProbeService(metadata, profiles, resolver, clientFactory);
    return new ProbeFixture(probe, resolver, handler, serviceId, environmentId, profileId);
}

static string FutureJwt() => JwtWithExp(4_102_444_800);

static string JwtWithExp(long exp)
{
    static string Base64Url(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
    return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url($"{{\"exp\":{exp}}}")}.synthetic";
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record ProbeFixture(
    MojAuthenticationProbeService Probe,
    FakeSecretResolver Resolver,
    RecordingHandler Handler,
    Guid ServiceId,
    Guid EnvironmentId,
    Guid ProfileId);

sealed record SecretCall(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string SecretName);

sealed class RecordingHandler(HttpStatusCode statusCode, string token) : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public string? LastPath { get; private set; }
    public HttpMethod? LastMethod { get; private set; }
    public string? LastContentType { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastPath = request.RequestUri?.AbsolutePath;
        LastMethod = request.Method;
        LastContentType = request.Content?.Headers.ContentType?.MediaType;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(statusCode == HttpStatusCode.OK ? $"{{\"data\":\"{token}\"}}" : "{}", Encoding.UTF8, "application/json")
        };
    }
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (!string.Equals(name, "GSIP.Execution", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected HttpClient name.");
        return client;
    }
}

sealed class FakeSecretResolver(IReadOnlyDictionary<string, string> values) : ISecretMaterialResolver
{
    public List<SecretCall> Calls { get; } = [];

    public async Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new SecretCall(serviceId, environmentId, authProfileId, secretName));
        if (!values.TryGetValue(secretName, out var value))
            throw new SecretReferenceRejectedException();
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
        authProfileId == profile.Id
            ? Task.FromResult(profile)
            : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());

    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthProfileDescriptor?>(profile);
    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
