using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P17_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P17_SQL is required for P17 MOE persistence acceptance.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();

try
{
    await db.Database.MigrateAsync();
    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-10T12:00:00Z"));
    await new GreenGovernmentCatalogSeedService(db, clock).SeedAsync();
    await new MoeMetadataSeedService(db, clock).SeedAsync();

    Check(await db.CatalogEntities.CountAsync(entity => entity.Code == MoeMetadataSeedService.EntityCode) == 1,
        "MOE entity identity is missing or ambiguous.");

    var expectedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MOE_LAST_ACTIVE_RECORD"] = "/lastactive",
        ["MOE_LAST_STUDENT_RECORD"] = "/last",
        ["MOE_LAST_SUCCESS_RECORD"] = "/lastsuccess"
    };
    var expectedCodes = expectedPaths.Keys.ToArray();

    var services = await db.CatalogServices.AsNoTracking()
        .Where(service => service.IsCurrent && expectedCodes.Contains(service.Code))
        .Include(service => service.EnvironmentConfigs)
        .Include(service => service.Fields)
        .Include(service => service.ResultMappings)
        .OrderBy(service => service.Code)
        .ToListAsync();

    Check(services.Count == 3, "P17 must materialize exactly three MOE Student API services.");
    Check(services.All(service => service.Active && service.Version == MoeMetadataSeedService.DefinitionVersion),
        "MOE services must be current active definition version 2.");

    foreach (var service in services)
    {
        Check(service.EnvironmentConfigs.Count == 2,
            $"{service.Code} must contain exactly isolated UAT and Production configurations.");
        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var production = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);

        Check(uat.BaseUrl == MoeMetadataSeedService.UatBaseUrl
              && uat.RelativePath == expectedPaths[service.Code]
              && uat.HttpMethod == "GET"
              && uat.ContentType == "application/json"
              && !uat.Active
              && uat.AuthProfileId.HasValue
              && uat.LastTestStatus == "CONTRACT_READY_BASIC_CREDENTIALS_REQUIRED",
            $"{service.Code} UAT transport/auth metadata drifted.");

        Check(!production.Active
              && production.AuthProfileId is null
              && production.BaseUrl.Length == 0
              && production.RelativePath.Length == 0
              && production.HttpMethod.Length == 0
              && production.ContentType.Length == 0
              && production.LastTestStatus == "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT",
            $"{service.Code} Production must remain empty and fail closed.");

        var cid = service.Fields.SingleOrDefault(field => field.Key == "cid");
        Check(cid is not null && cid.Required && cid.FieldType == "integer" && cid.Sensitive,
            $"{service.Code} must expose required sensitive integer query field cid.");
        Check(service.ResultMappings.Count >= 8,
            $"{service.Code} response mapping is incomplete.");
    }

    var serviceIds = services.Select(service => service.Id).ToArray();
    var profiles = await db.AuthProfiles.AsNoTracking()
        .Where(profile => serviceIds.Contains(profile.OwnerServiceId))
        .Include(profile => profile.Bindings)
        .Include(profile => profile.Secrets)
        .ToListAsync();

    Check(profiles.Count == 3, "Every MOE UAT service must own exactly one independent AuthProfile.");
    foreach (var profile in profiles)
    {
        Check(profile.AuthType == AuthProfileType.CustomHeaders
              && !profile.IsEnabled
              && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId,
            "MOE AuthProfile must remain disabled in exact UAT scope until Basic credentials are provisioned.");
        Check(profile.Secrets.Count == 0,
            "MOE seed must not persist username/password secret references.");
        Check(profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared,
            "MOE Basic credentials must not be implicitly shared across services.");
        Check(profile.Bindings.Single().ServiceId == profile.OwnerServiceId
              && profile.Bindings.Single().EnvironmentId == profile.OwnerEnvironmentId,
            "MOE AuthProfile binding escaped exact Service + Environment scope.");
    }

    Check(await db.AuthProfileSecrets.CountAsync() == 0 && await db.SecretVaultEntries.CountAsync() == 0,
        "MOE contract seed persisted credential material or secret references.");

    var before = await CaptureCountsAsync(db);
    var beforeIds = services.Select(service => service.Id).Order().ToArray();
    await new MoeMetadataSeedService(db, clock).SeedAsync();
    var after = await CaptureCountsAsync(db);
    var afterIds = await db.CatalogServices.AsNoTracking()
        .Where(service => service.IsCurrent && expectedCodes.Contains(service.Code))
        .Select(service => service.Id)
        .OrderBy(id => id)
        .ToArrayAsync();
    Check(before == after && beforeIds.SequenceEqual(afterIds),
        "MOE official contract seed is not idempotent.");

    var edited = await db.CatalogServices.SingleAsync(service => service.IsCurrent && service.Code == "MOE_LAST_STUDENT_RECORD");
    edited.NameEn = "Owner Preserved MOE Name";
    await db.SaveChangesAsync();
    await new MoeMetadataSeedService(db, clock).SeedAsync();
    var preserved = await db.CatalogServices.AsNoTracking().SingleAsync(service => service.Id == edited.Id);
    Check(preserved.NameEn == "Owner Preserved MOE Name" && preserved.Version == 2 && preserved.IsCurrent,
        "MOE seed rerun overwrote owner-edited current metadata.");

    Console.WriteLine("P17_MOE_METADATA_ACCEPTANCE=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static async Task<DbCounts> CaptureCountsAsync(GsipDbContext db) => new(
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
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record DbCounts(
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
