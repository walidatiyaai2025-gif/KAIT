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

const string SecretSentinel = "SYNTHETIC_RUNTIME_SECRET_7f9c_DO_NOT_LOG";
var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "70000000-0000-0000-0000-000000000701")],
    "SyntheticRuntime"));

await VerifySuccessfulGetAndStructuredMappingAsync();
await VerifyValidationFailsBeforeTransportAsync();
await VerifyPostDoesNotRetryByDefaultAsync();
await VerifyPostRetriesOnlyWithExplicitMetadataAsync();
await VerifyStatusMappingAndSafeTelemetryAsync();
await VerifyUnsupportedTransportFailsClosedAsync();
await VerifyStaticBearerIsTransientAndNotLoggedAsync();
await VerifyCallerCancellationPropagatesAsync();

Console.WriteLine("P07 generic execution runtime checks passed.");
return;

async Task VerifySuccessfulGetAndStructuredMappingAsync()
{
    var service = SyntheticService(
        [Field("civilId", required: true, regex: "^[0-9]{12}$")],
        [Mapping("$.data.name", sensitive: false)]);
    var binding = Binding("GET", nonSecretHeadersJson: "{\"X-Synthetic-Mode\":\"runtime\"}");
    var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"data\":{\"name\":\"Synthetic Person\"}}")));
    var fixture = Fixture(service, binding, handler);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal,
        service.Id,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = "123456789012" }));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "Expected successful GET execution.");
    Check(result.Attempts == 1 && handler.CallCount == 1, "Successful GET must use one transport attempt.");
    Check(result.RequestId.StartsWith("GSIP-", StringComparison.Ordinal) && result.RequestId.Length > 12,
        "Execution must emit a RequestId.");
    Check(Guid.TryParse(result.CorrelationId, out _), "Execution must emit a valid CorrelationId.");
    Check(result.EndpointAlias == "SYNTH-P07:UAT", "Result must expose only the safe endpoint alias.");
    Check(!result.EndpointAlias.Contains("synthetic.invalid", StringComparison.OrdinalIgnoreCase),
        "Endpoint alias must not disclose BaseUrl.");
    Check(result.StructuredResult.Count == 1 && result.StructuredResult[0].Value == "Synthetic Person",
        "ResultMappings must produce a structured result.");
    var request = handler.Requests.Single();
    Check(request.Uri.Query.Contains("civilId=123456789012", StringComparison.Ordinal),
        "GET fields must be encoded into the query string.");
    Check(request.Headers.TryGetValue("X-Request-ID", out var requestHeader) && requestHeader == result.RequestId,
        "Outbound request must carry the generated RequestId.");
    Check(request.Headers.TryGetValue("X-Correlation-ID", out var correlationHeader) && correlationHeader == result.CorrelationId,
        "Outbound request must carry the generated CorrelationId.");
    Check(request.Headers.TryGetValue("X-Synthetic-Mode", out var mode) && mode == "runtime",
        "Configured non-secret headers must be applied.");
    Check(fixture.Metadata.MarkUsedCalls == 1, "Execution must mark the service definition used.");
}

async Task VerifyValidationFailsBeforeTransportAsync()
{
    var service = SyntheticService([Field("civilId", required: true, regex: "^[0-9]{12}$")], []);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding("POST"), handler);

    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            service.Id,
            CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?>
            {
                ["civilId"] = "bad-value",
                ["forgedField"] = "must-fail"
            }));
        throw new InvalidOperationException("Expected ServiceExecutionValidationException.");
    }
    catch (ServiceExecutionValidationException exception)
    {
        Check(exception.Message == ServiceExecutionValidationException.SafeMessage,
            "Validation failure must use the constant safe message.");
        Check(exception.Errors.ContainsKey("civilId") && exception.Errors.ContainsKey("forgedField"),
            "Validation must reject invalid and unknown fields server-side.");
    }

    Check(handler.CallCount == 0, "Invalid input must fail before outbound HTTP.");
    Check(fixture.Metadata.MarkUsedCalls == 0, "Invalid input must not mark the service as used.");
}

async Task VerifyPostDoesNotRetryByDefaultAsync()
{
    var service = SyntheticService([Field("value", required: true)], []);
    var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(
        attempt == 1 ? Json(HttpStatusCode.ServiceUnavailable, "{\"error\":\"synthetic\"}") : Json(HttpStatusCode.OK, "{}")));
    var fixture = Fixture(service, Binding("POST", nonSecretHeadersJson: "{}"), handler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 });

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal,
        service.Id,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["value"] = "alpha" }));

    Check(result.Outcome == ServiceExecutionOutcome.ServerError, "POST 503 must surface as ServerError.");
    Check(result.Attempts == 1 && handler.CallCount == 1,
        "POST must never retry automatically without explicit SafeToRetry metadata.");
}

async Task VerifyPostRetriesOnlyWithExplicitMetadataAsync()
{
    var service = SyntheticService([Field("value", required: true)], []);
    var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(
        attempt == 1 ? Json(HttpStatusCode.ServiceUnavailable, "{\"error\":\"retry\"}") : Json(HttpStatusCode.OK, "{\"ok\":true}")));
    var fixture = Fixture(
        service,
        Binding("POST", nonSecretHeadersJson: "{\"X-GSIP-SafeToRetry\":\"true\",\"X-Synthetic\":\"yes\"}"),
        handler,
        new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 });

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal,
        service.Id,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["value"] = "alpha" }));

    Check(result.Outcome == ServiceExecutionOutcome.Success && result.Attempts == 2 && handler.CallCount == 2,
        "Explicit SafeToRetry metadata must allow bounded retry for POST.");
    Check(handler.Requests.All(request => !request.Headers.ContainsKey(ServiceExecutionRuntimeOptions.SafeToRetryMetadataHeader)),
        "SafeToRetry metadata is policy and must never be emitted as an HTTP header.");
    Check(handler.Requests.All(request => request.Headers.TryGetValue("X-Synthetic", out var value) && value == "yes"),
        "Normal non-secret headers must remain outbound while policy metadata is stripped.");
}

async Task VerifyStatusMappingAndSafeTelemetryAsync()
{
    var service = SyntheticService([], []);
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, "{\"error\":\"no\"}")));
    var fixture = Fixture(service, Binding("GET"), handler, logger: logger);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));

    Check(result.Outcome == ServiceExecutionOutcome.Unauthorized && result.StatusCode == 401,
        "HTTP 401 must map to Unauthorized without retry.");
    Check(result.Attempts == 1, "Non-transient 401 must not retry.");
    Check(logger.Messages.Any(message => message.Contains(result.RequestId, StringComparison.Ordinal)),
        "Safe telemetry must include RequestId.");
    Check(logger.Messages.All(message => !message.Contains("https://synthetic.invalid", StringComparison.OrdinalIgnoreCase)),
        "Telemetry must not disclose BaseUrl.");
}

async Task VerifyUnsupportedTransportFailsClosedAsync()
{
    var service = SyntheticService([], []);
    var handler = new RecordingHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
    var unsafeBinding = Binding("GET") with { ValidateServerCertificate = false };
    var fixture = Fixture(service, unsafeBinding, handler);

    await ExpectRejectedAsync(
        () => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>())),
        "certificate validation bypass must fail closed");
    Check(handler.CallCount == 0, "Unsupported transport policy must fail before outbound HTTP.");
}

async Task VerifyStaticBearerIsTransientAndNotLoggedAsync()
{
    var service = SyntheticService([], []);
    var profileId = Guid.Parse("70000000-0000-0000-0000-000000000711");
    var secretRef = SecretRef.Parse("sr1_" + new string('A', 43));
    var profile = new AuthProfileDescriptor(
        profileId,
        service.Id,
        CatalogEnvironmentCodes.UatId,
        "Synthetic bearer",
        AuthProfileType.StaticBearer,
        true,
        9,
        "synthetic",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        [new AuthProfileSecretDescriptor("bearer", secretRef, 1)],
        [new AuthProfileBindingDescriptor(service.Id, CatalogEnvironmentCodes.UatId, false, "synthetic", "owner", DateTimeOffset.UnixEpoch)]);
    var logger = new RecordingLogger<GenericServiceExecutionEngine>();
    var handler = new RecordingHandler((_, request, _) =>
    {
        Check(request.Headers.Authorization?.Scheme == "Bearer"
              && request.Headers.Authorization.Parameter == SecretSentinel,
            "Resolved bearer secret must exist only on the outbound request during send.");
        return Task.FromResult(Json(HttpStatusCode.OK, "{\"ok\":true}"));
    });
    var secretResolver = new FakeSecretResolver(Encoding.UTF8.GetBytes(SecretSentinel));
    var fixture = Fixture(
        service,
        Binding("GET") with { AuthProfileId = profileId, AuthProfileVersion = profile.Version },
        handler,
        profile: profile,
        secretResolver: secretResolver,
        logger: logger);

    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));

    Check(result.Outcome == ServiceExecutionOutcome.Success, "Static bearer synthetic execution should succeed.");
    Check(secretResolver.UseCalls == 1, "Runtime must resolve secret material through ISecretMaterialResolver.");
    Check(logger.Messages.All(message => !message.Contains(SecretSentinel, StringComparison.Ordinal)),
        "Runtime logs must never contain plaintext secret material.");
    Check(!result.ToString().Contains(SecretSentinel, StringComparison.Ordinal),
        "Execution result diagnostics must not contain plaintext secret material.");
}

async Task VerifyCallerCancellationPropagatesAsync()
{
    var service = SyntheticService([], []);
    var handler = new RecordingHandler(async (_, _, token) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30), token);
        return Json(HttpStatusCode.OK, "{}");
    });
    var fixture = Fixture(service, Binding("GET") with { TimeoutSeconds = 30 }, handler);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()), cancellation.Token);
        throw new InvalidOperationException("Expected caller cancellation to propagate.");
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
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
        [],
        [service],
        [new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true }]));
    var engine = new GenericServiceExecutionEngine(
        new FakeSecurityGate(binding),
        metadata,
        new FakeAuthProfiles(profile),
        secretResolver ?? new FakeSecretResolver([]),
        new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
        Options.Create(options ?? new ServiceExecutionRuntimeOptions { MaxAttempts = 2, RetryDelayMilliseconds = 0 }),
        logger ?? new RecordingLogger<GenericServiceExecutionEngine>());
    return new RuntimeFixture(engine, metadata);
}

static CatalogService SyntheticService(
    IReadOnlyList<ServiceFieldDefinition> fields,
    IReadOnlyList<ResultMappingDefinition> mappings) => new()
{
    Id = Guid.Parse("70000000-0000-0000-0000-000000000702"),
    DefinitionKey = Guid.Parse("70000000-0000-0000-0000-000000000703"),
    EntityId = Guid.Parse("70000000-0000-0000-0000-000000000704"),
    Code = "SYNTH-P07",
    NameAr = "خدمة اصطناعية",
    NameEn = "Synthetic P07",
    DescriptionAr = "اختبار فقط",
    DescriptionEn = "Synthetic test only",
    Active = true,
    IsCurrent = true,
    Version = 1,
    Fields = fields.ToList(),
    ResultMappings = mappings.ToList()
};

static ServiceFieldDefinition Field(string key, bool required, string regex = "") => new()
{
    Id = Guid.NewGuid(),
    ServiceId = Guid.Parse("70000000-0000-0000-0000-000000000702"),
    Key = key,
    LabelAr = key,
    LabelEn = key,
    FieldType = "text",
    Required = required,
    Regex = regex,
    OptionsJson = "[]",
    DisplayOrder = 1,
    Sensitive = false,
    Masking = "None"
};

static ResultMappingDefinition Mapping(string path, bool sensitive) => new()
{
    Id = Guid.NewGuid(),
    ServiceId = Guid.Parse("70000000-0000-0000-0000-000000000702"),
    SourcePath = path,
    LabelAr = "الاسم",
    LabelEn = "Name",
    ResultType = "text",
    Formatter = string.Empty,
    Sensitive = sensitive,
    DisplayOrder = 1
};

static AuthorizedServiceExecutionBinding Binding(string method, string nonSecretHeadersJson = "{}") => new(
    Guid.Parse("70000000-0000-0000-0000-000000000702"),
    "SYNTH-P07",
    CatalogEnvironmentCodes.UatId,
    CatalogEnvironmentCodes.Uat,
    "https://synthetic.invalid",
    "/runtime",
    method,
    "application/json",
    5,
    "SystemDefault",
    true,
    string.Empty,
    null,
    null,
    nonSecretHeadersJson);

static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static async Task ExpectRejectedAsync(Func<Task<ServiceExecutionResult>> action, string scenario)
{
    try
    {
        _ = await action();
        throw new InvalidOperationException($"Expected rejection: {scenario}");
    }
    catch (ServiceExecutionRejectedException exception)
    {
        Check(exception.Message == ServiceExecutionRejectedException.SafeMessage,
            $"Rejection for '{scenario}' must use the constant safe message.");
    }
}

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
        var headers = request.Headers.Concat(request.Content?.Headers ?? [])
            .ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RequestSnapshot(request.Method, request.RequestUri!, headers, body));
        return await responder(CallCount, request, cancellationToken);
    }
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        Check(name == "GSIP.Execution", "Runtime must use the canonical named HttpClient.");
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
            "Runtime must authorize the exact requested Service + Environment.");
        return Task.FromResult(binding);
    }
}

sealed class FakeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
{
    public int MarkUsedCalls { get; private set; }
    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        MarkUsedCalls++;
        return Task.CompletedTask;
    }
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
        profile is not null && profile.Id == authProfileId
            ? Task.FromResult(profile)
            : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
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
    public async Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
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
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
