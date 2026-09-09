using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P09_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P09_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-09T11:12:00Z"));
    var seed = new MojMetadataSeedService(db, clock);

    Assert(MojMetadataSeedService.DefinitionVersion == 2, "P09 proven-contract seed must be definition version 2.");
    await seed.SeedAsync();

    var entity = await db.CatalogEntities.AsNoTracking().SingleAsync(item => item.Code == MojMetadataSeedService.EntityCode);
    Assert(entity.NameAr == "وزارة العدل" && entity.NameEn == "Ministry of Justice" && entity.DisplayOrder == 1 && entity.Active,
        "MOJ canonical bilingual entity metadata is invalid.");

    var services = await LoadServicesAsync(db, entity.Id);
    Assert(services.Count == 5, "P09 must expose exactly five current MOJ services.");
    Assert(new HashSet<string>(services.Select(service => service.Code), StringComparer.Ordinal).SetEquals(MojMetadataSeedService.CanonicalServiceCodes),
        "P09 canonical service set drifted.");
    Assert(services.All(service => service.Version == 2 && service.IsCurrent && service.Active),
        "All five proven UAT service definitions must be current version 2 metadata.");

    var expected = new Dictionary<string, ExpectedService>(StringComparer.Ordinal)
    {
        ["MARRIAGECASES"] = new(MojMetadataSeedService.MarriageUatBaseUrl, "/marriageCasesAPIGEE", "application/x-www-form-urlencoded",
            ["civilId"], ["data[0].CIVILID", "data[0].CONTRACT_DATE", "data[0].CONTRACT_NO", "data[0].CONTRACT_TYPE"],
            "/genToken", "application/x-www-form-urlencoded", "data", "username", "password", null, null, true),
        ["ISSINGLEBASIC"] = new(MojMetadataSeedService.MarriageUatBaseUrl, "/isSingleBasicAPIGEE", "application/x-www-form-urlencoded",
            ["civilId"], ["data[0].FENATI_NAME", "data[0].NATI_NAME", "data[0].maritalStatus", "data[0].marriageFree", "data[0].marriedCouple"],
            "/genToken", "application/x-www-form-urlencoded", "data", "username", "password", null, null, false),
        ["MARRIAGECOUPLELASTCASE"] = new(MojMetadataSeedService.MarriageUatBaseUrl, "/marriageCoupleLastAPIGEE", "application/x-www-form-urlencoded",
            ["male_civilId", "female_civilId"], ["data[0].FENATI_NAME", "data[0].NATI_NAME", "data[0].NO_DAT", "data[0].maritalStatus", "data[0].marriedCouple"],
            "/genToken", "application/x-www-form-urlencoded", "data", "username", "password", null, null, false),
        ["FAMILYJUDGMENTTEXT"] = new(MojMetadataSeedService.VerdictUatBaseUrl, "/familyJudgmentText", "application/json",
            ["caseNo", "type"], ["data.VRDESC"],
            "/token", "application/json", "token", "username", "password", null, null, false),
        ["PROCURATIONSTATUS"] = new(MojMetadataSeedService.ProcurationUatBaseUrl, "/Procuration/ProcurationStatus", "application/json",
            ["CivilClient", "CivilAgent", "year", "Number"], ["Status", "Message", "Data.type", "Data.Status"],
            "/Authenticate/Token", "application/json", "token", "UserName", "Password", "Geha", 21600, false)
    };

    foreach (var service in services)
    {
        Assert(expected.TryGetValue(service.Code, out var contract), $"Unexpected service code {service.Code}.");
        Assert(service.EnvironmentConfigs.Count == 2, $"{service.Code} must contain isolated UAT and Production rows.");

        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var production = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
        Assert(!uat.Active && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
            $"{service.Code} UAT must remain disabled until owner credentials are configured.");
        Assert(uat.BaseUrl == contract!.BaseUrl && uat.RelativePath == contract.RelativePath && uat.HttpMethod == "POST" && uat.ContentType == contract.ContentType,
            $"{service.Code} UAT transport metadata drifted from official contract.");
        Assert(uat.AuthProfileId.HasValue, $"{service.Code} UAT is missing its independent disabled AuthProfile binding.");

        using (var metadata = JsonDocument.Parse(uat.NonSecretHeadersJson))
        {
            var root = metadata.RootElement;
            Assert(root.GetProperty(MojMetadataSeedService.TokenPathMetadataKey).GetString() == contract.TokenPath, $"{service.Code} token path drifted.");
            Assert(root.GetProperty(MojMetadataSeedService.TokenRequestContentTypeMetadataKey).GetString() == contract.TokenContentType, $"{service.Code} token content type drifted.");
            Assert(root.GetProperty(MojMetadataSeedService.TokenResponsePathMetadataKey).GetString() == contract.TokenResponsePath, $"{service.Code} token response path drifted.");
            Assert(root.GetProperty(MojMetadataSeedService.TokenUsernameFieldMetadataKey).GetString() == contract.TokenUsernameField, $"{service.Code} token username wire name drifted.");
            Assert(root.GetProperty(MojMetadataSeedService.TokenPasswordFieldMetadataKey).GetString() == contract.TokenPasswordField, $"{service.Code} token password wire name drifted.");
            if (contract.TokenGehaField is null)
                Assert(!root.TryGetProperty(MojMetadataSeedService.TokenGehaFieldMetadataKey, out _), $"{service.Code} invented a Geha field.");
            else
                Assert(root.GetProperty(MojMetadataSeedService.TokenGehaFieldMetadataKey).GetString() == contract.TokenGehaField, $"{service.Code} Geha wire name drifted.");
            if (contract.DocumentedTtlSeconds is null)
                Assert(!root.TryGetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey, out _), $"{service.Code} invented a token TTL.");
            else
                Assert(root.GetProperty(MojMetadataSeedService.TokenDocumentedTtlSecondsMetadataKey).GetInt32() == contract.DocumentedTtlSeconds, $"{service.Code} documented token TTL drifted.");
            var hasApiKeyRequired = root.TryGetProperty("X-GSIP-TokenApiKeyRequired", out var apiKeyFlag) && apiKeyFlag.GetBoolean();
            Assert(hasApiKeyRequired == contract.ApiKeyRequired, $"{service.Code} operation-level token API-key evidence drifted.");
        }

        Assert(service.Fields.OrderBy(field => field.DisplayOrder).Select(field => field.Key).SequenceEqual(contract.RequestFields),
            $"{service.Code} request field names/order drifted from official contract.");
        Assert(service.Fields.All(field => field.FieldType == "text" && field.Regex.Length == 0 && field.MinLength is null && field.MaxLength is null),
            $"{service.Code} seed invented undocumented request validation.");
        Assert(contract.ResultMappings.All(path => service.ResultMappings.Any(mapping => mapping.SourcePath == path)),
            $"{service.Code} result mappings are missing official response fields.");

        Assert(!production.Active
            && production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix
            && production.RelativePath.Length == 0
            && production.HttpMethod.Length == 0
            && production.ContentType.Length == 0
            && production.AuthProfileId is null
            && production.LastTestStatus == "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT",
            $"{service.Code} Production must remain gateway-prefix-only and fail closed.");
        Assert(production.BaseUrl != uat.BaseUrl, $"{service.Code} Production silently fell back to UAT.");
    }

    var profiles = await db.AuthProfiles.AsNoTracking().Include(profile => profile.Bindings).Include(profile => profile.Secrets).ToListAsync();
    Assert(profiles.Count == 5 && await db.AuthProfileBindings.CountAsync() == 5,
        "Each P09 service must have exactly one independent UAT AuthProfile/binding.");
    foreach (var profile in profiles)
    {
        Assert(profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId,
            "Seeded P09 AuthProfiles must remain disabled exact-UAT TokenEndpoint profiles.");
        Assert(profile.Secrets.Count == 0 && profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared,
            "Seeded P09 AuthProfile contains secret material or implicit sharing.");
        Assert(profile.Bindings.Single().ServiceId == profile.OwnerServiceId
            && profile.Bindings.Single().EnvironmentId == profile.OwnerEnvironmentId,
            "P09 AuthProfile binding escaped exact Service + Environment scope.");
    }
    Assert(await db.AuthProfileSecrets.CountAsync() == 0 && await db.SecretVaultEntries.CountAsync() == 0,
        "P09 metadata seed must never persist credential references or secret material.");

    var nonSecretText = string.Join('\n', services.SelectMany(service => service.EnvironmentConfigs)
        .Select(config => $"{config.BaseUrl}|{config.RelativePath}|{config.NonSecretHeadersJson}"));
    foreach (var forbidden in new[] { "Bearer ", "Basic ", "password=", "username=", "consumer secret", "consumer key" })
        Assert(!nonSecretText.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Seed metadata contains credential-like content: {forbidden}");

    var beforeIds = services.Select(service => service.Id).Order().ToArray();
    var beforeCounts = await CaptureCountsAsync(db);
    await seed.SeedAsync();
    var afterSecondSeed = await LoadServicesAsync(db, entity.Id);
    Assert(beforeIds.SequenceEqual(afterSecondSeed.Select(service => service.Id).Order()), "Idempotent seed changed stable service identities.");
    Assert(beforeCounts == await CaptureCountsAsync(db), "Idempotent seed changed canonical persisted counts.");

    var ownerEdited = await db.CatalogServices.SingleAsync(service => service.Id == services[0].Id);
    ownerEdited.NameEn = "Owner Edited P09 Display";
    await db.SaveChangesAsync();
    await seed.SeedAsync();
    var preserved = await db.CatalogServices.AsNoTracking().SingleAsync(service => service.Id == ownerEdited.Id);
    Assert(preserved.NameEn == "Owner Edited P09 Display" && preserved.Version == 2,
        "Seed rerun silently overwrote owner-edited current metadata.");

    var evidenceDirectory = Path.Combine("artifacts", "p09-metadata-seed-evidence");
    Directory.CreateDirectory(evidenceDirectory);
    var evidence = new
    {
        phase = "P09",
        unit = "P09::moj-metadata-seed",
        definitionVersion = 2,
        serviceCount = 5,
        uatContracts = "PROVEN_5",
        productionContracts = "DEFERRED_EXTERNAL",
        uatCredentialState = "DISABLED_OWNER_CONFIGURATION_REQUIRED",
        exactAuthProfileScope = true,
        implicitSharing = false,
        seedSecretCount = 0,
        idempotent = true,
        ownerEditsPreserved = true
    };
    await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "manifest.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("P09_MOJ_METADATA_SEED_CHECKS=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static async Task<List<CatalogService>> LoadServicesAsync(GsipDbContext db, Guid entityId) =>
    await db.CatalogServices.AsNoTracking()
        .Where(service => service.EntityId == entityId && service.IsCurrent)
        .Include(service => service.EnvironmentConfigs)
        .Include(service => service.Fields)
        .Include(service => service.ResultMappings)
        .OrderBy(service => service.Code)
        .ToListAsync();

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

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed record ExpectedService(
    string BaseUrl,
    string RelativePath,
    string ContentType,
    IReadOnlyList<string> RequestFields,
    IReadOnlyList<string> ResultMappings,
    string TokenPath,
    string TokenContentType,
    string TokenResponsePath,
    string TokenUsernameField,
    string TokenPasswordField,
    string? TokenGehaField,
    int? DocumentedTtlSeconds,
    bool ApiKeyRequired);

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
