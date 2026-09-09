using System.Net;
using System.Security.Claims;
using System.Text;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

const string secretSentinel = "SYNTHETIC_RUNTIME_SECRET_7f9c_DO_NOT_LOG";
var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "70000000-0000-0000-0000-000000000701")],
    "SyntheticRuntime"));

await SuccessfulGetAndMappingAsync();
await ValidationStopsTransportAsync();
await PostDoesNotRetryByDefaultAsync();
await PostRetriesOnlyWithExplicitSafeMetadataAsync();
await StatusAndTelemetryAreSafeAsync();
await UnsupportedTransportFailsClosedAsync();
await StaticBearerIsTransientAndNotLoggedAsync();
await CallerCancellationPropagatesAsync();

Console.WriteLine("P07 generic execution runtime checks passed.");
return;

async Task SuccessfulGetAndMappingAsync()
{
    var service = SyntheticService(
        [Field("civilId", required: true, regex: "^[0-9]{12}$")],
        [Mapping("$.data.name")]);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"data\":{\"name\":\"Synthetic Person\"}}")));
    var fixture = Fixture(service, Binding("GET", "{\"X-Synthetic-Mode\":\"runtime\"}"), handler);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = "123456789012" }));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "Expected successful GET execution.");
    Check(result.Attempts == 1 && handler.CallCount == 1, "Successful GET must use one transport attempt.");
    Check(result.RequestId.StartsWith("GSIP-", StringComparison.Ordinal), "RequestId is required.");
    Check(Guid.TryParse(result.CorrelationId, out _), "CorrelationId must be a GUID.");
    Check(result.EndpointAlias == "SYNTH-P07:UAT", "Only safe endpoint alias may be returned.");
    Check(!result.EndpointAlias.Contains("synthetic.invalid", StringComparison.OrdinalIgnoreCase), "BaseUrl leaked through endpoint alias.");
    Check(result.StructuredResult.Count == 1 && result.StructuredResult[0].Value == "Synthetic Person",
        "ResultMappings must produce structured output.");
    var sent = handler.Requests.Single();
    Check(sent.Uri.Query.Contains("civilId=123456789012", StringComparison.Ordinal), "GET inputs must become query parameters.");
    Check(sent.Headers.TryGetValue("X-Request-ID", out var requestId) && requestId == result.RequestId,
        "Outbound request must carry RequestId.");
    Check(sent.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && correlationId == result.CorrelationId,
        "Outbound request must carry CorrelationId.");
    Check(sent.Headers.TryGetValue("X-Synthetic-Mode", out var mode) && mode == "runtime",
        "Non-secret configured header was not applied.");
    Check(fixture.Metadata.MarkUsedCalls == 1, "Successful execution must mark service metadata used.");
}

async Task ValidationStopsTransportAsync()
{
    var service = SyntheticService([Field("civilId", true, "^[0-9]{12}$")], []);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding("POST"), handler);

    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, service.Id, CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = "bad", ["forged"] = "x" }));
        throw new InvalidOperationException("Expected validation rejection.");
    }
    catch (ServiceExecutionValidationException ex)
    {
        Check(ex.Message == ServiceExecutionValidationException.SafeMessage, "Validation message must be safe and constant.");
        Check(ex.Errors.ContainsKey("civilId") && ex.Errors.ContainsKey("forged"), "Invalid and unknown fields must fail server-side.");
    }

    Check(handler.CallCount == 0, "Invalid input reached outbound transport.");
    Check(fixture.Metadata.MarkUsedCalls == 0, "Invalid request must not mark service used.");
}

async Task PostDoesNotRetryByDefaultAsync()
{
    var service = SyntheticService([Field("value", true)], []);
    var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(
        attempt == 1 ? Json(HttpStatusCode.ServiceUnavailable, "{\"error\":\"synthetic\"}") : Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding("POST"), handler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 });

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["value"] = "alpha" }));

    Check(result.Outcome == ServiceExecutionOutcome.ServerError, "POST 503 must map to ServerError.");
    Check(result.Attempts == 1 && handler.CallCount == 1, "POST retried without explicit SafeToRetry metadata.");
}

async Task PostRetriesOnlyWithExplicitSafeMetadataAsync()
{
    var service = SyntheticService([Field("value", true)], []);
    var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(
        attempt == 1 ? Json(HttpStatusCode.ServiceUnavailable, "{\"error\":\"retry\"}") : Json(HttpStatusCode.OK, "{\"ok\":true}")));
    var fixture = Fixture(service,
        Binding("POST", "{\"X-GSIP-SafeToRetry\":\"true\",\"X-Synthetic\":\"yes\"}"), handler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 });

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["value"] = "alpha" }));

    Check(result.Outcome == ServiceExecutionOutcome.Success && result.Attempts == 2 && handler.CallCount == 2,
        "Explicit SafeToRetry metadata did not enable bounded POST retry.");
    Check(handler.Requests.All(x => !x.Headers.ContainsKey(ServiceExecutionRuntimeOptions.SafeToRetryMetadataHeader)),
        "SafeToRetry policy marker leaked into outbound headers.");
    Check(handler.Requests.All(x => x.Headers.TryGetValue("X-Synthetic", out var value) && value == "yes"),
        "Normal non-secret headers must remain outbound.");
}

async Task StatusAndTelemetryAreSafeAsync()
{
    var service = SyntheticService([], []);
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, "{\"error\":\"no\"}")));
    var fixture = Fixture(service, Binding("GET"), handler, logger: logger);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));

    Check(result.Outcome == ServiceExecutionOutcome.Unauthorized && result.StatusCode == 401 && result.Attempts == 1,
        "HTTP 401 mapping/retry behavior is incorrect.");
    Check(logger.Messages.Any(x => x.Contains(result.RequestId, StringComparison.Ordinal)), "Safe telemetry must include RequestId.");
    Check(logger.Messages.All(x => !x.Contains("https://synthetic.invalid", StringComparison.OrdinalIgnoreCase)),
        "Telemetry leaked BaseUrl.");
}

async Task UnsupportedTransportFailsClosedAsync()
{
    var service = SyntheticService([], []);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding("GET") with { ValidateServerCertificate = false }, handler);

    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));
        throw new InvalidOperationException("Expected fail-closed transport rejection.");
    }
    catch (ServiceExecutionRejectedException ex)
    {
        Check(ex.Message == ServiceExecutionRejectedException.SafeMessage, "Transport rejection must use safe message.");
    }
    Check(handler.CallCount == 0, "Unsupported TLS policy reached transport.");
}

async Task StaticBearerIsTransientAndNotLoggedAsync()
{
    var service = SyntheticService([], []);
    var profileId = Guid.Parse("70000000-0000-0000-0000-000000000711");
    var secretRef = SecretRef.Parse("sr1_" + new string('A', 43));
    var profile = new AuthProfileDescriptor(
        profileId, service.Id, CatalogEnvironmentCodes.UatId, "Synthetic bearer", AuthProfileType.StaticBearer,
        true, 9, "synthetic", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new AuthProfileSecretDescriptor("bearer", secretRef, 1)],
        [new AuthProfileBindingDescriptor(service.Id, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var handler = new RecordingHandler((_, request, _) =>
    {
        Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == secretSentinel,
            "Resolved bearer secret was not applied transiently during send.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
    });
    var resolver = new FakeSecretResolver(Encoding.UTF8.GetBytes(secretSentinel));
    var fixture = Fixture(service,
        Binding("GET") with { AuthProfileId = profileId, AuthProfileVersion = profile.Version }, handler,
        profile: profile, secretResolver: resolver, logger: logger);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));

    Check(result.Outcome == ServiceExecutionOutcome.Success && resolver.UseCalls == 1,
        "Static bearer execution did not use secret resolver exactly once.");
    Check(logger.Messages.All(x => !x.Contains(secretSentinel, StringComparison.Ordinal)), "Plaintext secret leaked to logs.");
    Check(!result.ToString().Contains(secretSentinel, StringComparison.Ordinal), "Plaintext secret leaked to result diagnostics.");
}

async Task CallerCancellationPropagatesAsync()
{
    var service = SyntheticService([], []);
    var handler = new RecordingHandler(async (_, _, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    });
    var fixture = Fixture(service, Binding("GET") with { TimeoutSeconds = 30 }, handler);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()), cts.Token);
        throw new InvalidOperationException("Expected caller cancellation.");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
    }
}

static RuntimeFixture Fixture(
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
        new FakeSecurityGate(binding), metadata, new FakeAuthProfiles(profile), secretResolver ?? new FakeSecretResolver([]),
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(options ?? new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 }),
        logger ?? new RecordingLogger<GenericServiceExecutionEngine>());
    return new RuntimeFixture(engine, metadata);
}

static CatalogService SyntheticService(IReadOnlyList<ServiceFieldDefinition> fields, IReadOnlyList<ResultMappingDefinition> mappings) => new()
{
    Id = Guid.Parse("70000000-0000-0000-0000-000000000702"),
    DefinitionKey = Guid.Parse("70000000-0000-0000-0000-000000000703"),
    EntityId = Guid.Parse("70000000-0000-0000-0000-000000000704"),
    Code = "SYNTH-P07", NameAr = "خدمة اصطناعية", NameEn = "Synthetic P07",
    DescriptionAr = "اختبار فقط", DescriptionEn = "Synthetic test only", Active = true, IsCurrent = true, Version = 1,
    Fields = fields.ToList(), ResultMappings = mappings.ToList()
};

static ServiceFieldDefinition Field(string key, bool required, string regex = "") => new()
{
    Id = Guid.NewGuid(), ServiceId = Guid.Parse("70000000-0000-0000-0000-000000000702"), Key = key,
    LabelAr = key, LabelEn = key, FieldType = "text", Required = required, Regex = regex,
    OptionsJson = "[]", DisplayOrder = 1, Sensitive = false, Masking = "None"
};

static ResultMappingDefinition Mapping(string path) => new()
{
    Id = Guid.NewGuid(), ServiceId = Guid.Parse("70000000-0000-0000-0000-000000000702"), SourcePath = path,
    LabelAr = "الاسم", LabelEn = "Name", ResultType = "text", Formatter = string.Empty, Sensitive = false, DisplayOrder = 1
};

static AuthorizedServiceExecutionBinding Binding(string method, string nonSecretHeadersJson = "{}") => new(
    Guid.Parse("70000000-0000-0000-0000-000000000702"), "SYNTH-P07",
    CatalogEnvironmentCodes.UatId, CatalogEnvironmentCodes.Uat,
    "https://synthetic.invalid", "/runtime", method, "application/json", 5,
    "SystemDefault", true, string.Empty, null, null, nonSecretHeadersJson);

static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record RuntimeFixture(GenericServiceExecutionEngine Engine, FakeMetadata Metadata);
sealed record RequestSnapshot(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body);

sealed class RecordingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public List<RequestSnapshot> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in request.Headers) headers[pair.Key] = string.Join(",", pair.Value);
        if (request.Content is not null)
            foreach (var pair in request.Content.Headers) headers[pair.Key] = string.Join(",", pair.Value);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RequestSnapshot(request.Method, request.RequestUri!, headers, body));
        return await responder(CallCount, request, cancellationToken);
    }
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        AssertEx.That(name == "GSIP.Execution", "Runtime must use the canonical named HttpClient.");
        return client;
    }
}

sealed class FakeSecurityGate(AuthorizedServiceExecutionBinding binding) : IServiceExecutionSecurityGate
{
    public Task<AuthorizedServiceExecutionBinding> AuthorizeAsync(ClaimsPrincipal principal, Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default)
    {
        AssertEx.That(serviceId == binding.ServiceId && environmentId == binding.EnvironmentId,
            "Runtime must authorize the exact requested Service + Environment.");
        return Task.FromResult(binding);
    }
}

sealed class FakeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
{
    public int MarkUsedCalls { get; private set; }
    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default) { MarkUsedCalls++; return Task.CompletedTask; }
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
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeSecretResolver(byte[] material) : ISecretMaterialResolver
{
    public int UseCalls { get; private set; }
    public async Task<TResult> UseSecretAsync<TResult>(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName,
        SecretRef secretRef, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation, CancellationToken cancellationToken = default)
    {
        UseCalls++;
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

static class AssertEx
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
