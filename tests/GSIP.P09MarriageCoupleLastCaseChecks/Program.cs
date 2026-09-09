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

var connection = Environment.GetEnvironmentVariable("GSIP_P09_COUPLE_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_COUPLE_SQL is required.");

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
        .SingleAsync(item => item.Code == "MARRIAGECOUPLELASTCASE" && item.IsCurrent);

    ValidateOfficialDeferredContract(service);
    await ValidateNoImplicitCredentialSharingAsync(db, service);
    await ValidateSeededServiceFailsClosedAsync(service);
    await ValidateSyntheticWrongProfileAndEnvironmentAsync(service);
    await ValidateSyntheticForgedSecretRefAsync(service);
    await WriteSafeEvidenceAsync();

    Console.WriteLine("P09_MARRIAGE_COUPLE_LAST_CASE_ACCEPTANCE=PASS_FAIL_CLOSED_SCOPE");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static void ValidateOfficialDeferredContract(CatalogService service)
{
    using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "docs", "moj-api-reference", "p09", "api-130-marriage-couple-last-case.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 130, "Wrong API130 authoritative snapshot.");
    Check(root.GetProperty("serviceName").GetString() == "Marriage Couple Last Case Service", "API130 service-name snapshot drifted.");
    Check(root.GetProperty("captureStatus").GetString() == "DEFERRED_EXTERNAL", "API130 operation contract was promoted without official evidence.");
    Check(root.GetProperty("evidence").GetProperty("classification").GetString() == "OFFICIAL_CAIT_SPECIFICATION_NOT_RETRIEVABLE_WITHOUT_SIGN_IN",
        "API130 evidence classification changed unexpectedly.");

    var items = root.GetProperty("contractItems");
    foreach (var property in items.EnumerateObject())
    {
        Check(property.Value.GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
            $"API130 contract item {property.Name} was promoted without official evidence.");
    }
    Check(items.GetProperty("requestFields").GetProperty("fields").GetArrayLength() == 0,
        "API130 invented request fields, including screenshot-derived key names.");
    Check(items.GetProperty("operationAuthentication").GetProperty("requirements").GetArrayLength() == 0,
        "API130 invented operation authentication.");
    Check(items.GetProperty("successStatusesAndSchema").GetProperty("responses").GetArrayLength() == 0
        && items.GetProperty("errorStatusesAndSchemas").GetProperty("responses").GetArrayLength() == 0,
        "API130 invented success/error schemas.");
    Check(items.GetProperty("responseFields").GetProperty("fields").GetArrayLength() == 0
        && items.GetProperty("resultMappingCandidates").GetProperty("fields").GetArrayLength() == 0,
        "API130 invented response fields/result mappings.");

    Check(service.Fields.Count == 0, "API130 seed contains unproven request fields.");
    Check(service.ResultMappings.Count == 0, "API130 seed contains unproven result mappings.");
    Check(service.EnvironmentConfigs.Count == 2, "API130 must retain isolated UAT and Production rows.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && string.IsNullOrEmpty(uat.BaseUrl)
        && string.IsNullOrEmpty(uat.RelativePath)
        && string.IsNullOrEmpty(uat.HttpMethod)
        && string.IsNullOrEmpty(uat.ContentType)
        && uat.AuthProfileId is null
        && uat.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API130 UAT contains invented/executable operation metadata.");
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null
        && production.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API130 Production must remain gateway-prefix-only and non-executable.");
}

static async Task ValidateNoImplicitCredentialSharingAsync(GsipDbContext db, CatalogService service)
{
    Check(await db.AuthProfiles.CountAsync(item => item.OwnerServiceId == service.Id) == 0,
        "API130 acquired a service-specific AuthProfile without official auth evidence.");
    Check(await db.AuthProfileBindings.CountAsync(item => item.ServiceId == service.Id) == 0,
        "API130 acquired an implicit/shared AuthProfile binding.");
    Check(await db.AuthProfileSecrets.AnyAsync(item => item.AuthProfile.OwnerServiceId == service.Id) == false,
        "API130 acquired secret references without official auth evidence.");

    var marriageProfile = await db.AuthProfiles.AsNoTracking()
        .Include(item => item.Bindings)
        .SingleAsync();
    Check(marriageProfile.OwnerServiceId != service.Id
        && marriageProfile.Bindings.All(binding => binding.ServiceId != service.Id),
        "API130 implicitly reused the Marriage Cases credential scope.");
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
        "Deferred API130 execution reached transport or secret resolution.");
    Check(fixture.Logger.Messages.All(message => !message.Contains(TestValues.PrivateSentinel, StringComparison.Ordinal)),
        "Deferred API130 input leaked to logs.");
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
    Check(fixture.Logger.Messages.All(message => !message.Contains(TestValues.PrivateSentinel, StringComparison.Ordinal)),
        "Synthetic sensitive sentinel leaked to logs.");
}

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

static async Task WriteSafeEvidenceAsync()
{
    var directory = Path.Combine("artifacts", "p09-marriage-couple-last-case-evidence");
    Directory.CreateDirectory(directory);
    var payload = JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::marriage-couple-last-case-service",
        apiId = 130,
        automatedScope = "PASS_FAIL_CLOSED_BOUNDARY",
        officialOperationContract = "DEFERRED_EXTERNAL",
        screenshotDerivedFieldNamesUsed = false,
        seededRequestFields = 0,
        seededResultMappings = 0,
        seededAuthProfile = false,
        uatExecutable = false,
        productionExecutable = false,
        marriageCredentialReuse = false,
        wrongAuthProfileRejected = true,
        wrongEnvironmentRejected = true,
        forgedSecretRefRejectedBeforeTransport = true,
        liveUatCalledByCi = false,
        swappedOrMissingOfficialFieldsTest = "BLOCKED_DEFERRED_EXTERNAL_REQUEST_SCHEMA",
        malformedOfficialResponseTest = "BLOCKED_DEFERRED_EXTERNAL_RESPONSE_SCHEMA",
        documentedErrorHandlingTest = "BLOCKED_DEFERRED_EXTERNAL_ERROR_SCHEMA"
    }, new JsonSerializerOptions { WriteIndented = true });
    var path = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(path, payload);
    var persisted = await File.ReadAllTextAsync(path);
    Check(!persisted.Contains(TestValues.PrivateSentinel, StringComparison.Ordinal)
        && !persisted.Contains("sr1_", StringComparison.Ordinal),
        "API130 evidence artifact contains synthetic sensitive/reference material.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static class TestValues
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-09T09:42:00Z");
    public static readonly ClaimsPrincipal Principal = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "99000000-0000-0000-0000-000000000930")],
        "SyntheticP09Api130"));
    public const string PrivateSentinel = "SYNTHETIC_PRIVATE_INPUT_DO_NOT_LOG";
    public static readonly Guid ForeignServiceId = Guid.Parse("99000000-0000-0000-0000-000000000931");
    public static readonly Guid ProfileId = Guid.Parse("99000000-0000-0000-0000-000000000932");
    public static readonly SecretRef ForgedReference = SecretRef.Parse("sr1_" + new string('F', 43));
}

enum RuntimeMode
{
    SeededDeferred,
    WrongProfile,
    CorrectSyntheticProfile,
    ForgedSecretRef
}

sealed class RuntimeFixture : IDisposable
{
    private RuntimeFixture(
        GenericServiceExecutionEngine engine,
        CountingHandler handler,
        RejectingSecretResolver resolver,
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
    public CountingHandler Handler { get; }
    public RejectingSecretResolver Resolver { get; }
    public RecordingLogger<GenericServiceExecutionEngine> Logger { get; }
    private InMemoryTokenCache Cache { get; }

    public static RuntimeFixture Create(CatalogService seeded, RuntimeMode mode)
    {
        var service = mode == RuntimeMode.SeededDeferred ? CloneService(seeded) : CloneSyntheticActiveUat(seeded);
        AuthProfileDescriptor? profile = null;
        if (mode != RuntimeMode.SeededDeferred)
        {
            var ownerService = mode == RuntimeMode.WrongProfile ? TestValues.ForeignServiceId : service.Id;
            profile = new AuthProfileDescriptor(
                TestValues.ProfileId,
                ownerService,
                CatalogEnvironmentCodes.UatId,
                "Synthetic API130 boundary profile",
                AuthProfileType.ApiKeyHeader,
                true,
                1,
                "synthetic-api130",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                [new AuthProfileSecretDescriptor("x-synthetic-key", TestValues.ForgedReference, 1)],
                [new AuthProfileBindingDescriptor(ownerService, CatalogEnvironmentCodes.UatId, false,
                    "synthetic-api130", "synthetic boundary only", DateTimeOffset.UnixEpoch)]);
        }

        var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
            [], [service],
            [
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, NameAr = "اختبار", NameEn = "UAT", Active = true },
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.ProductionId, Code = CatalogEnvironmentCodes.Production, NameAr = "إنتاج", NameEn = "Production", Active = true }
            ]));
        var profiles = new FakeAuthProfiles(profile);
        var resolver = new RejectingSecretResolver();
        var handler = new CountingHandler();
        var logger = new RecordingLogger<GenericServiceExecutionEngine>();
        var cache = new InMemoryTokenCache(new FixedClock(TestValues.Now), new TokenCacheOptions());
        var engine = new GenericServiceExecutionEngine(
            new ServiceExecutionSecurityGate(metadata, new AllowPermissionEvaluator(), profiles),
            metadata,
            profiles,
            resolver,
            new FakeHttpClientFactory(new HttpClient(handler, disposeHandler: false)),
            Options.Create(new ServiceExecutionRuntimeOptions { MaxAttempts = 1, RetryDelayMilliseconds = 0 }),
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
        EnvironmentConfigs = source.EnvironmentConfigs.Select(config => CloneConfig(config)).ToList()
    };

    private static CatalogService CloneSyntheticActiveUat(CatalogService source)
    {
        var clone = CloneService(source);
        clone.EnvironmentConfigs =
        [
            new ServiceEnvironmentConfig
            {
                Id = Guid.Parse("99000000-0000-0000-0000-000000000933"),
                ServiceId = clone.Id,
                EnvironmentId = CatalogEnvironmentCodes.UatId,
                BaseUrl = "https://synthetic.invalid/runtime-boundary",
                RelativePath = "/not-an-official-api130-path",
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

sealed class RejectingSecretResolver : ISecretMaterialResolver
{
    public int Calls { get; private set; }
    public Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new SecretReferenceRejectedException();
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

sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (!string.Equals(name, "GSIP.Execution", StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime did not use the canonical named client.");
        return client;
    }
}

sealed class CountingHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        throw new InvalidOperationException("API130 boundary acceptance must never reach transport.");
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
