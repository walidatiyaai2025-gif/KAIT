using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_MARRIAGE_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_MARRIAGE_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:26:00Z"))).SeedAsync();

    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("docs", "moj-api-reference", "p09", "api-129-marriage-cases.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 129 && root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT",
        "API129 authoritative UAT contract identity/status drifted.");
    Check(root.GetProperty("environments").GetProperty("uat").GetProperty("baseUrl").GetString() == MojMetadataSeedService.MarriageUatBaseUrl,
        "API129 UAT base URL drifted.");
    Check(root.GetProperty("environments").GetProperty("production").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "API129 Production must remain externally deferred.");

    var operations = root.GetProperty("operations").EnumerateArray().ToArray();
    var token = operations.Single(item => item.GetProperty("relativePath").GetString() == "/genToken");
    var target = operations.Single(item => item.GetProperty("relativePath").GetString() == "/marriageCasesAPIGEE");
    Check(token.GetProperty("method").GetString() == "POST"
        && token.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded"
        && token.GetProperty("tokenResponsePath").GetString() == "data",
        "API129 token transport/response contract drifted.");
    Check(token.GetProperty("requestFields").EnumerateArray().Select(item => item.GetProperty("name").GetString()).SequenceEqual(["username", "password"]),
        "API129 token credential fields drifted.");
    Check(token.GetProperty("authentication").EnumerateArray().Single().GetProperty("headerName").GetString() == "x-api-key",
        "API129 proven /genToken x-api-key requirement drifted.");
    Check(token.GetProperty("responses").EnumerateArray().Select(item => item.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 401]),
        "API129 token documented statuses drifted.");

    Check(target.GetProperty("method").GetString() == "POST"
        && target.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded",
        "API129 target transport contract drifted.");
    var requestField = target.GetProperty("requestFields").EnumerateArray().Single();
    Check(requestField.GetProperty("name").GetString() == "civilId"
        && requestField.GetProperty("type").GetString() == "string"
        && requestField.GetProperty("required").GetBoolean(),
        "API129 civilId contract drifted.");
    Check(target.GetProperty("responses").EnumerateArray().Select(item => item.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 400, 401, 404]),
        "API129 documented target statuses drifted.");
    var targetAuth = target.GetProperty("authentication").EnumerateArray().Select(item => item.GetProperty("headerName").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Check(targetAuth.SetEquals(["x-api-key", "Authorization"]), "API129 proven target composite auth drifted.");

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "MARRIAGECASES" && item.IsCurrent);
    Check(service.Version == 2 && service.Active, "API129 current metadata is not v2.");
    var civilId = service.Fields.Single();
    Check(civilId.Key == "civilId" && civilId.Required && civilId.Sensitive && civilId.Masking == "Last4",
        "API129 seeded civilId field drifted.");
    Check(civilId.Regex.Length == 0 && civilId.MinLength is null && civilId.MaxLength is null,
        "API129 invented undocumented civilId validation.");
    var requiredMappings = new[] { "data[0].CIVILID", "data[0].CONTRACT_DATE", "data[0].CONTRACT_NO", "data[0].CONTRACT_TYPE", "data[0].FEMALE_NAME", "data[0].FEM_CIVILID", "data[0].MALE_NAME", "errorDTO.civilID" };
    Check(requiredMappings.All(path => service.ResultMappings.Any(mapping => mapping.SourcePath == path)),
        "API129 official result mappings are incomplete.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && uat.BaseUrl == MojMetadataSeedService.MarriageUatBaseUrl
        && uat.RelativePath == "/marriageCasesAPIGEE"
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/x-www-form-urlencoded"
        && uat.AuthProfileId.HasValue
        && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
        "API129 UAT metadata must be exact-contract and credential-gated.");
    using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
    {
        var m = metadata.RootElement;
        Check(m.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == "/genToken", "API129 token path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == "application/x-www-form-urlencoded", "API129 token request type drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == "data", "API129 token response path drifted.");
        Check(m.GetProperty("X-GSIP-TokenApiKeyRequired").GetBoolean(),
            "API129 proven /genToken x-api-key requirement is missing from runtime metadata.");
        Check(!m.TryGetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey, out _),
            "API129 invented a numeric token TTL.");
    }
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null,
        "API129 Production must remain prefix-only/fail-closed without UAT fallback.");

    var profile = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets)
        .SingleAsync(item => item.Id == uat.AuthProfileId!.Value);
    Check(profile.OwnerServiceId == service.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API129 AuthProfile scope/type/state drifted.");
    Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
        && profile.Bindings.Single().ServiceId == service.Id
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId,
        "API129 AuthProfile acquired implicit sharing or wrong scope.");
    Check(profile.Secrets.Count == 0, "API129 seed must not persist secret references.");

    var directory = Path.Combine("artifacts", "p09-marriage-cases-evidence");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::marriage-cases-service",
        apiId = 129,
        uatContract = "PROVEN",
        tokenApiKey = "PROVEN",
        targetCompositeAuth = "PROVEN_X_API_KEY_PLUS_BEARER",
        exactTargetField = "civilId",
        targetResponseStatuses = new[] { 200, 400, 401, 404 },
        productionContract = "DEFERRED_EXTERNAL",
        ownerCredentialsRequired = true,
        liveUatCalledByCi = false
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_MARRIAGE_CASES_ACCEPTANCE=PASS");
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
