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

var connection = Environment.GetEnvironmentVariable("GSIP_P09_MARRIAGE_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_MARRIAGE_SQL is required.");

var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "99000000-0000-0000-0000-000000000901")],
    "SyntheticP09Marriage"));

var dbOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(dbOptions);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(TestValues.Now)).SeedAsync();

    var marriage = await db.CatalogServices.AsNoTracking()
        .Include(service => service.EnvironmentConfigs)
        .Include(service => service.Fields)
        .Include(service => service.ResultMappings)
        .SingleAsync(service => service.Code == "MARRIAGECASES" && service.IsCurrent);

    ValidateOfficialContractAndSeed(marriage);
    await ExactTransportAndCompositeAuthAsync(marriage, principal);
    await RequiredFieldFailsBeforeSecretsAsync(marriage, principal);
    await Target401DoesNotStormTokenEndpointAsync(marriage, principal);
    await CrossScopeCredentialsFailClosedAsync(marriage, principal);
    await WriteSafeEvidenceAsync();

    Console.WriteLine("P09_MARRIAGE_CASES_ACCEPTANCE=PASS_AUTONOMOUS_SCOPE");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static void ValidateOfficialContractAndSeed(CatalogService marriage)
{
    using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "docs", "moj-api-reference", "p09", "api-129-marriage-cases.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 129, "Wrong authoritative contract snapshot.");
    Check(root.GetProperty("captureStatus").GetString() == "PARTIAL_PROVEN", "Unexpected API 129 capture status.");

    var operation = root.GetProperty("operations").EnumerateArray()
        .Single(x => x.GetProperty("name").GetString() == "Marriage Cases");
    Check(operation.GetProperty("method").GetString() == "POST", "Marriage Cases method drifted.");
    Check(operation.GetProperty("relativePath").GetString() == MojMetadataSeedService.Api129TargetPath,
        "Marriage Cases path drifted.");
    Check(operation.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded",
        "Marriage Cases content type drifted.");
    var field = operation.GetProperty("requestFields").EnumerateArray().Single();
    Check(field.GetProperty("name").GetString() == "civilId" && field.GetProperty("required").GetBoolean(),
        "Marriage Cases exact request key/requiredness drifted.");
    Check(field.GetProperty("typeStatus").GetString() == "DEFERRED_EXTERNAL"
        && field.GetProperty("validationStatus").GetString() == "DEFERRED_EXTERNAL",
        "Unproven civilId type/validation was promoted without official evidence.");
    Check(operation.GetProperty("successResponses").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Unproven success schema was promoted without official evidence.");
    Check(operation.GetProperty("documentedErrorResponses").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Unproven error schema was promoted without official evidence.");
    Check(operation.GetProperty("responseFields").GetProperty("status").GetString() == "DEFERRED_EXTERNAL"
        && operation.GetProperty("resultMappingCandidates").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "Unproven response mappings were promoted without official evidence.");

    var auth = operation.GetProperty("authentication").EnumerateArray()
        .Select(x => x.GetProperty("scheme").GetString())
        .ToHashSet(StringComparer.Ordinal);
    Check(auth.SetEquals(["ApiKeyHeader", "Bearer"]), "Marriage Cases composite auth drifted.");

    var uat = marriage.EnvironmentConfigs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = marriage.EnvironmentConfigs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(uat.BaseUrl == MojMetadataSeedService.Api129UatBaseUrl
        && uat.RelativePath == MojMetadataSeedService.Api129TargetPath
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/x-www-form-urlencoded"
        && !uat.Active
        && uat.AuthProfileId.HasValue,
        "Seeded UAT Marriage binding drifted from proven contract or became implicitly active.");
    Check(production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && !production.Active
        && production.AuthProfileId is null,
        "Production must remain disabled/prefix-only without UAT fallback.");

    var fields = marriage.Fields.ToArray();
    Check(fields.Length == 1
        && fields[0].Key == "civilId"
        && fields[0].Required
        && fields[0].Sensitive
        && fields[0].Masking == "Last4",
        "Seeded Marriage field lost exact proven key/required/sensitive metadata.");
    Check(fields[0].FieldType == "deferred"
        && string.IsNullOrEmpty(fields[0].Regex)
        && fields[0].Minimum is null
        && fields[0].Maximum is null
        && fields[0].MinLength is null
        && fields[0].MaxLength is null,
        "Seed invented validation beyond official evidence.");
    Check(marriage.ResultMappings.Count == 0, "Business result mappings must remain empty while response schema is deferred.");

    var view = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "GSIP.Web", "Views", "Execution", "Index.cshtml"));
    Check(view.Contains("required=\"@field.Required\"", StringComparison.Ordinal), "Generic client required validation is missing.");
    Check(view.Contains("name=\"Inputs[@field.Key]\"", StringComparison.Ordinal), "Generic UI no longer preserves exact metadata keys.");
    Check(view.Contains("value=\"@(field.Sensitive ? null : submittedValue)\"", StringComparison.Ordinal),
        "Generic UI may redisplay sensitive submitted values.");
}

static async Task ExactTransportAndCompositeAuthAsync(CatalogService marriage, ClaimsPrincipal principal)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK);
    var result = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
        principal, marriage.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = TestValues.RequestValue }));

    Check(result.Outcome == ServiceExecutionOutcome.Success && result.StatusCode == 200 && result.Attempts == 1,
        "Synthetic Marriage execution did not succeed exactly once.");
    Check(result.StructuredResult.Count == 0, "Deferred response schema produced invented structured mappings.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.TargetCalls == 1,
        "Composite flow must use one token call and one target call.");
    Check(fixture.Handler.TokenPath == "/WSWEB/WS/v1/marriage/genToken",
        "Token URI composition lost the official base path prefix.");
    Check(fixture.Handler.Target is not null
        && fixture.Handler.Target.Uri.AbsoluteUri == "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage/marriageCasesAPIGEE",
        "Target URI composition lost the official base path prefix.");
    Check(fixture.Handler.TokenMethod == HttpMethod.Post
        && fixture.Handler.TokenContentType == "application/x-www-form-urlencoded"
        && fixture.Handler.TokenHasExactApiKey,
        "Token request method/content-type/API-key composition is wrong.");
    Check(fixture.Handler.TokenBody.Contains("username=" + Uri.EscapeDataString(TestValues.Username), StringComparison.Ordinal)
        && fixture.Handler.TokenBody.Contains("password=" + Uri.EscapeDataString(TestValues.Password), StringComparison.Ordinal),
        "Token form serialization is wrong.");
    Check(fixture.Handler.Target!.Method == HttpMethod.Post
        && fixture.Handler.Target.ContentType == "application/x-www-form-urlencoded"
        && fixture.Handler.Target.Body == "civilId=" + Uri.EscapeDataString(TestValues.RequestValue),
        "Target form serialization or exact field casing is wrong.");
    Check(fixture.Handler.Target.HasExactApiKey && fixture.Handler.Target.HasBearer,
        "Target did not receive exact x-api-key plus Bearer composition.");
    Check(fixture.Resolver.Calls.Count == 3
        && fixture.Resolver.Calls.All(x => x.ServiceId == marriage.Id
            && x.EnvironmentId == CatalogEnvironmentCodes.UatId
            && x.AuthProfileId == fixture.ProfileId),
        "Secret resolution escaped exact Service + Environment + AuthProfile scope.");
    Check(fixture.Logger.Messages.All(x => !x.Contains(TestValues.RequestValue, StringComparison.Ordinal)),
        "Sensitive request value leaked to logs.");
    Check(!result.ToString().Contains(TestValues.RequestValue, StringComparison.Ordinal),
        "Sensitive request value leaked to result diagnostics.");
}

static async Task RequiredFieldFailsBeforeSecretsAsync(CatalogService marriage, ClaimsPrincipal principal)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK);
    try
    {
        _ = await fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, marriage.Id, CatalogEnvironmentCodes.UatId, new Dictionary<string, string?>()));
        throw new InvalidOperationException("Expected required field rejection.");
    }
    catch (ServiceExecutionValidationException ex)
    {
        Check(ex.Message == ServiceExecutionValidationException.SafeMessage, "Validation error message is not safe/constant.");
        Check(ex.Errors.TryGetValue("civilId", out var code) && code == "Required",
            "Server-side required validation lost exact official key casing.");
    }

    Check(fixture.Resolver.Calls.Count == 0 && fixture.Handler.TokenCalls == 0 && fixture.Handler.TargetCalls == 0,
        "Invalid request reached secrets or transport.");
}

static async Task Target401DoesNotStormTokenEndpointAsync(CatalogService marriage, ClaimsPrincipal principal)
{
    using var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.Unauthorized);
    var command = new ServiceExecutionCommand(
        principal, marriage.Id, CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?> { ["civilId"] = TestValues.RequestValue });

    var first = await fixture.Engine.ExecuteAsync(command);
    var second = await fixture.Engine.ExecuteAsync(command);
    Check(first.Outcome == ServiceExecutionOutcome.Unauthorized && first.Attempts == 1
        && second.Outcome == ServiceExecutionOutcome.Unauthorized && second.Attempts == 1,
        "Target 401 must remain single-attempt for POST.");
    Check(fixture.Handler.TokenCalls == 1 && fixture.Handler.TargetCalls == 2,
        "Target 401 caused token reacquisition/retry storm.");
}

static async Task CrossScopeCredentialsFailClosedAsync(CatalogService marriage, ClaimsPrincipal principal)
{
    var otherService = Guid.Parse("99000000-0000-0000-0000-000000000999");
    using (var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK, ownerServiceId: otherService))
    {
        await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, marriage.Id, CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = TestValues.RequestValue })));
        Check(fixture.Resolver.Calls.Count == 0 && fixture.Handler.TokenCalls == 0 && fixture.Handler.TargetCalls == 0,
            "Cross-service profile reached secrets/transport.");
    }

    using (var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK, ownerEnvironmentId: CatalogEnvironmentCodes.ProductionId))
    {
        await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, marriage.Id, CatalogEnvironmentCodes.UatId,
            new Dictionary<string, string?> { ["civilId"] = TestValues.RequestValue })));
        Check(fixture.Resolver.Calls.Count == 0 && fixture.Handler.TokenCalls == 0 && fixture.Handler.TargetCalls == 0,
            "Cross-environment profile reached secrets/transport.");
    }

    using (var fixture = RuntimeFixture.Create(marriage, HttpStatusCode.OK))
    {
        await ExpectRejectedAsync(() => fixture.Engine.ExecuteAsync(new ServiceExecutionCommand(
            principal, marriage.Id, CatalogEnvironmentCodes.ProductionId,
            new Dictionary<string, string?> { ["civilId"] = TestValues.RequestValue })));
        Check(fixture.Profiles.ResolveCalls == 0 && fixture.Resolver.Calls.Count == 0
            && fixture.Handler.TokenCalls == 0 && fixture.Handler.TargetCalls == 0,
            "Production request fell back to UAT configuration/authentication.");
    }
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
        Check(ex.Message == ServiceExecutionRejectedException.SafeMessage, "Execution rejection message is not safe/constant.");
    }
}

static async Task WriteSafeEvidenceAsync()
{
    var directory = Path.Combine("artifacts", "p09-marriage-cases-evidence");
    Directory.CreateDirectory(directory);
    var payload = JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::marriage-cases-service",
        apiId = 129,
        automatedScope = "PASS",
        liveUatCalledByCi = false,
        exactUriComposition = true,
        exactFormFieldCasing = true,
        clientAndServerRequiredValidation = true,
        compositeApiKeyAndBearer = true,
        crossServiceRejected = true,
        crossEnvironmentRejected = true,
        productionFallback = false,
        target401TokenStorm = false,
        targetSuccessSchema = "DEFERRED_EXTERNAL",
        targetDocumentedErrors = "DEFERRED_EXTERNAL",
        targetResponseFields = "DEFERRED_EXTERNAL",
        targetResultMappings = "DEFERRED_EXTERNAL",
        civilIdTypeAndValidation = "DEFERRED_EXTERNAL"
    }, new JsonSerializerOptions { WriteIndented = true });
    var path = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(path, payload);
    var persisted = await File.ReadAllTextAsync(path);
    Check(!persisted.Contains(TestValues.RequestValue, StringComparison.Ordinal)
        && !persisted.Contains(TestValues.ApiKey, StringComparison.Ordinal)
        && !persisted.Contains(TestValues.Username, StringComparison.Ordinal)
        && !persisted.Contains(TestValues.Password, StringComparison.Ordinal),
        "Evidence artifact contains synthetic secret/request plaintext.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static class TestValues
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-09T08:46:00Z");
    public const string RequestValue = "SYNTHETIC_MARRIAGE_REQUEST";
    public const string ApiKey = "SYNTHETIC_P09_API_KEY";
    public const string Username = "SYNTHETIC_P09_USER";
    public const string Password = "SYNTHETIC_P09_PASSWORD";

    public static string FutureJwt()
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("{\"alg\":\"none\"}")}.{Encode("{\"exp\":4102444800}")}.synthetic";
    }
}

sealed class RuntimeFixture : IDisposable
{
    private RuntimeFixture(
        GenericServiceExecutionEngine engine,
        MarriageHandler handler,
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
    public MarriageHandler Handler { get; }
    public FakeSecretResolver Resolver { get; }
    public RecordingLogger<GenericServiceExecutionEngine> Logger { get; }
    public FakeAuthProfiles Profiles { get; }
    public InMemoryTokenCache Cache { get; }
    public Guid ProfileId { get; }

    public static RuntimeFixture Create(
        CatalogService seeded,
        HttpStatusCode targetStatus,
        Guid? ownerServiceId = null,
        Guid? ownerEnvironmentId = null)
    {
        var profileId = Guid.Parse("99000000-0000-0000-0000-000000000971");
        var service = CloneActiveUat(seeded, profileId);
        var ownerService = ownerServiceId ?? service.Id;
        var ownerEnvironment = ownerEnvironmentId ?? CatalogEnvironmentCodes.UatId;
        var profile = new AuthProfileDescriptor(
            profileId,
            ownerService,
            ownerEnvironment,
            "Synthetic P09 Marriage profile",
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
            [new AuthProfileBindingDescriptor(ownerService, ownerEnvironment, false, "synthetic-p09", "synthetic exact scope", DateTimeOffset.UnixEpoch)]);

        var metadata = new FakeMetadata(new MetadataCatalogSnapshot(
            [], [service],
            [
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, NameAr = "اختبار", NameEn = "UAT", Active = true },
                new CatalogEnvironment { Id = CatalogEnvironmentCodes.ProductionId, Code = CatalogEnvironmentCodes.Production, NameAr = "إنتاج", NameEn = "Production", Active = true }
            ]));
        var profiles = new FakeAuthProfiles(profile);
        var resolver = new FakeSecretResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-api-key"] = TestValues.ApiKey,
            ["username"] = TestValues.Username,
            ["password"] = TestValues.Password
        });
        var handler = new MarriageHandler(targetStatus, TestValues.FutureJwt(), TestValues.ApiKey);
        var logger = new RecordingLogger<GenericServiceExecutionEngine>();
        var cache = new InMemoryTokenCache(new FixedClock(TestValues.Now), new TokenCacheOptions());
        var engine = new GenericServiceExecutionEngine(
            new ServiceExecutionSecurityGate(metadata, new AllowPermissionEvaluator(), profiles),
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

    private static CatalogService CloneActiveUat(CatalogService seeded, Guid profileId)
    {
        var source = seeded.EnvironmentConfigs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
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

sealed record TargetSnapshot(HttpMethod Method, Uri Uri, string ContentType, string Body, bool HasExactApiKey, bool HasBearer);

sealed class MarriageHandler(HttpStatusCode targetStatus, string token, string expectedApiKey) : HttpMessageHandler
{
    public int TokenCalls { get; private set; }
    public int TargetCalls { get; private set; }
    public string TokenPath { get; private set; } = string.Empty;
    public HttpMethod? TokenMethod { get; private set; }
    public string TokenContentType { get; private set; } = string.Empty;
    public string TokenBody { get; private set; } = string.Empty;
    public bool TokenHasExactApiKey { get; private set; }
    public TargetSnapshot? Target { get; private set; }

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
                Content = new StringContent(JsonSerializer.Serialize(new { data = token }), Encoding.UTF8, "application/json")
            };
        }

        if (path.EndsWith("/marriageCasesAPIGEE", StringComparison.Ordinal))
        {
            TargetCalls++;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKeyMatches = request.Headers.TryGetValues("x-api-key", out var values)
                && values.SingleOrDefault() == expectedApiKey;
            Target = new TargetSnapshot(
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

        throw new InvalidOperationException("Unexpected synthetic Marriage Cases path.");
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
        var bytes = Encoding.UTF8.GetBytes(value);
        try { return await operation(bytes, cancellationToken); }
        finally { Array.Clear(bytes); }
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
        authProfileId == profile.Id ? Task.FromResult(profile) : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
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
            throw new InvalidOperationException("Runtime did not use canonical named HttpClient.");
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
