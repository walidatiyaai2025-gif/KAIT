using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Authentication;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

const string SyntheticCivilValue = "SYNTHETIC_MARRIAGE_REQUEST";
const string SyntheticApiKey = "SYNTHETIC_P09_API_KEY";
const string SyntheticUsername = "SYNTHETIC_P09_USER";
const string SyntheticPassword = "SYNTHETIC_P09_PASSWORD";

var connection = Environment.GetEnvironmentVariable("GSIP_P09_MARRIAGE_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_MARRIAGE_SQL is required.");

var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "99000000-0000-0000-0000-000000000901")],
    "SyntheticP09Marriage"));

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    var seed = new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T08:46:00Z")));
    await seed.SeedAsync();

    var marriage = await db.CatalogServices.AsNoTracking()
        .Include(service => service.EnvironmentConfigs)
        .Include(service => service.Fields)
        .Include(service => service.ResultMappings)
        .SingleAsync(service => service.Code == "MARRIAGECASES" && service.IsCurrent);

    ValidateOfficialSnapshotAndSeed(marriage);
    ValidateGenericClientContract();
    await ExactUriFormAndCompositeAuthenticationAsync(marriage);
    await RequiredFieldStopsBeforeSecretsOrTransportAsync(marriage);
    await Target401DoesNotCauseTokenStormAsync(marriage);
    await CrossServiceAndCrossEnvironmentAuthAreRejectedAsync(marriage);
    await WriteSafeEvidenceAsync();

    Console.WriteLine("P09_MARRIAGE_CASES_ACCEPTANCE=PASS_AUTONOMOUS_SCOPE");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

void ValidateOfficialSnapshotAndSeed(CatalogService marriage)
{
    var contractPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "moj-api-reference", "p09", "api-129-marriage-cases.contract.json");
    using var contract = JsonDocument.Parse(File.ReadAllText(contractPath));
    var root = contract.RootElement;
    AssertEx.That(root.GetProperty("apiId").GetInt32() == 129, "Marriage Cases acceptance is not anchored to official API 129 evidence.");
    AssertEx.That(root.GetProperty("captureStatus").GetString() == "PARTIAL_PROVEN", "API 129 capture status changed without acceptance reconciliation.");

    var target = root.GetProperty("operations").EnumerateArray()
        .Single(operation => operation.GetProperty("name").GetString() == "Marriage Cases");
    AssertEx.That(target.GetProperty("method").GetString() == "POST", "Official Marriage Cases method is not POST.");
    AssertEx.That(target.GetProperty("relativePath").GetString() == MojMetadataSeedService.Api129TargetPath,
        "Official Marriage Cases relative path drifted from the integrated seed.");
    AssertEx.That(target.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded",
        "Official Marriage Cases request content type drifted.");
    var requestField = target.GetProperty("requestFields").EnumerateArray().Single();
    AssertEx.That(requestField.GetProperty("name").GetString() == "civilId" && requestField.GetProperty("required").GetBoolean(),
        "Official Marriage Cases request field casing/requiredness drifted.");
    AssertEx.That(requestField.GetProperty("typeStatus").GetString() == "DEFERRED_EXTERNAL"
        && requestField.GetProperty("validationStatus").GetString() == "DEFERRED_EXTERNAL",
        "Civil identifier type/validation must remain deferred until stronger official evidence exists.");
    var schemes = target.GetProperty("authentication").EnumerateArray()
        .Select(item => item.GetProperty("scheme").GetString())
        .ToHashSet(StringComparer.Ordinal);
    AssertEx.That(schemes.SetEquals(["ApiKeyHeader", "Bearer"]), "Marriage Cases composite authentication evidence drifted.");
    AssertEx.That(target.GetProperty("successResponses").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Target success schema must not be invented while official evidence is deferred.");
    AssertEx.That(target.GetProperty("documentedErrorResponses").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Target documented errors must not be invented while official evidence is deferred.");
    AssertEx.That(target.GetProperty("responseFields").GetProperty("status").GetString() == "DEFERRED_EXTERNAL"
        && target.GetProperty("resultMappingCandidates").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Target response/result mappings must remain deferred until official schema evidence exists.");

    var uat = marriage.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = marriage.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    AssertEx.That(uat.BaseUrl == MojMetadataSeedService.Api129UatBaseUrl
        && uat.RelativePath == MojMetadataSeedService.Api129TargetPath
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/x-www-form-urlencoded",
        "Seeded UAT Marriage Cases transport contract drifted from official evidence.");
    AssertEx.That(!uat.Active && uat.AuthProfileId.HasValue,
        "Seeded UAT must remain disabled until owner-secured credential configuration is completed.");
    AssertEx.That(production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && production.RelativePath.Length == 0
        && production.HttpMethod.Length == 0
        && production.ContentType.Length == 0
        && !production.Active
        && production.AuthProfileId is null,
        "Production must remain prefix-only, disabled and isolated; UAT fallback is forbidden.");

    var fields = marriage.Fields.ToArray();
    AssertEx.That(fields.Length == 1, "Marriage Cases must expose exactly one proven request field.");
    var field = fields[0];
    AssertEx.That(field.Key == "civilId" && field.Required && field.Sensitive && field.Masking == "Last4",
        "Marriage Cases request metadata lost exact field casing, requiredness or sensitive treatment.");
    AssertEx.That(field.FieldType == "deferred" && field.Regex.Length == 0 && field.Minimum is null && field.Maximum is null
        && field.MinLength is null && field.MaxLength is null,
        "Marriage Cases seed invented field type or validation beyond official evidence.");
    AssertEx.That(marriage.ResultMappings.Count == 0,
        "Marriage Cases must not contain business result mappings before official response schema evidence exists.");
}

void ValidateGenericClientContract()
{
    var viewPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "GSIP.Web", "Views", "Execution", "Index.cshtml");
    var view = File.ReadAllText(viewPath);
    AssertEx.That(view.Contains("required=\"@field.Required\"", StringComparison.Ordinal),
        "Generic execution UI no longer binds HTML required validation from metadata.");
    AssertEx.That(view.Contains("value=\"@(field.Sensitive ? null : submittedValue)\"", StringComparison.Ordinal),
        "Generic execution UI may redisplay sensitive submitted input.");
    AssertEx.That(view.Contains("name=\"Inputs[@field.Key]\"", StringComparison.Ordinal),
        "Generic execution UI no longer preserves exact API field keys from metadata.");
}

async Task ExactUriFormAndCompositeAuthenticationAsync(CatalogService marriage)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK);
    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal,
        marriage.Id,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = SyntheticCivilValue }));

    AssertEx.That(result.Outcome == ServiceExecutionOutcome.Success && result.StatusCode == 200 && result.Attempts == 1,
        "Synthetic transport success did not complete through the generic execution engine.");
    AssertEx.That(result.StructuredResult.Count == 0,
        "Synthetic target response produced a business mapping even though official response mappings are deferred.");
    AssertEx.That(fixture.Handler.TokenCalls == 1 && fixture.Handler.TargetCalls == 1,
        "Composite flow must acquire one token and send one target request.");
    AssertEx.That(fixture.Handler.TokenPath == "/WSWEB/WS/v1/marriage/genToken",
        "Token URI composition lost the official base path prefix.");
    AssertEx.That(fixture.Handler.Target is not null
        && fixture.Handler.Target.Uri.AbsoluteUri == "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage/marriageCasesAPIGEE",
        "Marriage Cases target URI composition lost or replaced the official base path prefix.");
    AssertEx.That(fixture.Handler.TokenMethod == HttpMethod.Post
        && fixture.Handler.TokenContentType == "application/x-www-form-urlencoded"
        && fixture.Handler.TokenHasExactApiKey,
        "Token request method/content type/x-api-key contract is incorrect.");
    AssertEx.That(fixture.Handler.TokenBody.Contains("username=" + Uri.EscapeDataString(SyntheticUsername), StringComparison.Ordinal)
        && fixture.Handler.TokenBody.Contains("password=" + Uri.EscapeDataString(SyntheticPassword), StringComparison.Ordinal),
        "Token request form fields were not serialized exactly.");
    AssertEx.That(fixture.Handler.Target!.Method == HttpMethod.Post
        && fixture.Handler.Target.ContentType == "application/x-www-form-urlencoded"
        && fixture.Handler.Target.Body == "civilId=" + Uri.EscapeDataString(SyntheticCivilValue),
        "Marriage Cases form serialization or exact civilId casing is incorrect.");
    AssertEx.That(fixture.Handler.Target.HasExactApiKey && fixture.Handler.Target.HasBearer,
        "Marriage Cases target request did not apply x-api-key plus Bearer composite authentication.");
    AssertEx.That(fixture.Resolver.Calls.Count == 3
        && fixture.Resolver.Calls.All(call => call.ServiceId == marriage.Id
            && call.EnvironmentId == CatalogEnvironmentCodes.UatId
            && call.AuthProfileId == fixture.ProfileId),
        "Composite secret resolution escaped exact Service + Environment + AuthProfile scope.");
    AssertEx.That(fixture.Logger.Messages.All(message => !message.Contains(SyntheticCivilValue, StringComparison.Ordinal)),
        "Sensitive request input leaked to execution logs.");
    AssertEx.That(!result.ToString().Contains(SyntheticCivilValue, StringComparison.Ordinal),
        "Sensitive request input leaked to execution result diagnostics.");
}

async Task RequiredFieldStopsBeforeSecretsOrTransportAsync(CatalogService marriage)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK);
    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            marriage.Id,
            CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?>()));
        throw new InvalidOperationException("Expected missing required Marriage Cases field to fail server-side validation.");
    }
    catch (ServiceExecutionValidationException exception)
    {
        AssertEx.That(exception.Message == ServiceExecutionValidationException.SafeMessage,
            "Required-field rejection must expose only the generic safe message.");
        AssertEx.That(exception.Errors.TryGetValue("civilId", out var error) && error == "Required",
            "Required civilId validation did not preserve the exact official field key.");
    }

    AssertEx.That(fixture.Resolver.Calls.Count == 0 && fixture.Handler.TokenCalls == 0 && fixture.Handler.TargetCalls == 0,
        "Invalid Marriage Cases input reached secret resolution or outbound transport.");
}

async Task Target401DoesNotCauseTokenStormAsync(CatalogService marriage)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.Unauthorized);
    var command = new ServiceExecutionCommand(
        principal,
        marriage.Id,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = SyntheticCivilValue });

    var first = await fixture.Engine.ExecuteAsync(command);
    var second = await fixture.Engine.ExecuteAsync(command);

    AssertEx.That(first.Outcome == ServiceExecutionOutcome.Unauthorized && first.Attempts == 1
        && second.Outcome == ServiceExecutionOutcome.Unauthorized && second.Attempts == 1,
        "Synthetic target 401 must remain a single-attempt target response.");
    AssertEx.That(fixture.Handler.TokenCalls == 1 && fixture.Handler.TargetCalls == 2,
        "Target 401 caused token reacquisition/retry storm instead of reusing the valid cached token.");
    AssertEx.That(fixture.Logger.Messages.All(message => !message.Contains(SyntheticCivilValue, StringComparison.Ordinal)),
        "Target 401 telemetry leaked sensitive request input.");
}

async Task CrossServiceAndCrossEnvironmentAuthAreRejectedAsync(CatalogService marriage)
{
    var otherServiceId = Guid.Parse("99000000-0000-0000-0000-000000000999");
    using (var crossService = RuntimeFixture.Create(
        marriage,
        HttpStatusCode.OK,
        profileOwnerServiceId: otherServiceId))
    {
        await ExpectRejectedAsync(() => crossService.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            marriage.Id,
            CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = SyntheticCivilValue })),
            "cross-service AuthProfile");
        AssertEx.That(crossService.Resolver.Calls.Count == 0 && crossService.Handler.TokenCalls == 0 && crossService.Handler.TargetCalls == 0,
            "Cross-service AuthProfile misuse reached secrets or transport.");
    }

    using (var crossEnvironment = RuntimeFixture.Create(
        marriage,
        HttpStatusCode.OK,
        profileOwnerEnvironmentId: CatalogEnvironmentCodes.ProductionId))
    {
        await ExpectRejectedAsync(() => crossEnvironment.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            marriage.Id,
            CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = SyntheticCivilValue })),
            "cross-environment AuthProfile");
        AssertEx.That(crossEnvironment.Resolver.Calls.Count == 0 && crossEnvironment.Handler.TokenCalls == 0 && crossEnvironment.Handler.TargetCalls == 0,
            "Cross-environment AuthProfile misuse reached secrets or transport.");
    }

    using (var noProductionFallback = RuntimeFixture.Create(marriage, HttpStatusCode.OK))
    {
        await ExpectRejectedAsync(() => noProductionFallback.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal,
            marriage.Id,
            CatalogEnvironmentCodes.ProductionId,
            new Dictionary<string, string?> { ["civilId"] = SyntheticCivilValue })),
            "Production-to-UAT fallback");
        AssertEx.That(noProductionFallback.Profiles.ResolveCalls == 0
            && noProductionFallback.Resolver.Calls.Count == 0
            && noProductionFallback.Handler.TokenCalls == 0
            && noProductionFallback.Handler.TargetCalls == 0,
            "Missing Production binding fell back to UAT metadata/auth/transport.");
    }
}

async Task WriteSafeEvidenceAsync()
{
    var directory = Path.Combine("artifacts", "p09-marriage-cases-evidence");
    Directory.CreateDirectory(directory);
    var evidence = new
    {
        phase = "P09",
        unit = "P09::marriage-cases-service",
        apiId = 129,
        environment = "UAT",
        exactBasePathPreserved = true,
        exactTargetPathPreserved = true,
        exactFormFieldCasing = true,
        clientRequiredValidationFromMetadata = true,
        serverRequiredValidationFromMetadata = true,
        compositeApiKeyAndBearer = true,
        exactAuthScope = true,
        crossServiceRejected = true,
        crossEnvironmentRejected = true,
        productionFallback = false,
        target401TokenStorm = false,
        sensitiveRequestValueLogged = false,
        liveUatCalledByCi = false,
        targetSuccessSchema = "DEFERRED_EXTERNAL",
        targetDocumentedErrors = "DEFERRED_EXTERNAL",
        targetResponseFields = "DEFERRED_EXTERNAL",
        targetResultMappings = "DEFERRED_EXTERNAL",
        targetResponseSensitiveMasking = "DEFERRED_EXTERNAL_PENDING_OFFICIAL_SCHEMA",
        civilIdTypeAndValidation = "DEFERRED_EXTERNAL",
        readOnlyNonDestructiveClassification = "DEFERRED_EXTERNAL"
    };
    var path = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    var persisted = await File.ReadAllTextAsync(path);
    AssertEx.That(!persisted.Contains(SyntheticCivilValue, StringComparison.Ordinal)
        && !persisted.Contains(SyntheticApiKey, StringComparison.Ordinal)
        && !persisted.Contains(SyntheticUsername, StringComparison.Ordinal)
        && !persisted.Contains(SyntheticPassword, StringComparison.Ordinal),
        "P09 Marriage Cases evidence contains synthetic secret/request material; evidence must remain value-free.");
}

static async Task ExpectRejectedAsync(Func<Task<ServiceExecutionResult>> operation, string scenario)
{
    try
    {
        _ = await operation();
        throw new InvalidOperationException($"Expected fail-closed rejection for {scenario}.");
    }
    catch (ServiceExecutionRejectedException exception)
    {
        AssertEx.That(exception.Message == ServiceExecutionRejectedException.SafeMessage,
            $"{scenario} rejection exposed non-generic details.");
    }
}

sealed class RuntimeFixture : IDisposable
{
    private RuntimeFixture(
        GenericServiceExecutionEngine engine,
        MarriageRecordingHandler handler,
        FakeSecretResolver resolver,
        RecordingLogger<GenericServiceExecutionEngine> logger,
        FakeAuthProfiles profiles,
        InMemoryTokenCache cache,
        Guid profileId)
    {
        Engine = engine;
        Handler = handler;
        Resolver = resolver;
        Logger = logger;
        Profiles = profiles;
        Cache = cache;
        ProfileId = profileId;
    }

    public GenericServiceExecutionEngine Engine { get; }
    public MarriageRecordingHandler Handler { get; }
    public FakeSecretResolver Resolver { get; }
    public RecordingLogger<GenericServiceExecutionEngine> Logger { get; }
    public FakeAuthProfiles Profiles { get; }
    public InMemoryTokenCache Cache { get; }
    public Guid ProfileId { get; }

    public static RuntimeFixture Create(
        CatalogService seeded,
        HttpStatusCode targetStatus,
        Guid? profileOwnerServiceId = null,
        Guid? profileOwnerEnvironmentId = null)
    {
        var profileId = Guid.Parse("99000000-0000-0000-0000-000000000971");
        var service = CloneActiveUatService(seeded, profileId);
        var ownerServiceId = profileOwnerServiceId ?? service.Id;
        var ownerEnvironmentId = profileOwnerEnvironmentId ?? CatalogEnvironmentCodes.UatId;
        var profile = new AuthProfileDescriptor(
            profileId,
            ownerServiceId,
            ownerEnvironmentId,
            "Synthetic P09 Marriage composite profile",
            AuthProfileType.TokenEndpoint,
            true,
            5,
            "synthetic-p09",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [
                new AuthProfileSecretDescriptor("x-api-key", SecretRef.Parse("sr1_" + new string('K', 43)), 1),
                new AuthProfileSecretDescriptor("username", SecretRef.Parse("sr1_" + new string('U', 43)), 1),
                new AuthProfileSecretDescriptor("password", SecretRef.Parse("sr1_" + new string('P', 43)), 1)
            ],
            [new AuthProfileBindingDescriptor(ownerServiceId, ownerEnvironmentId, false, "synthetic-p09", "synthetic exact owner scope", DateTimeOffset.UnixEpoch)]);

        var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
            [],
            [service],
            [
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, NameAr = "اختبار", NameEn = "UAT", Active = true },
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.ProductionId, Code = CatalogEnvironmentCodes.Production, NameAr = "إنتاج", NameEn = "Production", Active = true }
            ]));
        var profiles = new FakeAuthProfiles(profile);
        var gate = new ServiceExecutionSecurityGate(metadata, new AllowPermissionEvaluator(), profiles);
        var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-api-key"] = SyntheticApiKey,
            ["username"] = SyntheticUsername,
            ["password"] = SyntheticPassword
        });
        var handler = new MarriageRecordingHandler(targetStatus, SyntheticToken.FutureJwt(), SyntheticApiKey);
        var logger = new RecordingLogger<GenericServiceExecutionEngine>();
        var cache = new InMemoryTokenCache(new FixedClock(DateTimeOffset.Parse("2026-09-09T08:46:00Z")), new TokenCacheOptions());
        var engine = new GenericServiceExecutionEngine(
            gate,
            metadata,
            profiles,
            resolver,
            new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
            Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 3, RetryDelayMilliseconds = 0 }),
            logger,
            cache);
        return new RuntimeFixture(engine, handler, resolver, logger, profiles, cache, profileId);
    }

    public void Dispose() => Cache.Dispose();

    private static CatalogService CloneActiveUatService(CatalogService seeded, Guid profileId)
    {
        var source = seeded.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        return new CatalogService
        {
            Id = seeded.Id,
            DefinitionKey = seeded.DefinitionKey,
            EntityId = seeded.EntityId,
            Code = seeded.Code,
            NameAr = seeded.NameAr,
            NameEn = seeded.NameEn,
            DescriptionAr = seeded.DescriptionAr,
            DescriptionEn = seeded.DescriptionEn,
            Active = true,
            Version = seeded.Version,
            IsCurrent = true,
            Fields = seeded.Fields.Select(field => new ServiceFieldDefinition
            {
                Id = field.Id,
                ServiceId = seeded.Id,
                Key = field.Key,
                LabelAr = field.LabelAr,
                LabelEn = field.LabelEn,
                FieldType = field.FieldType,
                Required = field.Required,
                Regex = field.Regex,
                Minimum = field.Minimum,
                Maximum = field.Maximum,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                OptionsJson = field.OptionsJson,
                DisplayOrder = field.DisplayOrder,
                Sensitive = field.Sensitive,
                Masking = field.Masking
            }).ToList(),
            ResultMappings = [],
            EnvironmentConfigs =
            [
                new ServiceEnvironmentConfig
                {
                    Id = source.Id,
                    ServiceId = seeded.Id,
                    EnvironmentId = CatalogEnvironmentCodes.UatId,
                    BaseUrl = source.BaseUrl,
                    RelativePath = source.RelativePath,
                    HttpMethod = source.HttpMethod,
                    ContentType = source.ContentType,
                    NonSecretHeadersJson = source.NonSecretHeadersJson,
                    TimeoutSeconds = 10,
                    TlsPolicy = "SystemDefault",
                    ValidateServerCertificate = true,
                    ProxyUrl = string.Empty,
                    Active = true,
                    AuthProfileId = profileId
                }
            ]
        };
    }
}

sealed record TargetRequestSnapshot(
    HttpMethod Method,
    Uri Uri,
    string ContentType,
    string Body,
    bool HasExactApiKey,
    bool HasBearer);

sealed class MarriageRecordingHandler(HttpStatusCode targetStatus, string syntheticToken, string expectedApiKey) : HttpMessageHandler
{
    public int TokenCalls { get; private set; }
    public int TargetCalls { get; private set; }
    public string TokenPath { get; private set; } = string.Empty;
    public HttpMethod? TokenMethod { get; private set; }
    public string TokenContentType { get; private set; } = string.Empty;
    public string TokenBody { get; private set; } = string.Empty;
    public bool TokenHasExactApiKey { get; private set; }
    public TargetRequestSnapshot? Target { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.EndsWith("/genToken", StringComparison.Ordinal))
        {
            TokenCalls++;
            TokenPath = path;
            TokenMethod = request.Method;
            TokenContentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            TokenBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            TokenHasExactApiKey = request.Headers.TryGetValues("x-api-key", out var values)
                && values.SingleOrDefault() == expectedApiKey;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { data = syntheticToken }), Encoding.UTF8, "application/json")
            };
        }

        if (path.EndsWith("/marriageCasesAPIGEE", StringComparison.Ordinal))
        {
            TargetCalls++;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKeyMatches = request.Headers.TryGetValues("x-api-key", out var values)
                && values.SingleOrDefault() == expectedApiKey;
            Target = new TargetRequestSnapshot(
                request.Method,
                request.RequestUri!,
                request.Content?.Headers.ContentType?.MediaType ?? string.Empty,
                body,
                apiKeyMatches,
                request.Headers.Authorization?.Scheme == "Bearer" && !string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter));
            return new HttpResponseMessage(targetStatus)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        throw new InvalidOperationException("Synthetic Marriage Cases handler received an unexpected path.");
    }
}

sealed record SecretCall(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId, string SecretName);

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

sealed class FakeAuthProfiles(AuthProfileDescriptor profile) : IAuthProfileService
{
    public int ResolveCalls { get; private set; }

    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return Task.FromResult<AuthProfileDescriptor?>(profile);
    }

    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        authProfileId == profile.Id
            ? Task.FromResult(profile)
            : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());

    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

sealed class AllowPermissionEvaluator : IGsipPermissionEvaluator
{
    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        AssertEx.That(name == "GSIP.Execution", "Marriage Cases runtime must use the canonical named HttpClient.");
        return client;
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

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

static class SyntheticToken
{
    public static string FutureJwt()
    {
        static string Base64Url(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url("{\"exp\":4102444800}")}.synthetic";
    }
}

static class AssertEx
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
