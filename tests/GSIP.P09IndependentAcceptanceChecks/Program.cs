using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_INDEPENDENT_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_INDEPENDENT_SQL is required.");

var candidateSha = Environment.GetEnvironmentVariable("CANDIDATE_SHA");
if (string.IsNullOrWhiteSpace(candidateSha) || candidateSha.Length < 7)
    throw new InvalidOperationException("CANDIDATE_SHA is required for exact-head evidence.");

var rootDirectory = Directory.GetCurrentDirectory();
var referenceDirectory = Path.Combine(rootDirectory, "docs", "moj-api-reference", "p09");
var manifestPath = Path.Combine(referenceDirectory, "manifest.json");
using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
var manifestRoot = manifest.RootElement;
Check(manifestRoot.GetProperty("phase").GetString() == "P09", "P09 reference manifest phase drifted.");

var expected = new Dictionary<int, ExpectedService>
{
    [129] = new("MARRIAGECASES", "api-129-marriage-cases.contract.json", "/marriageCasesAPIGEE", "application/x-www-form-urlencoded",
        ["civilId"], true, ["ApiKeyHeader", "Bearer"], true),
    [132] = new("ISSINGLEBASIC", "api-132-is-single-basic.contract.json", "/isSingleBasicAPIGEE", "application/x-www-form-urlencoded",
        ["civilId"], true, ["Bearer"], false),
    [130] = new("MARRIAGECOUPLELASTCASE", "api-130-marriage-couple-last-case.contract.json", "/marriageCoupleLastAPIGEE", "application/x-www-form-urlencoded",
        ["male_civilId", "female_civilId"], true, ["Bearer"], false),
    [196] = new("FAMILYJUDGMENTTEXT", "api-196-family-judgment-text.contract.json", "/familyJudgmentText", "application/json",
        ["caseNo", "type"], false, ["Bearer"], false),
    [134] = new("PROCURATIONSTATUS", "api-134-procuration-status.contract.json", "/Procuration/ProcurationStatus", "application/json",
        ["CivilClient", "CivilAgent", "year", "Number"], false, ["Bearer"], false)
};

var manifestServices = manifestRoot.GetProperty("services").EnumerateArray().ToArray();
Check(manifestServices.Length == 5, "The authoritative manifest must contain exactly five MOJ services.");
Check(manifestServices.Select(item => item.GetProperty("apiId").GetInt32()).ToHashSet().SetEquals(expected.Keys),
    "The authoritative manifest API set drifted.");
Check(manifestServices.All(item => item.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT"),
    "All five P09 UAT contracts must be proven before independent acceptance.");

var snapshotBytes = new List<byte[]>();
var contracts = new Dictionary<int, ContractView>();
foreach (var manifestService in manifestServices)
{
    var apiId = manifestService.GetProperty("apiId").GetInt32();
    var rule = expected[apiId];
    Check(manifestService.GetProperty("snapshot").GetString() == rule.SnapshotFile, $"API{apiId} snapshot filename drifted.");
    Check(manifestService.GetProperty("officialSource").GetString() == $"https://developer.api.cait.gov.kw/api/{apiId}",
        $"API{apiId} official reference drifted.");

    var snapshotPath = Path.Combine(referenceDirectory, rule.SnapshotFile);
    var bytes = await File.ReadAllBytesAsync(snapshotPath);
    snapshotBytes.Add(bytes);
    using var document = JsonDocument.Parse(bytes);
    contracts[apiId] = ParseContract(apiId, document.RootElement, rule);
}

var manifestBytes = await File.ReadAllBytesAsync(manifestPath);
var fingerprintInput = new List<byte>(manifestBytes.Length + snapshotBytes.Sum(bytes => bytes.Length));
fingerprintInput.AddRange(manifestBytes);
foreach (var bytes in snapshotBytes) fingerprintInput.AddRange(bytes);
var snapshotFingerprint = Convert.ToHexString(SHA256.HashData(fingerprintInput.ToArray())).ToLowerInvariant();

var dbOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(dbOptions);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    var seed = new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T12:00:00Z")));
    await seed.SeedAsync();

    var entities = await db.CatalogEntities.AsNoTracking().Where(item => item.Code == MojMetadataSeedService.EntityCode).ToListAsync();
    Check(entities.Count == 1, "Independent acceptance requires exactly one MOJ entity.");
    var entity = entities.Single();
    Check(entity.Active && entity.NameEn == "Ministry of Justice" && entity.NameAr == "وزارة العدل",
        "MOJ bilingual entity metadata drifted.");

    var services = await db.CatalogServices.AsNoTracking()
        .Where(item => item.EntityId == entity.Id && item.IsCurrent)
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .OrderBy(item => item.Code)
        .ToListAsync();

    Check(services.Count == 5, "Independent acceptance requires exactly five current MOJ services.");
    Check(services.Select(item => item.Code).ToHashSet(StringComparer.Ordinal).SetEquals(expected.Values.Select(item => item.ServiceCode)),
        "Seeded MOJ service set drifted from the authoritative manifest.");

    var profileIds = new HashSet<Guid>();
    foreach (var service in services)
    {
        var rule = expected.Values.Single(item => item.ServiceCode == service.Code);
        var apiId = expected.Single(item => item.Value.ServiceCode == service.Code).Key;
        var contract = contracts[apiId];

        Check(service.Active && service.Version == MojMetadataSeedService.DefinitionVersion,
            $"{service.Code} is not the current active P09 definition.");
        Check(contract.Method == "POST" && contract.RelativePath == rule.RelativePath && contract.ContentType == rule.ContentType,
            $"API{apiId} method/path/content-type contract drifted.");
        Check(contract.RequestFields.SequenceEqual(rule.RequestFields), $"API{apiId} official request field order drifted.");
        Check(contract.RequestFieldsRequired == rule.RequestFieldsRequired,
            $"API{apiId} official request requiredness classification drifted.");
        Check(contract.Authentication.ToHashSet(StringComparer.Ordinal).SetEquals(rule.Authentication),
            $"API{apiId} operation authentication drifted.");

        var seededFields = service.Fields.OrderBy(item => item.DisplayOrder).ToArray();
        Check(seededFields.Select(item => item.Key).SequenceEqual(contract.RequestFields),
            $"{service.Code} seeded request fields/order do not match the official contract.");
        Check(seededFields.All(item => item.Required == contract.RequestFieldsRequired),
            $"{service.Code} seeded requiredness does not match the official contract.");
        Check(seededFields.All(item => item.FieldType == "text" && item.Regex.Length == 0 && item.MinLength is null && item.MaxLength is null),
            $"{service.Code} invented undocumented request validation.");

        var seededMappings = service.ResultMappings.Select(item => item.SourcePath).ToHashSet(StringComparer.Ordinal);
        Check(seededMappings.SetEquals(contract.ResultMappings),
            $"{service.Code} result mappings do not exactly correspond to documented success fields.");

        Check(service.EnvironmentConfigs.Count == 2, $"{service.Code} must have exactly UAT and Production configuration rows.");
        var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);

        Check(!uat.Active && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
            $"{service.Code} UAT must remain owner-credential-gated until owner operation.");
        Check(uat.BaseUrl == contract.UatBaseUrl && uat.RelativePath == contract.RelativePath
            && uat.HttpMethod == contract.Method && uat.ContentType == contract.ContentType,
            $"{service.Code} UAT metadata does not exactly match the official contract.");
        Check(uat.AuthProfileId.HasValue, $"{service.Code} UAT is missing its isolated AuthProfile binding.");
        Check(profileIds.Add(uat.AuthProfileId!.Value), $"{service.Code} reuses another service's AuthProfile.");

        using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
        {
            var metadataRoot = metadata.RootElement;
            var hasApiKey = metadataRoot.TryGetProperty("X-GSIP-TokenApiKeyRequired", out var apiKeyFlag) && apiKeyFlag.GetBoolean();
            Check(hasApiKey == rule.TokenApiKeyRequired, $"{service.Code} token API-key applicability drifted.");
        }

        Check(!production.Active
            && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
            && string.IsNullOrEmpty(production.RelativePath)
            && string.IsNullOrEmpty(production.HttpMethod)
            && string.IsNullOrEmpty(production.ContentType)
            && production.AuthProfileId is null
            && production.LastTestStatus == "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT",
            $"{service.Code} Production is not fail-closed while its operation contract remains external.");
        Check(!string.Equals(production.BaseUrl, uat.BaseUrl, StringComparison.OrdinalIgnoreCase),
            $"{service.Code} Production silently falls back to UAT.");
    }

    Check(profileIds.Count == 5, "Five-service acceptance requires five distinct UAT AuthProfiles.");
    var profiles = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets).ToListAsync();
    Check(profiles.Count == 5, "Seed must create exactly five P09 AuthProfiles.");
    foreach (var profile in profiles)
    {
        Check(!profile.IsEnabled && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId,
            "Seeded AuthProfile must remain disabled and UAT-scoped before owner credential configuration.");
        Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
            && profile.Bindings.Single().ServiceId == profile.OwnerServiceId
            && profile.Bindings.Single().EnvironmentId == profile.OwnerEnvironmentId,
            "AuthProfile binding escaped exact Service + Environment scope or acquired implicit sharing.");
        Check(profile.Secrets.Count == 0, "P09 seed persisted a SecretRef before owner configuration.");
    }
    Check(await db.AuthProfileSecrets.CountAsync() == 0 && await db.SecretVaultEntries.CountAsync() == 0,
        "P09 seed persisted secret references/material.");

    var beforeIds = services.Select(item => item.Id).Order().ToArray();
    var beforeCounts = await CaptureCountsAsync(db);
    await seed.SeedAsync();
    var afterIds = await db.CatalogServices.AsNoTracking()
        .Where(item => item.EntityId == entity.Id && item.IsCurrent)
        .Select(item => item.Id).OrderBy(item => item).ToArrayAsync();
    Check(beforeIds.SequenceEqual(afterIds), "Metadata seed idempotency changed stable service identities.");
    Check(beforeCounts == await CaptureCountsAsync(db), "Metadata seed idempotency changed persisted object counts.");

    var ownerEdited = await db.CatalogServices.SingleAsync(item => item.Id == services[0].Id);
    ownerEdited.NameEn = "Synthetic Owner-Preserved Display";
    await db.SaveChangesAsync();
    await seed.SeedAsync();
    var preserved = await db.CatalogServices.AsNoTracking().SingleAsync(item => item.Id == ownerEdited.Id);
    Check(preserved.NameEn == "Synthetic Owner-Preserved Display",
        "Metadata seed upgrade/rerun overwrote an owner-preserved current display edit.");

    var evidenceDirectory = Path.Combine(rootDirectory, "artifacts", "p09-independent-acceptance-evidence");
    Directory.CreateDirectory(evidenceDirectory);
    var summary = new
    {
        phase = "P09",
        unit = "P09::independent-contract-security-acceptance",
        candidateSha,
        officialSnapshotFingerprintSha256 = snapshotFingerprint,
        entityCount = 1,
        serviceCount = 5,
        serviceCodes = expected.Values.Select(item => item.ServiceCode).Order().ToArray(),
        authoritativeUatContracts = "PROVEN_5",
        productionOperationContracts = "DEFERRED_EXTERNAL",
        distinctUatAuthProfiles = true,
        implicitCredentialSharing = false,
        seededSecretReferences = 0,
        metadataSeedIdempotent = true,
        ownerUpgradePreservation = true,
        liveUatCalledByCi = false,
        liveUatClassification = "OWNER_LAST_DEFERRED_EXTERNAL",
        requiredRegressions = new[]
        {
            "P06_TOKEN_CACHE",
            "P07_EXECUTION_RUNTIME_SECURITY_MASKING",
            "P08_AUTH_RUNTIME_SCOPE_SECURITY",
            "P09_TOKEN_VARIANTS"
        }
    };
    await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "acceptance-summary.json"),
        JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_INDEPENDENT_FIVE_SERVICE_CONTRACT_ACCEPTANCE=PASS");
    Console.WriteLine($"P09_OFFICIAL_SNAPSHOT_FINGERPRINT_SHA256={snapshotFingerprint}");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static ContractView ParseContract(int apiId, JsonElement root, ExpectedService rule)
{
    Check(root.GetProperty("apiId").GetInt32() == apiId, $"API{apiId} snapshot identity drifted.");
    Check(root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT", $"API{apiId} is not a proven UAT contract.");
    Check(root.GetProperty("officialSource").GetString() == $"https://developer.api.cait.gov.kw/api/{apiId}",
        $"API{apiId} snapshot official source drifted.");

    var environments = root.GetProperty("environments");
    var uat = environments.GetProperty("uat");
    var production = environments.GetProperty("production");
    Check(uat.GetProperty("status").GetString() == "PROVEN", $"API{apiId} UAT environment is not proven.");
    Check(production.GetProperty("status").GetString() == "DEFERRED_EXTERNAL" && production.GetProperty("baseUrl").ValueKind == JsonValueKind.Null,
        $"API{apiId} Production operation contract was promoted without evidence.");

    JsonElement operation;
    if (apiId == 129)
    {
        operation = root.GetProperty("operations").EnumerateArray()
            .Single(item => item.GetProperty("relativePath").GetString() == rule.RelativePath);
    }
    else
    {
        operation = root.GetProperty("operation");
    }

    var method = operation.GetProperty("method").GetString() ?? string.Empty;
    var relativePath = operation.GetProperty("relativePath").GetString() ?? string.Empty;
    var contentType = operation.GetProperty("requestContentType").GetString() ?? string.Empty;
    var requestFields = operation.GetProperty("requestFields").EnumerateArray()
        .Select(item => item.GetProperty("name").GetString() ?? string.Empty).ToArray();

    bool required;
    if (rule.RequestFieldsRequired)
    {
        required = operation.GetProperty("requestFields").EnumerateArray().All(item => item.GetProperty("required").GetBoolean());
    }
    else
    {
        required = operation.GetProperty("requestFields").EnumerateArray().Any(item =>
            item.TryGetProperty("required", out var requiredValue) && requiredValue.ValueKind == JsonValueKind.True);
        Check(!required, $"API{apiId} optional requiredness was promoted without official evidence.");
    }

    var authentication = operation.GetProperty("authentication").EnumerateArray()
        .Where(item => !item.TryGetProperty("status", out var status)
            || !string.Equals(status.GetString(), "OWNER_LAST_DEFERRED_EXTERNAL_OPERATION_APPLICABILITY", StringComparison.Ordinal))
        .Select(item => item.GetProperty("scheme").GetString() ?? string.Empty)
        .ToArray();

    var officialMappings = operation.GetProperty("responseFields").EnumerateArray()
        .Select(item => item.GetProperty("path").GetString() ?? string.Empty)
        .Where(path => !path.StartsWith("errorDTO.", StringComparison.Ordinal))
        .ToHashSet(StringComparer.Ordinal);

    return new ContractView(
        uat.GetProperty("baseUrl").GetString() ?? string.Empty,
        method,
        relativePath,
        contentType,
        requestFields,
        rule.RequestFieldsRequired ? required : false,
        authentication,
        officialMappings);
}

static async Task<SeedCounts> CaptureCountsAsync(GsipDbContext db) => new(
    await db.CatalogEntities.CountAsync(),
    await db.CatalogServices.CountAsync(),
    await db.ServiceEnvironmentConfigs.CountAsync(),
    await db.ServiceFieldDefinitions.CountAsync(),
    await db.ResultMappingDefinitions.CountAsync(),
    await db.AuthProfiles.CountAsync(),
    await db.AuthProfileBindings.CountAsync(),
    await db.AuthProfileSecrets.CountAsync(),
    await db.SecretVaultEntries.CountAsync());

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record ExpectedService(
    string ServiceCode,
    string SnapshotFile,
    string RelativePath,
    string ContentType,
    IReadOnlyList<string> RequestFields,
    bool RequestFieldsRequired,
    IReadOnlyList<string> Authentication,
    bool TokenApiKeyRequired);

sealed record ContractView(
    string UatBaseUrl,
    string Method,
    string RelativePath,
    string ContentType,
    IReadOnlyList<string> RequestFields,
    bool RequestFieldsRequired,
    IReadOnlyList<string> Authentication,
    IReadOnlySet<string> ResultMappings);

sealed record SeedCounts(
    int Entities,
    int Services,
    int EnvironmentConfigs,
    int Fields,
    int ResultMappings,
    int AuthProfiles,
    int AuthBindings,
    int AuthSecrets,
    int VaultEntries);

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
