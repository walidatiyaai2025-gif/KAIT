using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_COUPLE_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_COUPLE_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:20:00Z"))).SeedAsync();

    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("docs", "moj-api-reference", "p09", "api-130-marriage-couple-last-case.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 130, "Wrong API130 snapshot.");
    Check(root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT", "API130 UAT contract is not proven.");
    Check(root.GetProperty("environments").GetProperty("uat").GetProperty("baseUrl").GetString() == MojMetadataSeedService.MarriageUatBaseUrl,
        "API130 UAT base URL drifted.");
    Check(root.GetProperty("environments").GetProperty("production").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "API130 Production must remain externally deferred.");

    var operation = root.GetProperty("operation");
    Check(operation.GetProperty("method").GetString() == "POST"
        && operation.GetProperty("relativePath").GetString() == "/marriageCoupleLastAPIGEE"
        && operation.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded",
        "API130 operation transport contract drifted.");
    var fields = operation.GetProperty("requestFields").EnumerateArray().ToArray();
    Check(fields.Length == 2
        && fields[0].GetProperty("name").GetString() == "male_civilId"
        && fields[1].GetProperty("name").GetString() == "female_civilId"
        && fields.All(field => field.GetProperty("type").GetString() == "string" && field.GetProperty("required").GetBoolean()),
        "API130 exact request field names/types/requiredness drifted.");
    Check(operation.GetProperty("responses").EnumerateArray().Select(item => item.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 400, 401, 404]),
        "API130 documented response status set drifted.");

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "MARRIAGECOUPLELASTCASE" && item.IsCurrent);
    Check(service.Version == 2 && service.Active, "API130 current metadata version is not v2.");
    Check(service.Fields.Select(item => item.Key).SequenceEqual(["male_civilId", "female_civilId"]),
        "API130 seed request keys/order drifted.");
    Check(service.Fields.All(item => item.Required && item.Sensitive && item.Masking == "Last4"),
        "API130 Civil ID request fields must be required and sensitive.");
    var expectedMappings = new[] { "data[0].FENATI_NAME", "data[0].NATI_NAME", "data[0].NO_DAT", "data[0].maritalStatus", "data[0].marriedCouple" };
    Check(expectedMappings.All(path => service.ResultMappings.Any(mapping => mapping.SourcePath == path)),
        "API130 official result mappings are incomplete.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && uat.BaseUrl == MojMetadataSeedService.MarriageUatBaseUrl
        && uat.RelativePath == "/marriageCoupleLastAPIGEE"
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/x-www-form-urlencoded"
        && uat.AuthProfileId.HasValue
        && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
        "API130 UAT metadata is not exact-contract/credential-gated.");
    using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
    {
        Check(metadata.RootElement.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == "/genToken", "API130 token path drifted.");
        Check(metadata.RootElement.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == "application/x-www-form-urlencoded", "API130 token content type drifted.");
        Check(metadata.RootElement.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == "data", "API130 token response path drifted.");
        Check(!metadata.RootElement.TryGetProperty("X-GSIP-TokenApiKeyRequired", out _),
            "API130 token x-api-key requirement was invented without exact operation-level proof.");
    }
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null,
        "API130 Production must remain fail-closed without UAT fallback.");

    var profile = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets)
        .SingleAsync(item => item.Id == uat.AuthProfileId!.Value);
    Check(profile.OwnerServiceId == service.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API130 AuthProfile scope/type/state drifted.");
    Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
        && profile.Bindings.Single().ServiceId == service.Id
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId,
        "API130 AuthProfile acquired implicit sharing or wrong scope.");
    Check(profile.Secrets.Count == 0, "API130 seed must not persist secret references.");

    var directory = Path.Combine("artifacts", "p09-marriage-couple-last-case-evidence");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::marriage-couple-last-case-service",
        apiId = 130,
        uatContract = "PROVEN",
        exactRequestFields = true,
        responseStatuses = new[] { 200, 400, 401, 404 },
        productionContract = "DEFERRED_EXTERNAL",
        ownerCredentialsRequired = true,
        implicitCredentialSharing = false,
        liveUatCalledByCi = false
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_MARRIAGE_COUPLE_LAST_CASE_ACCEPTANCE=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
