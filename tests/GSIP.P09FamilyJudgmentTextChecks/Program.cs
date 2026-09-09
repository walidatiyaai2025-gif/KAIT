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

var connection = Environment.GetEnvironmentVariable("GSIP_P09_JUDGMENT_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_JUDGMENT_SQL is required.");

var dbOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(dbOptions);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(TestValues.Now)).SeedAsync();

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "FAMILYJUDGMENTTEXT" && item.IsCurrent);

    ValidateOfficialDeferredContract(service);
    await ValidateNoImplicitCredentialSharingAsync(db, service);
    await ValidateSeededServiceFailsClosedAsync(service);
    await ValidateSyntheticAuthorizationFailureAsync(service);
    await ValidateSyntheticUnknownInputRejectedAsync(service);
    await ValidateSyntheticWrongProfileAndEnvironmentAsync(service);
    await ValidateSyntheticForgedSecretRefAsync(service);
    await ValidateSyntheticBoundedResponseAsync(service);
    await WriteSafeEvidenceAsync();

    Console.WriteLine("P09_FAMILY_JUDGMENT_TEXT_ACCEPTANCE=PASS_FAIL_CLOSED_SCOPE");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static void ValidateOfficialDeferredContract(CatalogService service)
{
    using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "docs", "moj-api-reference", "p09", "api-196-family-judgment-text.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 196, "Wrong API196 authoritative snapshot.");
    Check(root.GetProperty("serviceName").GetString() == "Family Judgment Text Service", "API196 service-name snapshot drifted.");
    Check(root.GetProperty("captureStatus").GetString() == "DEFERRED_EXTERNAL", "API196 operation contract was promoted without official evidence.");
    Check(root.GetProperty("evidence").GetProperty("classification").GetString() == "OFFICIAL_CAIT_SPECIFICATION_NOT_RETRIEVABLE_WITHOUT_SIGN_IN",
        "API196 evidence classification changed unexpectedly.");

    var items = root.GetProperty("contractItems");
    foreach (var property in items.EnumerateObject())
    {
        Check(property.Value.GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
            $"API196 contract item {property.Name} was promoted without official evidence.");
    }

    Check(items.GetProperty("httpMethod").GetProperty("value").ValueKind == JsonValueKind.Null
        && items.GetProperty("relativePath").GetProperty("value").ValueKind == JsonValueKind.Null
        && items.GetProperty("requestContentType").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API196 invented method/path/content type.");
    Check(items.GetProperty("requestFields").GetProperty("fields").GetArrayLength() == 0,
        "API196 invented request fields from historical case-number/type notes.");
    Check(items.GetProperty("operationAuthentication").GetProperty("requirements").GetArrayLength() == 0,
        "API196 invented operation authentication.");
    Check(items.GetProperty("successStatusesAndSchema").GetProperty("responses").GetArrayLength() == 0
        && items.GetProperty("errorStatusesAndSchemas").GetProperty("responses").GetArrayLength() == 0,
        "API196 invented success/error schemas.");
    Check(items.GetProperty("responseFields").GetProperty("fields").GetArrayLength() == 0
        && items.GetProperty("resultMappingCandidates").GetProperty("fields").GetArrayLength() == 0,
        "API196 invented response fields/result mappings.");

    Check(service.Fields.Count == 0, "API196 seed contains unproven request fields.");
    Check(service.ResultMappings.Count == 0, "API196 seed contains unproven result mappings.");
    Check(service.EnvironmentConfigs.Count == 2, "API196 must retain isolated UAT and Production rows.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && string.IsNullOrEmpty(uat.BaseUrl)
        && string.IsNullOrEmpty(uat.RelativePath)
        && string.IsNullOrEmpty(uat.HttpMethod)
        && string.IsNullOrEmpty(uat.ContentType)
        && uat.AuthProfileId is null
        && uat.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API196 UAT contains invented/executable operation metadata.");
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null
        && production.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API196 Production must remain gateway-prefix-only and non-executable.");
}

static async Task ValidateNoImplicitCredentialSharingAsync(GsipDbContext db, CatalogService service)
{
    Check(await db.AuthProfiles.CountAsync(item => item.OwnerServiceId == service.Id) == 0,
        "API196 acquired a service-specific AuthProfile without official auth evidence.");
    Check(await db.AuthProfileBindings.CountAsync(item => item.ServiceId == service.Id) == 0,
        "API196 acquired an implicit/shared AuthProfile binding.");
    Check(await db.AuthProfileSecrets.AnyAsync(item => item.AuthProfile != null && item.AuthProfile.OwnerServiceId == service.Id) == false,
        "API196 acquired secret references without official auth evidence.");

    var marriageProfile = await db.AuthProfiles.AsNoTracking()
        .Include(item => item.Bindings)
        .SingleAsync();
    Check(marriageProfile.OwnerServiceId != service.Id
        && marriageProfile.Bindings.All(binding => binding.ServiceId != service.Id),
        "API196 implicitly reused the Marriage Cases credential scope.");
}

static async Task ValidateSeededServiceFailsClosedAsync(CatalogService service)
{
    using var fixture = RuntimeFixture.Create(service, RuntimeMode.SeededDeferred);
    await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["undocumented"] = TestValues.PrivateSentinel })));
    await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.ProductionId,
        new Dictionary<string, string?>())));

    Check(fixture.Handler.Calls == 0 && fixture.Resolver.Calls == 0,
        "Deferred API196 execution reached transport or secret resolution.");
    Check(NoSensitiveLogMaterial(fixture),
        "Deferred API196 input leaked to logs.");
}

static async Task ValidateSyntheticAuthorizationFailureAsync(CatalogService service)
{
    using var fixture = RuntimeFixture.Create(service, RuntimeMode.DeniedPermission);
    await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>())));
    Check(fixture.Handler.Calls == 0 && fixture.Resolver.Calls == 0,
        "Unauthorized synthetic API196 execution reached transport or secrets.");
    Check(NoSensitiveLogMaterial(fixture), "Authorization failure leaked sensitive synthetic material.");
}

static async Task ValidateSyntheticUnknownInputRejectedAsync(CatalogService service)
{
    using var fixture = RuntimeFixture.Create(service, RuntimeMode.CorrectSyntheticProfile);
    await ExpectValidationRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["historical-case-hint"] = TestValues.PrivateSentinel })));
    Check(fixture.Handler.Calls == 0 && fixture.Resolver.Calls == 0,
        "Unproven API196 input reached transport or secret resolution.");
    Check(NoSensitiveLogMaterial(fixture), "Rejected unproven API196 input leaked to logs.");
}

static async Task ValidateSyntheticWrongProfileAndEnvironmentAsync(CatalogService service)
{
    using var wrongProfile = RuntimeFixture.Create(service, RuntimeMode.WrongProfile);
    await ExpectRejectedAsync(() => wrongProfile.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>())));
    Check(wrongProfile.Handler.Calls == 0 && wrongProfile.Resolver.Calls == 0,
        "Cross-service AuthProfile reached transport or secrets.");

    using var wrongEnvironment = RuntimeFixture.Create(service, RuntimeMode.CorrectSyntheticProfile);
    await ExpectRejectedAsync(() => wrongEnvironment.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.ProductionId, new Dictionary<string, string?>())));
    Check(wrongEnvironment.Handler.Calls == 0 && wrongEnvironment.Resolver.Calls == 0,
        "Wrong-environment request fell back to synthetic UAT scope.");
}

static async Task ValidateSyntheticForgedSecretRefAsync(CatalogService service)
{
    using var fixture = RuntimeFixture.Create(service, RuntimeMode.ForgedSecretRef);
    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));
    Check(result.Outcome == ServiceExecutionOutcome.AuthenticationUnavailable,
        "Forged synthetic SecretRef did not fail closed as AuthenticationUnavailable.");
    Check(fixture.Resolver.Calls == 1 && fixture.Handler.Calls == 0,
        "Forged synthetic SecretRef reached outbound transport.");
    Check(NoSensitiveLogMaterial(fixture), "Forged SecretRef path leaked sensitive synthetic material.");
}

static async Task ValidateSyntheticBoundedResponseAsync(CatalogService service)
{
    using var fixture = RuntimeFixture.Create(service, RuntimeMode.BoundedResponse);
    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        TestValues.Principal, service.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));

    Check(result.Outcome == ServiceExecutionOutcome.ResponseTooLarge,
        "Generic API196 boundary did not classify oversized response as ResponseTooLarge.");
    Check(result.RawResponse.Length == 0 && result.StructuredResult.Count == 0,
        "Oversized response was retained or mapped.");
    Check(fixture.Resolver.Calls == 1 && fixture.Handler.Calls == 1 && fixture.Handler.SyntheticHeaderObserved,
        "Synthetic exact-scope auth did not reach the bounded-response transport exactly once.");
    Check(NoSensitiveLogMaterial(fixture),
        "Synthetic judgment/header material leaked to runtime logs.");
}

static bool NoSensitiveLogMaterial(RuntimeFixture fixture) =>
    fixture.Logger.Messages.All(message =>
        !message.Contains(TestValues.PrivateSentinel, StringComparison.Ordinal)
        && !message.Contains(TestValues.JudgmentSentinel, StringComparison.Ordinal)
        && !message.Contains(TestValues.SyntheticSecretMaterial, StringComparison.Ordinal)
        && !message.Contains("sr1_", StringComparison.Ordinal));

static async Task ExpectRejectedAsync(Func<Task<ServiceExecutionResult>> action)
{
    try
    {
        _ = await action();
        throw new InvalidOperationException("Expected fail-closed execution rejection.");
    }
    catch (ServiceExecutionRejectedException ex)
    {
        Check(ex.Message == ServiceExecutionRejectedException.SafeMessage,
            "Execution rejection did not use the safe constant message.");
    }
}

static async Task ExpectValidationRejectedAsync(Func<Task<ServiceExecutionResult>> action)
{
    try
    {
        _ = await action();
        throw new InvalidOperationException("Expected fail-closed input validation rejection.");
    }
    catch (ServiceExecutionValidationException ex)
    {
        Check(ex.Message == ServiceExecutionValidationException.SafeMessage && ex.Errors.Count > 0,
            "Input validation did not fail with the canonical safe validation contract.");
    }
}

static async Task WriteSafeEvidenceAsync()
{
    var directory = Path.Combine("artifacts", "p09-family-judgment-text-evidence");
    Directory.CreateDirectory(directory);
    var payload = JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::family-judgment-text-service",
        apiId = 196,
        automatedScope = "PASS_FAIL_CLOSED_AND_GENERIC_SECURITY_BOUNDARY",
        officialOperationContract = "DEFERRED_EXTERNAL",
        historicalCaseFieldHintsUsed = false,
        seededRequestFields = 0,
        seededResultMappings = 0,
        seededAuthProfile = false,
        uatExecutable = false,
        productionExecutable = false,
        marriageCredentialReuse = false,
        authorizationFailureRejected = true,
        unprovenInputRejected = true,
        wrongAuthProfileRejected = true,
        wrongEnvironmentRejected = true,
        forgedSecretRefRejectedBeforeTransport = true,
        genericOversizeResponseRejectedWithoutRawRetention = true,
        syntheticSensitiveMaterialLogged = false,
        liveUatCalledByCi = false,
        officialRequestValidationTest = "BLOCKED_DEFERRED_EXTERNAL_REQUEST_SCHEMA_NOT_PASS",
        officialSuccessSchemaTest = "BLOCKED_DEFERRED_EXTERNAL_SUCCESS_SCHEMA_NOT_PASS",
        officialErrorSchemaTest = "BLOCKED_DEFERRED_EXTERNAL_ERROR_SCHEMA_NOT_PASS",
        malformedOfficialResponseTest = "BLOCKED_DEFERRED_EXTERNAL_RESPONSE_SCHEMA_NOT_PASS",
        officialJudgmentTextMappingAndSensitivity = "BLOCKED_DEFERRED_EXTERNAL_RESPONSE_FIELDS_NOT_PASS",
        officialOperationAuthComposition = "BLOCKED_DEFERRED_EXTERNAL_AUTH_SCHEMA_NOT_PASS"
    }, new JsonSerializerOptions { WriteIndented = true });
    var path = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(path, payload);
    var persisted = await File.ReadAllTextAsync(path);
    Check(!persisted.Contains(TestValues.PrivateSentinel, StringComparison.Ordinal)
        && !persisted.Contains(TestValues.JudgmentSentinel, StringComparison.Ordinal)
        && !persisted.Contains(TestValues.SyntheticSecretMaterial, StringComparison.Ordinal)
        && !persisted.Contains("sr1_", StringComparison.Ordinal),
        "API196 evidence artifact contains synthetic sensitive/reference material.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static class TestValues
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-09T10:10:00Z");
    public static readonly ClaimsPrincipal Principal = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "99000000-0000-0000-0000-000000000960")],
        "SyntheticP09Api196"));
    public const string PrivateSentinel = "SYNTHETIC_PRIVATE_INPUT_DO_NOT_LOG";
    public const string JudgmentSentinel = "SYNTHETIC_JUDGMENT_TEXT_DO_NOT_LOG";
    public const string SyntheticSecretMaterial = "SYNTHETIC_HEADER_MATERIAL_DO_NOT_LOG";
    public static readonly Guid ForeignServiceId = Guid.Parse("99000000-0000-0000-0000-000000000961");
    public static readonly Guid ProfileId = Guid.Parse("99000000-0000-0000-0000-000000000962");
    public static readonly SecretRef ForgedReference = SecretRef.Parse("sr1_" + new string('F', 43));
    public static readonly SecretRef SyntheticReference = SecretRef.Parse("sr1_" + new string('A', 43));
}

enum RuntimeMode
{
    SeededDeferred,
    DeniedPermission,
    WrongProfile,
    CorrectSyntheticProfile,
    ForgedSecretRef,
    BoundedResponse
}

sealed class RuntimeFixture : IDisposable
{
    private RuntimeFixture(
        GenericServiceExecutionEngine engine,
        TestHandler handler,
        TrackingSecretResolver resolver,
        RecordingLogger<GenericServiceExecutionEngine> logger,
        InMemoryTokenCache cache)
    {
        Engine = engine;
        Handler = handler;
        Resolver = resolver;
        Logger = logger;
        Cache = cache;
    }

    public GenericServiceExecutionEngine Engine { get; }
    public TestHandler Handler { get; }
    public TrackingSecretResolver Resolver { get; }
    public RecordingLogger<GenericServiceExecutionEngine> Logger { get; }
    private InMemoryTokenCache Cache { get; }

    public static RuntimeFixture Create(CatalogService seeded, RuntimeMode mode)
    {
        var service = mode == RuntimeMode.SeededDeferred ? CloneService(seeded) : CloneSyntheticActiveUat(seeded);
        AuthProfileDescriptor? profile = null;
        if (mode != RuntimeMode.SeededDeferred)
        {
            var ownerService = mode == RuntimeMode.WrongProfile ? TestValues.ForeignServiceId : service.Id;
            var bindingService = mode == RuntimeMode.WrongProfile ? TestValues.ForeignServiceId : service.Id;
            var reference = mode == RuntimeMode.ForgedSecretRef ? TestValues.ForgedReference : TestValues.SyntheticReference;
            profile = new AuthProfileDescriptor(
                TestValues.ProfileId,
                ownerService,
                CatalogEnvironmentCodes.UatId,
                "Synthetic API196 boundary profile",
                AuthProfileType.ApiKeyHeader,
                true,
                1,
                "synthetic-api196",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                [new AuthProfileSecretDescriptor("x-synthetic-key", reference, 1)],
                [new AuthProfileBindingDescriptor(bindingService, CatalogEnvironmentCodes.UatId, false,
                    "synthetic-api196", "synthetic exact-scope boundary only", DateTimeOffset.UnixEpoch)]);
        }

        var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
            [], [service],
            [
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, NameAr = "اختبار", NameEn = "UAT", Active = true },
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.ProductionId, Code = CatalogEnvironmentCodes.Production, NameAr = "إنتاج", NameEn = "Production", Active = true }
            ]));
        var profiles = new FakeAuthProfiles(profile);
        var resolver = new TrackingSecretResolver(mode == RuntimeMode.ForgedSecretRef);
        var handler = new TestHandler(mode == RuntimeMode.BoundedResponse);
        var logger = new RecordingLogger<GenericServiceExecutionEngine>();
        var cache = new InMemoryTokenCache(new FixedClock(TestValues.Now), new TokenCacheOptions());
        IGsipPermissionEvaluator permissions = mode == RuntimeMode.DeniedPermission
            ? new DenyPermissionEvaluator()
            : new AllowPermissionEvaluator();
        var engine = new GenericServiceExecutionEngine(
            new ServiceExecutionSecurityGate(metadata, permissions, profiles),
            metadata,
            profiles,
            resolver,
            new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
            Options.Create(new ServiceExecutionRuntimeOptions
            {
                MaxAttempts = 1,
                RetryDelayMilliseconds = 0,
                MaxRawResponseBytes = 1024
            }),
            logger,
            cache);
        return new RuntimeFixture(engine, handler, resolver, logger, cache);
    }

    public void Dispose() => Cache.Dispose();

    private static CatalogService CloneService(CatalogService source) => new()
    {
        Id = source.Id,
        DefinitionKey = source.DefinitionKey,
        EntityId = source.EntityId,
        Code = source.Code,
        NameAr = source.NameAr,
        NameEn = source.NameEn,
        DescriptionAr = source.DescriptionAr,
        DescriptionEn = source.DescriptionEn,
        Active = true,
        Version = source.Version,
        IsCurrent = true,
        Fields = [],
        ResultMappings = [],
        EnvironmentConfigs = source.EnvironmentConfigs.Select(CloneConfig).ToList()
    };

    private static CatalogService CloneSyntheticActiveUat(CatalogService source)
    {
        var clone = CloneService(source);
        clone.EnvironmentConfigs =
        [
            new ServiceEnvironmentConfig
            {
                Id = Guid.Parse("99000000-0000-0000-0000-000000000963"),
                ServiceId = clone.Id,
                EnvironmentId = CatalogEnvironmentCodes.UatId,
                BaseUrl = "https://synthetic.invalid/runtime-boundary",
                RelativePath = "/not-an-official-api196-path",
                HttpMethod = "POST",
                ContentType = "application/json",
                NonSecretHeadersJson = "{}",
                TimeoutSeconds = 5,
                TlsPolicy = "SystemDefault",
                ValidateServerCertificate = true,
                ProxyUrl = string.Empty,
                Active = true,
                AuthProfileId = TestValues.ProfileId
            }
        ];
        return clone;
    }

    private static ServiceEnvironmentConfig CloneConfig(ServiceEnvironmentConfig source) => new()
    {
        Id = source.Id,
        ServiceId = source.ServiceId,
        EnvironmentId = source.EnvironmentId,
        BaseUrl = source.BaseUrl,
        RelativePath = source.RelativePath,
        HttpMethod = source.HttpMethod,
        ContentType = source.ContentType,
        NonSecretHeadersJson = source.NonSecretHeadersJson,
        TimeoutSeconds = source.TimeoutSeconds,
        TlsPolicy = source.TlsPolicy,
        ValidateServerCertificate = source.ValidateServerCertificate,
        ProxyUrl = source.ProxyUrl,
        HealthPath = source.HealthPath,
        HealthMethod = source.HealthMethod,
        Active = source.Active,
        LastTestedAtUtc = source.LastTestedAtUtc,
        LastTestStatus = source.LastTestStatus,
        AuthProfileId = source.AuthProfileId
    };
}

sealed class TrackingSecretResolver(bool reject) : ISecretMaterialResolver
{
    public int Calls { get; private set; }

    public async Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        if (reject)
            throw new SecretReferenceRejectedException();

        var material = Encoding.UTF8.GetBytes(TestValues.SyntheticSecretMaterial);
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

sealed class FakeAuthProfiles(AuthProfileDescriptor? profile) : IAuthProfileService
{
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(profile);
    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        profile is not null && authProfileId == profile.Id
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

sealed class AllowPermissionEvaluator : IGsipPermissionEvaluator
{
    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class DenyPermissionEvaluator : IGsipPermissionEvaluator
{
    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (!string.Equals(name, "GSIP.Execution", StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime did not use the canonical named client.");
        return client;
    }
}

sealed class TestHandler(bool returnOversize) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool SyntheticHeaderObserved { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (!returnOversize)
            throw new InvalidOperationException("API196 fail-closed boundary acceptance must never reach transport.");

        SyntheticHeaderObserved = request.Headers.TryGetValues("x-synthetic-key", out var values)
            && values.SingleOrDefault() == TestValues.SyntheticSecretMaterial;
        var body = new string('X', 2048) + TestValues.JudgmentSentinel;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
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
