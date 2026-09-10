using System.Reflection;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var assembly = typeof(MojAuthenticationProbeService).Assembly;
var contractType = assembly.GetType("GSIP.Infrastructure.Execution.TokenEndpointContractMetadata", throwOnError: true)!;
var parse = contractType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
    ?? throw new InvalidOperationException("TokenEndpointContractMetadata.Parse was not found.");
var build = contractType.GetMethod("BuildRequestContent", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new InvalidOperationException("TokenEndpointContractMetadata.BuildRequestContent was not found.");

await CscIntegerAuthenticationContextAsync();
await LegacyStringThirdCredentialRemainsCompatibleAsync();
RejectInvalidIntegerAuthenticationContext();
await OfficialAgencySeedPersistenceAsync();
await OwnerUsedCscPlaceholderIsPreservedAsync();

Console.WriteLine("P17_OFFICIAL_AGENCY_CONTRACT_ACCEPTANCE=PASS");
return;

async Task CscIntegerAuthenticationContextAsync()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/csc/token/generate",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "response.token",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password",
        ["X-GSIP-TokenGehaField"] = "civilId",
        ["X-GSIP-TokenGehaValueType"] = "integer"
    });

    var contract = Parse(metadata);
    using var content = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "123456789012");
    using var document = JsonDocument.Parse(await content.ReadAsStringAsync());
    var root = document.RootElement;

    Check(content.Headers.ContentType?.MediaType == "application/json", "CSC token request content type drifted.");
    Check(root.GetProperty("username").GetString() == "SYNTHETIC_USER", "CSC token username field drifted.");
    Check(root.GetProperty("password").GetString() == "SYNTHETIC_PASSWORD", "CSC token password field drifted.");
    Check(root.GetProperty("civilId").ValueKind == JsonValueKind.Number, "CSC token civilId must be JSON numeric.");
    Check(root.GetProperty("civilId").GetInt64() == 123456789012L, "CSC token civilId numeric value drifted.");
}

async Task LegacyStringThirdCredentialRemainsCompatibleAsync()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/Authenticate/Token",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "token",
        ["X-GSIP-TokenUsernameField"] = "UserName",
        ["X-GSIP-TokenPasswordField"] = "Password",
        ["X-GSIP-TokenGehaField"] = "Geha"
    });

    var contract = Parse(metadata);
    using var content = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "SYNTHETIC_GEHA");
    using var document = JsonDocument.Parse(await content.ReadAsStringAsync());
    Check(document.RootElement.GetProperty("Geha").ValueKind == JsonValueKind.String,
        "Legacy MOJ Geha token credential must remain a JSON string.");
}

void RejectInvalidIntegerAuthenticationContext()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/csc/token/generate",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "response.token",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password",
        ["X-GSIP-TokenGehaField"] = "civilId",
        ["X-GSIP-TokenGehaValueType"] = "integer"
    });

    var contract = Parse(metadata);
    try
    {
        using var _ = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "not-an-integer");
        throw new InvalidOperationException("Invalid CSC token civilId did not fail closed.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
    {
        // Expected: invalid typed authentication context is rejected before transport.
    }
}

async Task OfficialAgencySeedPersistenceAsync()
{
    await WithFreshDatabaseAsync(async (db, clock) =>
    {
        await new GreenGovernmentCatalogSeedService(db, clock).SeedAsync();
        await new OfficialAgencyMetadataSeedService(db, clock).SeedAsync();

        var officialCodes = OfficialServiceCodes();
        var services = await db.CatalogServices.AsNoTracking()
            .Where(service => service.IsCurrent && officialCodes.Contains(service.Code))
            .Include(service => service.EnvironmentConfigs)
            .Include(service => service.Fields)
            .Include(service => service.ResultMappings)
            .OrderBy(service => service.Code)
            .ToListAsync();

        Check(services.Count == 7, "P17 must materialize exactly four MOH and three CSC official UAT service overlays.");
        Check(services.All(service => service.Version == OfficialAgencyMetadataSeedService.DefinitionVersion && service.Active),
            "Every P17 official agency overlay must be current active definition version 2.");
        Check(await db.CatalogEntities.CountAsync(entity => entity.Code == OfficialAgencyMetadataSeedService.MohEntityCode) == 1,
            "P17 MOH entity identity is missing or ambiguous.");
        Check(await db.CatalogEntities.CountAsync(entity => entity.Code == OfficialAgencyMetadataSeedService.CscEntityCode) == 1,
            "P17 CSC entity identity is missing or ambiguous.");

        foreach (var service in services)
        {
            Check(service.EnvironmentConfigs.Count == 2, $"{service.Code} must contain exactly isolated UAT and Production configs.");
            var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
            var production = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);

            Check(!uat.Active && uat.AuthProfileId.HasValue && uat.LastTestStatus == "CONTRACT_READY_CREDENTIALS_REQUIRED",
                $"{service.Code} UAT must remain disabled and bound to a protected AuthProfile until owner credential provisioning.");
            Check(!string.IsNullOrWhiteSpace(uat.BaseUrl) && !string.IsNullOrWhiteSpace(uat.RelativePath)
                  && !string.IsNullOrWhiteSpace(uat.HttpMethod),
                $"{service.Code} UAT official transport metadata was not materialized.");
            Check(!production.Active && production.AuthProfileId is null
                  && production.BaseUrl.Length == 0 && production.RelativePath.Length == 0
                  && production.HttpMethod.Length == 0 && production.ContentType.Length == 0
                  && production.LastTestStatus == "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT",
                $"{service.Code} Production must remain empty, independent and fail closed.");
        }

        var serviceIds = services.Select(service => service.Id).ToArray();
        var profiles = await db.AuthProfiles.AsNoTracking()
            .Where(profile => serviceIds.Contains(profile.OwnerServiceId))
            .Include(profile => profile.Bindings)
            .Include(profile => profile.Secrets)
            .ToListAsync();
        Check(profiles.Count == 7, "Every P17 official UAT service must own exactly one independent AuthProfile.");
        foreach (var profile in profiles)
        {
            Check(profile.AuthType == AuthProfileType.TokenEndpoint && !profile.IsEnabled
                  && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId,
                "P17 seeded AuthProfiles must be disabled exact-UAT TokenEndpoint profiles.");
            Check(profile.Secrets.Count == 0 && profile.Bindings.Count == 1 && !profile.Bindings.Single().IsShared,
                "P17 seed must not persist secret references or implicit sharing.");
            Check(profile.Bindings.Single().ServiceId == profile.OwnerServiceId
                  && profile.Bindings.Single().EnvironmentId == profile.OwnerEnvironmentId,
                "P17 AuthProfile binding escaped exact Service + Environment scope.");
        }
        Check(await db.AuthProfileSecrets.CountAsync() == 0 && await db.SecretVaultEntries.CountAsync() == 0,
            "P17 official contract seed persisted credential references or secret material.");

        var before = await CaptureCountsAsync(db);
        var beforeIds = services.Select(service => service.Id).Order().ToArray();
        await new OfficialAgencyMetadataSeedService(db, clock).SeedAsync();
        var after = await CaptureCountsAsync(db);
        var afterIds = await db.CatalogServices.AsNoTracking()
            .Where(service => service.IsCurrent && officialCodes.Contains(service.Code))
            .Select(service => service.Id)
            .OrderBy(id => id)
            .ToArrayAsync();
        Check(before == after && beforeIds.SequenceEqual(afterIds),
            "P17 official agency seed is not idempotent.");

        var ownerEdited = await db.CatalogServices.SingleAsync(service => service.IsCurrent && service.Code == "MOH_CERTIFICATE_INFORMATION");
        ownerEdited.NameEn = "Owner Preserved Display Name";
        await db.SaveChangesAsync();
        await new OfficialAgencyMetadataSeedService(db, clock).SeedAsync();
        var preserved = await db.CatalogServices.AsNoTracking().SingleAsync(service => service.Id == ownerEdited.Id);
        Check(preserved.NameEn == "Owner Preserved Display Name" && preserved.Version == 2 && preserved.IsCurrent,
            "P17 seed rerun overwrote owner-edited current official metadata.");
    });
}

async Task OwnerUsedCscPlaceholderIsPreservedAsync()
{
    await WithFreshDatabaseAsync(async (db, clock) =>
    {
        await new GreenGovernmentCatalogSeedService(db, clock).SeedAsync();
        var placeholder = await db.CatalogServices.SingleAsync(service =>
            service.IsCurrent && service.Code == "CSC_EMPLOYEE_DATA");
        Check(placeholder.Version == 1 && placeholder.EnvironmentConfigs.Count == 0,
            "CSC test prerequisite is not an untouched green-catalog placeholder.");

        placeholder.FirstUsedAtUtc = clock.UtcNow;
        placeholder.NameEn = "Owner Used CSC Employee Data";
        await db.SaveChangesAsync();

        await new OfficialAgencyMetadataSeedService(db, clock).SeedAsync();

        var preserved = await db.CatalogServices.AsNoTracking()
            .Include(service => service.EnvironmentConfigs)
            .SingleAsync(service => service.Id == placeholder.Id);
        Check(preserved.IsCurrent && preserved.Active && preserved.Version == 1
              && preserved.NameEn == "Owner Used CSC Employee Data" && preserved.EnvironmentConfigs.Count == 0,
            "P17 overlay replaced an owner-used CSC placeholder.");

        var upgradedSiblingCount = await db.CatalogServices.CountAsync(service => service.IsCurrent
            && service.Version == OfficialAgencyMetadataSeedService.DefinitionVersion
            && (service.Code == "CSC_EMPLOYEE_FINANCIAL_DATA" || service.Code == "CSC_EMPLOYEE_SALARY_DETAILS"));
        Check(upgradedSiblingCount == 2,
            "Preserving one owner-used CSC placeholder incorrectly blocked unrelated official CSC overlays.");
    });
}

async Task WithFreshDatabaseAsync(Func<GsipDbContext, FixedClock, Task> test)
{
    var connection = Environment.GetEnvironmentVariable("GSIP_P17_SQL");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("GSIP_P17_SQL is required for P17 persistence acceptance.");

    var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
    await using var db = new GsipDbContext(options);
    await db.Database.EnsureDeletedAsync();
    try
    {
        await db.Database.MigrateAsync();
        await test(db, new FixedClock(DateTimeOffset.Parse("2026-09-10T11:30:00Z")));
    }
    finally
    {
        await db.Database.EnsureDeletedAsync();
    }
}

static HashSet<string> OfficialServiceCodes() => new(StringComparer.Ordinal)
{
    "MOH_CERTIFICATE_INFORMATION",
    "MOH_DEATH_CERTIFICATE_CIVIL_ID",
    "MOH_DEATH_CERTIFICATE_PASSPORT",
    "MOH_MEDICAL_LICENSE_INSTITUTION",
    "CSC_EMPLOYEE_DATA",
    "CSC_EMPLOYEE_FINANCIAL_DATA",
    "CSC_EMPLOYEE_SALARY_DETAILS"
};

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

object Parse(string metadata)
{
    try
    {
        return parse.Invoke(null, [metadata])
            ?? throw new InvalidOperationException("Token endpoint contract parse returned null.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

HttpContent Build(object contract, string username, string password, string thirdCredential) =>
    (HttpContent)(build.Invoke(contract, [username, password, thirdCredential])
        ?? throw new InvalidOperationException("Token request content builder returned null."));

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
