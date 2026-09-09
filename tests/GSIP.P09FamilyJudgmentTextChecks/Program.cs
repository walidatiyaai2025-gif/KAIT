using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_JUDGMENT_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_JUDGMENT_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:22:00Z"))).SeedAsync();

    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("docs", "moj-api-reference", "p09", "api-196-family-judgment-text.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 196 && root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT",
        "API196 authoritative UAT contract identity/status drifted.");
    var uatEvidence = root.GetProperty("environments").GetProperty("uat");
    Check(uatEvidence.GetProperty("baseUrl").GetString() == MojMetadataSeedService.VerdictUatBaseUrl
        && uatEvidence.GetProperty("targetType").GetString() == "MOCK",
        "API196 documented UAT mock/base contract drifted.");
    Check(root.GetProperty("environments").GetProperty("production").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "API196 Production contract must remain externally deferred.");

    var token = root.GetProperty("tokenContract");
    Check(token.GetProperty("method").GetString() == "POST"
        && token.GetProperty("relativePath").GetString() == "/token"
        && token.GetProperty("requestContentType").GetString() == "application/json"
        && token.GetProperty("tokenResponsePath").GetString() == "token",
        "API196 token contract drifted.");
    Check(token.GetProperty("requestFields").EnumerateArray().Select(x => x.GetProperty("name").GetString()).SequenceEqual(["username", "password"]),
        "API196 token wire fields drifted.");
    Check(token.GetProperty("responses").EnumerateArray().Select(x => x.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 400, 401, 500]),
        "API196 token response statuses drifted.");

    var operation = root.GetProperty("operation");
    Check(operation.GetProperty("method").GetString() == "POST"
        && operation.GetProperty("relativePath").GetString() == "/familyJudgmentText"
        && operation.GetProperty("requestContentType").GetString() == "application/json",
        "API196 target transport contract drifted.");
    Check(operation.GetProperty("requestFields").EnumerateArray().Select(x => x.GetProperty("name").GetString()).SequenceEqual(["caseNo", "type"]),
        "API196 request wire fields drifted.");
    Check(operation.GetProperty("responses").EnumerateArray().Select(x => x.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 400, 401, 403, 404, 500]),
        "API196 documented target statuses drifted.");
    Check(operation.GetProperty("responseFields").EnumerateArray().Single().GetProperty("path").GetString() == "data.VRDESC",
        "API196 judgment result path drifted.");

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "FAMILYJUDGMENTTEXT" && item.IsCurrent);
    Check(service.Version == 2 && service.Active, "API196 current metadata is not v2.");
    Check(service.Fields.Select(item => item.Key).SequenceEqual(["caseNo", "type"]), "API196 seeded request fields drifted.");
    Check(service.Fields.All(item => !item.Required), "API196 requiredness was invented although supplied schema did not mark fields required.");
    Check(service.Fields.Single(item => item.Key == "caseNo").Sensitive
        && !service.Fields.Single(item => item.Key == "type").Sensitive,
        "API196 request sensitivity metadata drifted.");
    var judgmentMapping = service.ResultMappings.Single(item => item.SourcePath == "data.VRDESC");
    Check(judgmentMapping.Sensitive, "API196 judgment text must remain sensitive in result mapping.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && uat.BaseUrl == MojMetadataSeedService.VerdictUatBaseUrl
        && uat.RelativePath == "/familyJudgmentText"
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/json"
        && uat.AuthProfileId.HasValue
        && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
        "API196 UAT metadata must be exact-contract and credential-gated.");
    using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
    {
        var m = metadata.RootElement;
        Check(m.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == "/token", "API196 token path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == "application/json", "API196 token request type drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == "token", "API196 token response path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenUsernameFieldMetadataKey).GetString() == "username"
            && m.GetProperty(MojMetadataSeedService.TokenPasswordFieldMetadataKey).GetString() == "password",
            "API196 token credential wire names drifted.");
        Check(!m.TryGetProperty("X-GSIP-TokenApiKeyRequired", out _),
            "API196 operation-level x-api-key applicability was promoted beyond supplied evidence.");
        Check(!m.TryGetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey, out _),
            "API196 invented a numeric token TTL.");
    }
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null,
        "API196 Production must remain prefix-only/fail-closed without UAT fallback.");

    var profile = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets)
        .SingleAsync(item => item.Id == uat.AuthProfileId!.Value);
    Check(profile.OwnerServiceId == service.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API196 AuthProfile scope/type/state drifted.");
    Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
        && profile.Bindings.Single().ServiceId == service.Id
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId,
        "API196 AuthProfile acquired implicit sharing or wrong scope.");
    Check(profile.Secrets.Count == 0, "API196 seed must not persist secret references.");

    var directory = Path.Combine("artifacts", "p09-family-judgment-text-evidence");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::family-judgment-text-service",
        apiId = 196,
        uatContract = "PROVEN",
        uatTarget = "MOCK",
        mockSuccessMeansRealBackendValidated = false,
        exactRequestFields = true,
        responseStatuses = new[] { 200, 400, 401, 403, 404, 500 },
        judgmentTextSensitive = true,
        operationApiKeyApplicability = "OWNER_LAST_DEFERRED_EXTERNAL",
        productionContract = "DEFERRED_EXTERNAL",
        ownerCredentialsRequired = true,
        liveEntityBackendCalledByCi = false
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_FAMILY_JUDGMENT_TEXT_ACCEPTANCE=PASS");
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
