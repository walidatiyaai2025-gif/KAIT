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
    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-09T08:30:00Z"));
    var seed = new MojMetadataSeedService(db, clock);

    Assert(MojMetadataSeedService.DefinitionVersion == 1, "Unexpected P09 seed definition version.");
    await seed.SeedAsync();

    var entity = await db.CatalogEntities.AsNoTracking().SingleAsync(item => item.Code == MojMetadataSeedService.EntityCode);
    Assert(entity.NameAr == "وزارة العدل" && entity.NameEn == "Ministry of Justice", "MOJ bilingual entity display metadata is invalid.");
    Assert(entity.DisplayOrder == 1 && entity.Active, "MOJ must be the first active canonical entity seed.");

    var services = await LoadServicesAsync(db, entity.Id);
    Assert(services.Count == 5, "P09 must seed exactly five current MOJ services.");
    Assert(new HashSet<string>(services.Select(service => service.Code), StringComparer.Ordinal).SetEquals(MojMetadataSeedService.CanonicalServiceCodes),
        "P09 canonical service codes differ from the exact five-service scope.");
    Assert(services.All(service => service.Version == MojMetadataSeedService.DefinitionVersion && service.IsCurrent),
        "Canonical service definitions are not on the expected stable version.");
    Assert(services.Select(service => service.DefinitionKey).Distinct().Count() == 5,
        "Canonical service definition keys are not stable and distinct.");
    Assert(services.All(service => !string.IsNullOrWhiteSpace(service.NameAr) && !string.IsNullOrWhiteSpace(service.NameEn)),
        "Every canonical MOJ service must have safe Arabic and English display labels.");

    foreach (var service in services)
    {
        Assert(service.EnvironmentConfigs.Count == 2, $"{service.Code} must have exactly UAT and Production configuration rows.");
        Assert(service.EnvironmentConfigs.Count(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId) == 1,
            $"{service.Code} is missing its isolated UAT configuration.");
        Assert(service.EnvironmentConfigs.Count(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId) == 1,
            $"{service.Code} is missing its isolated Production configuration.");
        Assert(service.EnvironmentConfigs.All(config => !config.Active),
            $"{service.Code} contains an executable environment before all required contract/credential facts are available.");

        var production = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
        Assert(production.BaseUrl == MojMetadataSeedService.ProductionGatewayPrefix,
            $"{service.Code} Production must contain only the proven CAIT gateway prefix.");
        Assert(production.RelativePath.Length == 0 && production.HttpMethod.Length == 0 && production.ContentType.Length == 0,
            $"{service.Code} invented a Production path, method or content type.");
        Assert(production.AuthProfileId is null, $"{service.Code} invented a Production authentication binding.");
    }

    var marriage = services.Single(service => service.Code == "MARRIAGECASES");
    var marriageUat = marriage.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var marriageProduction = marriage.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Assert(marriageUat.BaseUrl == MojMetadataSeedService.Api129UatBaseUrl, "API 129 UAT base URL drifted from the integrated official snapshot.");
    Assert(marriageUat.RelativePath == MojMetadataSeedService.Api129TargetPath, "API 129 UAT target path drifted from the integrated official snapshot.");
    Assert(marriageUat.HttpMethod == "POST", "API 129 UAT method drifted from the integrated official snapshot.");
    Assert(marriageUat.ContentType == "application/x-www-form-urlencoded", "API 129 UAT content type drifted from the integrated official snapshot.");
    using (var headers = JsonDocument.Parse(marriageUat.NonSecretHeadersJson))
    {
        Assert(headers.RootElement.EnumerateObject().Count() == 1, "API 129 UAT contains unproven configured header metadata.");
        Assert(headers.RootElement.TryGetProperty("X-GSIP-TokenEndpointPath", out var tokenPath)
            && tokenPath.GetString() == MojMetadataSeedService.Api129TokenPath,
            "API 129 UAT token path drifted from the integrated official snapshot.");
    }
    Assert(marriageProduction.BaseUrl != marriageUat.BaseUrl && marriageProduction.RelativePath != marriageUat.RelativePath,
        "Production silently fell back to the UAT endpoint contract.");

    var deferredServices = services.Where(service => service.Code != "MARRIAGECASES").ToArray();
    foreach (var service in deferredServices)
    {
        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        Assert(uat.BaseUrl.Length == 0 && uat.RelativePath.Length == 0 && uat.HttpMethod.Length == 0 && uat.ContentType.Length == 0,
            $"{service.Code} invented unavailable UAT operation metadata.");
        Assert(uat.AuthProfileId is null, $"{service.Code} invented unavailable UAT authentication metadata.");
        Assert(service.Fields.Count == 0 && service.ResultMappings.Count == 0,
            $"{service.Code} invented request or result fields while its official contract is deferred.");
    }

    Assert(marriage.Fields.Count == 1, "API 129 must contain only the one officially evidenced request field.");
    var civilId = marriage.Fields.Single();
    Assert(civilId.Key == "civilId" && civilId.Required, "API 129 request key or requiredness drifted from official evidence.");
    Assert(civilId.LabelAr == "civilId" && civilId.LabelEn == "civilId",
        "API 129 technical field label must remain neutral when no authoritative display label exists.");
    Assert(civilId.FieldType == "deferred" && civilId.Regex.Length == 0 && civilId.Minimum is null && civilId.Maximum is null
        && civilId.MinLength is null && civilId.MaxLength is null,
        "API 129 invented a request-field type or validation rule.");
    Assert(civilId.Sensitive && civilId.Masking == "Last4", "API 129 civil identifier is not protected as sensitive metadata.");
    Assert(marriage.ResultMappings.Count == 0, "API 129 invented result mappings before response evidence exists.");
    Assert(services.Sum(service => service.ResultMappings.Count) == 0, "P09 seed invented result mappings.");

    Assert(marriageUat.AuthProfileId is Guid authProfileId, "API 129 UAT must have an exact disabled AuthProfile binding.");
    var profile = await db.AuthProfiles.AsNoTracking()
        .Include(item => item.Bindings)
        .Include(item => item.Secrets)
        .SingleAsync(item => item.Id == authProfileId);
    Assert(profile.OwnerServiceId == marriage.Id && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId,
        "API 129 AuthProfile owner scope is not exact Service + Environment.");
    Assert(profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled,
        "API 129 UAT AuthProfile must remain disabled until credential material is configured through the vault.");
    Assert(profile.Secrets.Count == 0, "Seed data must not contain AuthProfile secret references or credential material.");
    Assert(profile.Bindings.Count == 1, "API 129 AuthProfile has an unexpected shared binding.");
    var binding = profile.Bindings.Single();
    Assert(binding.ServiceId == marriage.Id && binding.EnvironmentId == CatalogEnvironmentCodes.UatId && !binding.IsShared,
        "API 129 AuthProfile binding is not exact or was implicitly shared.");
    Assert(await db.AuthProfiles.CountAsync() == 1 && await db.AuthProfileBindings.CountAsync() == 1,
        "P09 seed created an unproven or shared AuthProfile.");
    Assert(await db.AuthProfileSecrets.CountAsync() == 0 && await db.SecretVaultEntries.CountAsync() == 0,
        "P09 seed persisted secret references or secret vault content.");

    var nonSecretText = string.Join('\n', services.SelectMany(service => service.EnvironmentConfigs)
        .Select(config => $"{config.BaseUrl}|{config.RelativePath}|{config.HttpMethod}|{config.ContentType}|{config.NonSecretHeadersJson}"));
    foreach (var forbidden in new[] { "Bearer ", "Basic ", "password=", "username=", "consumer secret", "consumer key" })
    {
        Assert(!nonSecretText.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Seed metadata contains forbidden credential-like content: {forbidden}");
    }

    var beforeIds = services.Select(service => service.Id).Order().ToArray();
    var beforeCounts = await CaptureCountsAsync(db);
    await seed.SeedAsync();
    var afterSecondSeed = await LoadServicesAsync(db, entity.Id);
    var afterIds = afterSecondSeed.Select(service => service.Id).Order().ToArray();
    var afterCounts = await CaptureCountsAsync(db);
    Assert(beforeIds.SequenceEqual(afterIds), "Idempotent seed changed stable service identities.");
    Assert(beforeCounts == afterCounts, "Idempotent seed changed persisted canonical object counts.");

    var ownerEdited = await db.CatalogServices.SingleAsync(service => service.Id == marriage.Id);
    ownerEdited.NameEn = "Owner Edited Marriage Display";
    await db.SaveChangesAsync();
    await seed.SeedAsync();
    var preserved = await db.CatalogServices.AsNoTracking().SingleAsync(service => service.Id == marriage.Id);
    Assert(preserved.NameEn == "Owner Edited Marriage Display" && preserved.Version == MojMetadataSeedService.DefinitionVersion,
        "Seed upgrade/rerun silently overwrote owner-edited metadata.");
    Assert(await db.CatalogServices.CountAsync(service => service.DefinitionKey == marriage.DefinitionKey) == 1,
        "Seed rerun created a silent revision over owner-edited metadata.");

    var evidenceDirectory = Path.Combine("artifacts", "p09-metadata-seed-evidence");
    Directory.CreateDirectory(evidenceDirectory);
    var evidence = new
    {
        phase = "P09",
        unit = "P09::moj-metadata-seed",
        definitionVersion = MojMetadataSeedService.DefinitionVersion,
        entity = MojMetadataSeedService.EntityCode,
        serviceCount = services.Count,
        serviceCodes = services.Select(service => service.Code).Order().ToArray(),
        exactEnvironmentIsolation = true,
        productionGatewayPrefixOnly = true,
        noProductionUatFallback = true,
        disabledDeferredEnvironments = true,
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
