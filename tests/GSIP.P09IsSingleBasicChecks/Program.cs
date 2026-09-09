using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_IS_SINGLE_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P09_IS_SINGLE_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    var seed = new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:00:00Z")));
    await seed.SeedAsync();

    var mojEntities = await db.CatalogEntities.AsNoTracking().Where(x => x.Code == MojMetadataSeedService.EntityCode).ToListAsync();
    Check(mojEntities.Count == 1, "Expected exactly one canonical MOJ entity.");

    var services = await db.CatalogServices.AsNoTracking()
        .Where(x => x.EntityId == mojEntities[0].Id && x.IsCurrent)
        .Include(x => x.EnvironmentConfigs)
        .Include(x => x.Fields)
        .Include(x => x.ResultMappings)
        .OrderBy(x => x.Code)
        .ToListAsync();
    Check(services.Count == 5, "Expected exactly five canonical MOJ services.");
    Check(services.Select(x => x.Code).OrderBy(x => x).SequenceEqual(MojMetadataSeedService.CanonicalServiceCodes.OrderBy(x => x)),
        "Canonical MOJ service set drifted.");

    var service = services.Single(x => x.Code == "ISSINGLEBASIC");
    ValidateOfficialDeferredSnapshot(service);
    await ValidateCredentialIsolationAsync(db, service);

    var stableId = service.Id;
    var stableConfigIds = service.EnvironmentConfigs.OrderBy(x => x.EnvironmentId).Select(x => x.Id).ToArray();
    await seed.SeedAsync();
    var after = await db.CatalogServices.AsNoTracking().Where(x => x.EntityId == mojEntities[0].Id && x.IsCurrent).ToListAsync();
    Check(after.Count == 5 && after.Single(x => x.Code == "ISSINGLEBASIC").Id == stableId,
        "API132 seed was not idempotent/stable.");
    var afterConfigIds = await db.ServiceEnvironmentConfigs.AsNoTracking()
        .Where(x => x.ServiceId == stableId)
        .OrderBy(x => x.EnvironmentId)
        .Select(x => x.Id)
        .ToArrayAsync();
    Check(stableConfigIds.SequenceEqual(afterConfigIds), "API132 environment identities changed after reseed.");

    await WriteEvidenceAsync(service);
    Console.WriteLine("P09_IS_SINGLE_BASIC_ACCEPTANCE=PASS_FAIL_CLOSED_SCOPE");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static void ValidateOfficialDeferredSnapshot(CatalogService service)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "docs", "moj-api-reference", "p09", "api-132-is-single-basic.contract.json");
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 132, "Wrong API132 authoritative snapshot.");
    Check(root.GetProperty("serviceName").GetString() == "Is Single Basic Service", "API132 service-name snapshot drifted.");
    Check(root.GetProperty("captureStatus").GetString() == "DEFERRED_EXTERNAL", "API132 operation contract was promoted without official evidence.");
    Check(root.GetProperty("evidence").GetProperty("classification").GetString() == "OFFICIAL_CAIT_SPECIFICATION_NOT_RETRIEVABLE_WITHOUT_SIGN_IN",
        "API132 evidence classification changed unexpectedly.");

    var items = root.GetProperty("contractItems");
    foreach (var item in items.EnumerateObject())
        Check(item.Value.GetProperty("status").GetString() == "DEFERRED_EXTERNAL", $"API132 contract item {item.Name} was promoted without official evidence.");

    Check(items.GetProperty("uatServerBaseUrl").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API132 invented UAT server contract.");
    Check(items.GetProperty("productionServerBaseUrl").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API132 invented Production server contract.");
    Check(items.GetProperty("httpMethod").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API132 invented HTTP method.");
    Check(items.GetProperty("relativePath").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API132 invented relative path.");
    Check(items.GetProperty("requestContentType").GetProperty("value").ValueKind == JsonValueKind.Null,
        "API132 invented content type.");
    Check(items.GetProperty("requestFields").GetProperty("fields").GetArrayLength() == 0,
        "API132 invented request fields.");
    Check(items.GetProperty("operationAuthentication").GetProperty("requirements").GetArrayLength() == 0,
        "API132 invented operation authentication.");
    Check(items.GetProperty("successStatusesAndSchema").GetProperty("responses").GetArrayLength() == 0,
        "API132 invented success schemas.");
    Check(items.GetProperty("errorStatusesAndSchemas").GetProperty("responses").GetArrayLength() == 0,
        "API132 invented error schemas.");
    Check(items.GetProperty("responseFields").GetProperty("fields").GetArrayLength() == 0,
        "API132 invented response fields.");
    Check(items.GetProperty("resultMappingCandidates").GetProperty("fields").GetArrayLength() == 0,
        "API132 invented result mappings.");

    Check(service.Fields.Count == 0, "API132 seed contains unproven request fields.");
    Check(service.ResultMappings.Count == 0, "API132 seed contains unproven result mappings.");
    Check(service.EnvironmentConfigs.Count == 2, "API132 must retain independent UAT and Production rows.");

    var uat = service.EnvironmentConfigs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active && string.IsNullOrEmpty(uat.BaseUrl) && string.IsNullOrEmpty(uat.RelativePath)
        && string.IsNullOrEmpty(uat.HttpMethod) && string.IsNullOrEmpty(uat.ContentType)
        && uat.AuthProfileId is null && uat.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API132 UAT must remain disabled and non-executable.");
    Check(!production.Active && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath) && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType) && production.AuthProfileId is null
        && production.LastTestStatus == "DEFERRED_EXTERNAL_CONTRACT",
        "API132 Production must remain gateway-prefix-only, isolated, and non-executable.");
    Check(!string.Equals(uat.BaseUrl, production.BaseUrl, StringComparison.OrdinalIgnoreCase),
        "API132 UAT/Production configuration collapsed into a fallback path.");
}

static async Task ValidateCredentialIsolationAsync(GsipDbContext db, CatalogService service)
{
    Check(await db.AuthProfiles.AsNoTracking().CountAsync(x => x.OwnerServiceId == service.Id) == 0,
        "API132 acquired an AuthProfile without official operation auth evidence.");
    Check(await db.AuthProfileBindings.AsNoTracking().CountAsync(x => x.ServiceId == service.Id) == 0,
        "API132 acquired an implicit/shared credential binding.");
    Check(await db.AuthProfileSecrets.AsNoTracking().AnyAsync(x => x.AuthProfile != null && x.AuthProfile.OwnerServiceId == service.Id) == false,
        "API132 acquired a secret reference without official auth evidence.");

    var otherServiceIds = await db.CatalogServices.AsNoTracking().Where(x => x.Id != service.Id).Select(x => x.Id).ToListAsync();
    var foreignBindings = await db.AuthProfileBindings.AsNoTracking().Where(x => otherServiceIds.Contains(x.ServiceId)).ToListAsync();
    Check(foreignBindings.All(x => x.ServiceId != service.Id), "Cross-service credential binding leaked into API132.");
}

static async Task WriteEvidenceAsync(CatalogService service)
{
    var directory = Path.Combine("artifacts", "p09-is-single-basic-evidence");
    Directory.CreateDirectory(directory);
    var head = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "LOCAL_SYNTHETIC_RUN";
    var payload = JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::is-single-basic-service",
        apiId = 132,
        service = service.Code,
        candidateHead = head,
        automatedScope = "PASS_FAIL_CLOSED_READINESS",
        officialOperationContract = "DEFERRED_EXTERNAL_NOT_PASS",
        exactMethodPathContentType = "DEFERRED_EXTERNAL_NOT_PASS",
        exactRequestFieldsValidation = "DEFERRED_EXTERNAL_NOT_PASS",
        exactOperationAuthentication = "DEFERRED_EXTERNAL_NOT_PASS",
        successErrorSchemas = "DEFERRED_EXTERNAL_NOT_PASS",
        exactResultMappings = "DEFERRED_EXTERNAL_NOT_PASS",
        seededRequestFields = 0,
        seededResultMappings = 0,
        seededAuthProfile = false,
        uatExecutable = false,
        productionExecutable = false,
        productionToUatFallback = false,
        crossServiceCredentialReuse = false,
        liveCaitCalledByCi = false,
        containsPersonalPayload = false
    }, new JsonSerializerOptions { WriteIndented = true });
    var manifest = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(manifest, payload);
    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.sha256"), $"{digest}  manifest.json{Environment.NewLine}");
    Check(!payload.Contains("civilId", StringComparison.OrdinalIgnoreCase)
        && !payload.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
        && !payload.Contains("x-api-key:", StringComparison.OrdinalIgnoreCase)
        && !payload.Contains("sr1_", StringComparison.Ordinal),
        "API132 evidence contains forbidden credential/personal/reference material.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset value) : ISystemClock
{
    public DateTimeOffset UtcNow => value;
}
