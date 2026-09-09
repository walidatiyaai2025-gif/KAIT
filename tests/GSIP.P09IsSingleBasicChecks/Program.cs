using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_SINGLE_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_SINGLE_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:28:00Z"))).SeedAsync();

    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("docs", "moj-api-reference", "p09", "api-132-is-single-basic.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 132 && root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT",
        "API132 authoritative UAT contract identity/status drifted.");
    Check(root.GetProperty("environments").GetProperty("uat").GetProperty("baseUrl").GetString() == MojMetadataSeedService.MarriageUatBaseUrl,
        "API132 UAT base URL drifted.");
    Check(root.GetProperty("environments").GetProperty("production").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "API132 Production must remain externally deferred.");

    var token = root.GetProperty("tokenContract");
    Check(token.GetProperty("method").GetString() == "POST"
        && token.GetProperty("relativePath").GetString() == "/genToken"
        && token.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded"
        && token.GetProperty("tokenResponsePath").GetString() == "data",
        "API132 common Marriage token contract drifted.");
    Check(token.GetProperty("requestFields").EnumerateArray().Select(item => item.GetProperty("name").GetString()).SequenceEqual(["username", "password"]),
        "API132 token credential fields drifted.");

    var operation = root.GetProperty("operation");
    Check(operation.GetProperty("method").GetString() == "POST"
        && operation.GetProperty("relativePath").GetString() == "/isSingleBasicAPIGEE"
        && operation.GetProperty("requestContentType").GetString() == "application/x-www-form-urlencoded",
        "API132 target transport contract drifted.");
    var field = operation.GetProperty("requestFields").EnumerateArray().Single();
    Check(field.GetProperty("name").GetString() == "civilId"
        && field.GetProperty("type").GetString() == "string"
        && field.GetProperty("required").GetBoolean(),
        "API132 civilId contract drifted.");
    Check(operation.GetProperty("responses").EnumerateArray().Select(item => item.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 401, 404]),
        "API132 documented response statuses drifted.");
    Check(operation.GetProperty("authentication").EnumerateArray().Single().GetProperty("headerName").GetString() == "Authorization",
        "API132 proven Bearer requirement drifted.");

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "ISSINGLEBASIC" && item.IsCurrent);
    Check(service.Version == 2 && service.Active, "API132 current metadata is not v2.");
    var civilId = service.Fields.Single();
    Check(civilId.Key == "civilId" && civilId.Required && civilId.Sensitive && civilId.Masking == "Last4",
        "API132 seeded civilId metadata drifted.");
    Check(civilId.Regex.Length == 0 && civilId.MinLength is null && civilId.MaxLength is null,
        "API132 invented undocumented civilId validation.");
    Check(new[] { "data[0].FENATI_NAME", "data[0].NATI_NAME", "data[0].maritalStatus", "data[0].marriageFree", "data[0].marriedCouple", "errorDTO.civilID" }
        .All(path => service.ResultMappings.Any(mapping => mapping.SourcePath == path)),
        "API132 official result mappings are incomplete.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && uat.BaseUrl == MojMetadataSeedService.MarriageUatBaseUrl
        && uat.RelativePath == "/isSingleBasicAPIGEE"
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/x-www-form-urlencoded"
        && uat.AuthProfileId.HasValue
        && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
        "API132 UAT metadata must be exact-contract and credential-gated.");
    using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
    {
        var m = metadata.RootElement;
        Check(m.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == "/genToken", "API132 token path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == "application/x-www-form-urlencoded", "API132 token request type drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == "data", "API132 token response path drifted.");
        Check(!m.TryGetProperty("X-GSIP-TokenApiKeyRequired", out _),
            "API132 exact operation-level x-api-key applicability was inferred from another service.");
        Check(!m.TryGetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey, out _),
            "API132 invented a numeric token TTL.");
    }
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null,
        "API132 Production must remain prefix-only/fail-closed without UAT fallback.");

    var profile = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets)
        .SingleAsync(item => item.Id == uat.AuthProfileId!.Value);
    Check(profile.OwnerServiceId == service.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API132 AuthProfile scope/type/state drifted.");
    Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
        && profile.Bindings.Single().ServiceId == service.Id
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId,
        "API132 AuthProfile acquired implicit sharing or wrong scope.");
    Check(profile.Secrets.Count == 0, "API132 seed must not persist secret references.");

    var directory = Path.Combine("artifacts", "p09-is-single-basic-evidence");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::is-single-basic-service",
        apiId = 132,
        uatContract = "PROVEN",
        exactTargetField = "civilId",
        targetResponseStatuses = new[] { 200, 401, 404 },
        bearer = "PROVEN",
        operationApiKeyApplicability = "NOT_PROMOTED_FROM_OTHER_MARRIAGE_API",
        productionContract = "DEFERRED_EXTERNAL",
        ownerCredentialsRequired = true,
        liveUatCalledByCi = false
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_IS_SINGLE_BASIC_ACCEPTANCE=PASS");
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
