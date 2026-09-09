using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_PROCURATION_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_PROCURATION_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    await new MojMetadataSeedService(db, new FixedClock(DateTimeOffset.Parse("2026-09-09T11:24:00Z"))).SeedAsync();

    using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("docs", "moj-api-reference", "p09", "api-134-procuration-status.contract.json")));
    var root = contract.RootElement;
    Check(root.GetProperty("apiId").GetInt32() == 134 && root.GetProperty("captureStatus").GetString() == "PROVEN_UAT_CONTRACT",
        "API134 authoritative UAT contract identity/status drifted.");
    var uatEvidence = root.GetProperty("environments").GetProperty("uat");
    Check(uatEvidence.GetProperty("baseUrl").GetString() == MojMetadataSeedService.ProcurationUatBaseUrl
        && uatEvidence.GetProperty("targetType").GetString() == "MOCK",
        "API134 documented UAT mock/base contract drifted.");
    Check(root.GetProperty("environments").GetProperty("production").GetProperty("status").GetString() == "DEFERRED_EXTERNAL",
        "API134 Production contract must remain externally deferred.");

    var token = root.GetProperty("tokenContract");
    Check(token.GetProperty("method").GetString() == "POST"
        && token.GetProperty("relativePath").GetString() == "/Authenticate/Token"
        && token.GetProperty("requestContentType").GetString() == "application/json"
        && token.GetProperty("tokenResponsePath").GetString() == "token",
        "API134 token transport/response contract drifted.");
    var tokenFields = token.GetProperty("requestFields").EnumerateArray().ToArray();
    Check(tokenFields.Select(x => x.GetProperty("name").GetString()).SequenceEqual(["UserName", "Password", "Geha"]),
        "API134 token wire field casing drifted.");
    Check(tokenFields.Select(x => x.GetProperty("canonicalSecretName").GetString()).SequenceEqual(["username", "password", "geha"]),
        "API134 canonical secret mapping drifted.");
    Check(token.GetProperty("tokenLifetime").GetProperty("status").GetString() == "PROVEN"
        && token.GetProperty("tokenLifetime").GetProperty("seconds").GetInt32() == 21600,
        "API134 documented six-hour token lifetime drifted.");
    Check(token.GetProperty("responses").EnumerateArray().Select(x => x.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 401]),
        "API134 token response statuses drifted.");

    var operation = root.GetProperty("operation");
    Check(operation.GetProperty("method").GetString() == "POST"
        && operation.GetProperty("relativePath").GetString() == "/Procuration/ProcurationStatus"
        && operation.GetProperty("requestContentType").GetString() == "application/json",
        "API134 target transport contract drifted.");
    Check(operation.GetProperty("requestFields").EnumerateArray().Select(x => x.GetProperty("name").GetString())
        .SequenceEqual(["CivilClient", "CivilAgent", "year", "Number"]),
        "API134 request wire field casing/order drifted.");
    Check(operation.GetProperty("responses").EnumerateArray().Select(x => x.GetProperty("statusCode").GetInt32()).SequenceEqual([200, 400, 401, 404]),
        "API134 documented target statuses drifted.");
    Check(operation.GetProperty("responseFields").EnumerateArray().Select(x => x.GetProperty("path").GetString())
        .SequenceEqual(["Status", "Message", "Data.type", "Data.Status"]),
        "API134 response mappings drifted.");

    var service = await db.CatalogServices.AsNoTracking()
        .Include(item => item.EnvironmentConfigs)
        .Include(item => item.Fields)
        .Include(item => item.ResultMappings)
        .SingleAsync(item => item.Code == "PROCURATIONSTATUS" && item.IsCurrent);
    Check(service.Version == 2 && service.Active, "API134 current metadata is not v2.");
    Check(service.Fields.Select(item => item.Key).SequenceEqual(["CivilClient", "CivilAgent", "year", "Number"]),
        "API134 seeded request fields drifted.");
    Check(service.Fields.All(item => !item.Required),
        "API134 requiredness was invented although supplied schema did not mark fields required.");
    Check(service.Fields.Single(item => item.Key == "CivilClient").Sensitive
        && service.Fields.Single(item => item.Key == "CivilAgent").Sensitive
        && !service.Fields.Single(item => item.Key == "year").Sensitive
        && service.Fields.Single(item => item.Key == "Number").Sensitive,
        "API134 request sensitivity metadata drifted.");
    Check(new[] { "Status", "Message", "Data.type", "Data.Status" }.All(path => service.ResultMappings.Any(mapping => mapping.SourcePath == path)),
        "API134 official result mappings are incomplete.");

    var uat = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(!uat.Active
        && uat.BaseUrl == MojMetadataSeedService.ProcurationUatBaseUrl
        && uat.RelativePath == "/Procuration/ProcurationStatus"
        && uat.HttpMethod == "POST"
        && uat.ContentType == "application/json"
        && uat.AuthProfileId.HasValue
        && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
        "API134 UAT metadata must be exact-contract and credential-gated.");
    using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
    {
        var m = metadata.RootElement;
        Check(m.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == "/Authenticate/Token", "API134 token path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == "application/json", "API134 token request type drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == "token", "API134 token response path drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenUsernameFieldMetadataKey).GetString() == "UserName"
            && m.GetProperty(MojMetadataSeedService.TokenPasswordFieldMetadataKey).GetString() == "Password"
            && m.GetProperty(MojMetadataSeedService.TokenGehaFieldMetadataKey).GetString() == "Geha",
            "API134 token credential wire casing drifted.");
        Check(m.GetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey).GetInt32() == 21600,
            "API134 documented token TTL was not carried into metadata.");
        Check(!m.TryGetProperty("X-GSIP-TokenApiKeyRequired", out _),
            "API134 operation-level x-api-key applicability was promoted beyond supplied evidence.");
    }
    Check(!production.Active
        && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
        && string.IsNullOrEmpty(production.RelativePath)
        && string.IsNullOrEmpty(production.HttpMethod)
        && string.IsNullOrEmpty(production.ContentType)
        && production.AuthProfileId is null,
        "API134 Production must remain prefix-only/fail-closed without UAT fallback.");

    var profile = await db.AuthProfiles.AsNoTracking().Include(item => item.Bindings).Include(item => item.Secrets)
        .SingleAsync(item => item.Id == uat.AuthProfileId!.Value);
    Check(profile.OwnerServiceId == service.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API134 AuthProfile scope/type/state drifted.");
    Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared
        && profile.Bindings.Single().ServiceId == service.Id
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId,
        "API134 AuthProfile acquired implicit sharing or wrong scope.");
    Check(profile.Secrets.Count == 0, "API134 seed must not persist secret references.");

    var directory = Path.Combine("artifacts", "p09-procuration-status-evidence");
    Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
    {
        phase = "P09",
        unit = "P09::procuration-status-service",
        apiId = 134,
        uatContract = "PROVEN",
        uatTarget = "MOCK",
        mockSuccessMeansRealBackendValidated = false,
        exactTokenFields = new[] { "UserName", "Password", "Geha" },
        documentedTokenTtlSeconds = 21600,
        exactTargetFields = new[] { "CivilClient", "CivilAgent", "year", "Number" },
        responseStatuses = new[] { 200, 400, 401, 404 },
        operationApiKeyApplicability = "OWNER_LAST_DEFERRED_EXTERNAL",
        productionContract = "DEFERRED_EXTERNAL",
        ownerCredentialsRequired = true,
        liveEntityBackendCalledByCi = false
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("P09_PROCURATION_STATUS_ACCEPTANCE=PASS");
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
