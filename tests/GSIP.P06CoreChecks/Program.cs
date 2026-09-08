using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

var connection = Environment.GetEnvironmentVariable("GSIP_P06_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P06_SQL is required.");

var keyDirectory = Path.Combine(Path.GetTempPath(), $"gsip-p06-keys-{Guid.NewGuid():N}");
Directory.CreateDirectory(keyDirectory);

try
{
    var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
    await using (var db = new GsipDbContext(options))
    {
        await db.Database.EnsureDeletedAsync();

        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260908170500_MetadataCatalog");

        var preserved = new CatalogEntity
        {
            Id = Guid.NewGuid(),
            Code = "P05-PRESERVE",
            NameAr = "حالة محفوظة",
            NameEn = "P05 Preserved State",
            Logo = string.Empty,
            Active = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-09-08T19:35:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-09-08T19:35:00Z")
        };
        db.CatalogEntities.Add(preserved);
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert(await db.CatalogEntities.AsNoTracking().AnyAsync(x => x.Id == preserved.Id && x.Code == "P05-PRESERVE"),
            "P06 migration did not preserve existing P05 state.");
        Assert(await db.SecretVaultEntries.CountAsync() == 0 && await db.AuthProfiles.CountAsync() == 0,
            "P06 migration did not create an empty vault/profile foundation deterministically.");

        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-08T19:36:00Z"));
        var catalog = new MetadataCatalogService(db, clock);
        var entity = await catalog.CreateEntityAsync(new EntityInput(
            "P06-SYNTH", "جهة اختبار الأسرار", "P06 Synthetic Authority", string.Empty, true, 10));

        var serviceA = await catalog.CreateServiceAsync(entity.Id, BuildService(
            "SERVICE-A",
            "Synthetic Service A",
            "https://shared-uat.example.invalid",
            "https://service-a-prod.example.invalid"));
        var serviceB = await catalog.CreateServiceAsync(entity.Id, BuildService(
            "SERVICE-B",
            "Synthetic Service B",
            "https://shared-uat.example.invalid",
            "https://service-b-prod.example.invalid"));

        var configsA = await db.ServiceEnvironmentConfigs.AsNoTracking()
            .Where(x => x.ServiceId == serviceA.Id).ToListAsync();
        var configsB = await db.ServiceEnvironmentConfigs.AsNoTracking()
            .Where(x => x.ServiceId == serviceB.Id).ToListAsync();
        var aUat = configsA.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var aProd = configsA.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
        var bUat = configsB.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var bProd = configsB.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);

        var provider = DataProtectionProvider.Create(new DirectoryInfo(keyDirectory), builder => builder.SetApplicationName("GSIP.P06.CoreChecks"));
        var vault = new DataProtectionSecretVault(db, provider, clock);
        var profiles = new AuthProfileService(db, vault, clock);

        const string syntheticPlaintext = "synthetic-p06-secret-material-7H4v2";
        var secretBytes = Encoding.UTF8.GetBytes(syntheticPlaintext);
        var createdSecret = await vault.CreateActiveAsync(secretBytes);
        CryptographicOperations.ZeroMemory(secretBytes);

        Assert(createdSecret.Reference.Value.StartsWith(SecretRef.Prefix, StringComparison.Ordinal)
               && createdSecret.Reference.Value.Length == SecretRef.Prefix.Length + SecretRef.TokenLength,
            "SecretRef is not opaque/versioned.");
        Assert(!createdSecret.Reference.Value.Contains("synthetic", StringComparison.OrdinalIgnoreCase),
            "SecretRef exposed semantic secret material.");

        var persistedSecret = await db.SecretVaultEntries.AsNoTracking()
            .SingleAsync(x => x.Reference == createdSecret.Reference.Value);
        Assert(persistedSecret.ProtectedPayload.Length > 0, "Protected secret payload was not persisted.");
        Assert(!Encoding.UTF8.GetString(persistedSecret.ProtectedPayload).Contains(syntheticPlaintext, StringComparison.Ordinal),
            "Plaintext secret material was persisted.");
        Assert(persistedSecret.State == SecretLifecycleState.Active && persistedSecret.Generation == 1,
            "Initial secret lifecycle state is invalid.");

        var resolvedMaterial = await vault.UseSecretAsync(
            createdSecret.Reference,
            (material, _) => ValueTask.FromResult(Encoding.UTF8.GetString(material.Span)));
        Assert(resolvedMaterial == syntheticPlaintext, "Minimal decrypt boundary did not resolve protected material correctly.");

        var profileAUat = await profiles.CreateAsync(new CreateAuthProfileCommand(
            serviceA.Id,
            CatalogEnvironmentCodes.UatId,
            "Service A UAT",
            AuthProfileType.ApiKeyHeader,
            "synthetic-admin",
            new Dictionary<string, SecretRef> { ["api-key"] = createdSecret.Reference }));

        Assert(profileAUat.Secrets.Count == 1 && profileAUat.Secrets[0].Reference == createdSecret.Reference,
            "AuthProfile did not persist an opaque SecretRef.");
        Assert(profileAUat.Bindings.Count == 1 && !profileAUat.Bindings[0].IsShared
               && profileAUat.Bindings[0].ServiceId == serviceA.Id
               && profileAUat.Bindings[0].EnvironmentId == CatalogEnvironmentCodes.UatId,
            "AuthProfile owner binding was not persisted exactly.");

        Assert((await profiles.ResolveAsync(serviceA.Id, CatalogEnvironmentCodes.UatId))?.Id == profileAUat.Id,
            "Exact Service A/UAT AuthProfile resolution failed.");
        Assert(await profiles.ResolveAsync(serviceA.Id, CatalogEnvironmentCodes.ProductionId) is null,
            "UAT AuthProfile fell back into Production.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId) is null,
            "Matching UAT endpoints caused implicit cross-service credential sharing.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.ProductionId) is null,
            "AuthProfile unexpectedly inherited across service/environment.");

        var prodBytes = Encoding.UTF8.GetBytes("synthetic-prod-secret-material-L9q5");
        var prodSecret = await vault.CreateActiveAsync(prodBytes);
        CryptographicOperations.ZeroMemory(prodBytes);
        var profileAProd = await profiles.CreateAsync(new CreateAuthProfileCommand(
            serviceA.Id,
            CatalogEnvironmentCodes.ProductionId,
            "Service A Production",
            AuthProfileType.StaticBearer,
            "synthetic-admin",
            new Dictionary<string, SecretRef> { ["bearer"] = prodSecret.Reference }));
        Assert(profileAProd.Id != profileAUat.Id && profileAProd.Secrets[0].Reference != profileAUat.Secrets[0].Reference,
            "UAT and Production credential bindings were not independent.");

        await ExpectInvalidAsync(
            () => profiles.ShareAsync(new ShareAuthProfileCommand(
                profileAUat.Id, serviceB.Id, CatalogEnvironmentCodes.UatId, "synthetic-admin", string.Empty)),
            "AuthProfile sharing without an explicit reason was accepted.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId) is null,
            "Rejected sharing attempt mutated the target binding.");

        const string shareReason = "Explicit synthetic cross-service sharing acceptance";
        var shared = await profiles.ShareAsync(new ShareAuthProfileCommand(
            profileAUat.Id,
            serviceB.Id,
            CatalogEnvironmentCodes.UatId,
            "synthetic-admin",
            shareReason));
        Assert(shared.Id == profileAUat.Id, "Explicit sharing created an unrelated AuthProfile.");
        var sharedBinding = shared.Bindings.Single(x =>
            x.ServiceId == serviceB.Id && x.EnvironmentId == CatalogEnvironmentCodes.UatId);
        Assert(sharedBinding.IsShared
               && sharedBinding.DecisionBy == "synthetic-admin"
               && sharedBinding.DecisionReason == shareReason,
            "Shared AuthProfile decision was not explicit, persisted, and attributable.");
        Assert((await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId))?.Id == profileAUat.Id,
            "Explicit shared AuthProfile did not resolve for the exact target.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.ProductionId) is null,
            "Explicit UAT sharing leaked into Production.");

        db.ChangeTracker.Clear();
        var forbiddenDirectBinding = await db.ServiceEnvironmentConfigs.SingleAsync(x => x.Id == bProd.Id);
        forbiddenDirectBinding.AuthProfileId = profileAUat.Id;
        await ExpectDbUpdateFailureAsync(
            () => db.SaveChangesAsync(),
            "A forged Service/Environment AuthProfileId bypassed the persisted binding decision.");
        db.ChangeTracker.Clear();
        Assert((await db.ServiceEnvironmentConfigs.AsNoTracking().SingleAsync(x => x.Id == bProd.Id)).AuthProfileId is null,
            "Failed forged binding changed persisted Service/Environment state.");

        var forgedRef = new SecretRef($"{SecretRef.Prefix}{new string('A', SecretRef.TokenLength)}");
        await ExpectSecretRejectedAsync(
            () => vault.GetDescriptorAsync(forgedRef),
            "A forged opaque SecretRef resolved.");
        await vault.RevokeAsync(createdSecret.Reference);
        await ExpectSecretRejectedAsync(
            () => vault.UseSecretAsync(
                createdSecret.Reference,
                static (_, _) => ValueTask.FromResult(true)),
            "A stale/revoked SecretRef still resolved secret material.");

        var storedProfileSecret = await db.AuthProfileSecrets.AsNoTracking()
            .SingleAsync(x => x.AuthProfileId == profileAUat.Id && x.SecretName == "api-key");
        Assert(storedProfileSecret.SecretReference == createdSecret.Reference.Value,
            "AuthProfile secret slot did not retain its opaque SecretRef.");
        Assert(!storedProfileSecret.SecretReference.Contains("synthetic-p06-secret-material", StringComparison.OrdinalIgnoreCase),
            "AuthProfile persisted plaintext secret material.");

        AssertNoPlaintextProperties(
            typeof(SecretDescriptor),
            typeof(AuthProfileDescriptor),
            typeof(AuthProfileSecretDescriptor),
            typeof(AuthProfileBindingDescriptor));

        Console.WriteLine("P06_SECRET_AUTH_FOUNDATION=PASS");
        await db.Database.EnsureDeletedAsync();
    }

    await using (var cleanDb = new GsipDbContext(
                     new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options))
    {
        await cleanDb.Database.MigrateAsync();
        Assert(await cleanDb.SecretVaultEntries.CountAsync() == 0
               && await cleanDb.AuthProfiles.CountAsync() == 0
               && await cleanDb.CatalogEnvironments.CountAsync() == 2,
            "Clean database migration did not produce the expected P00-P06 schema.");
        Console.WriteLine("P06_CLEAN_DATABASE_MIGRATION=PASS");
        await cleanDb.Database.EnsureDeletedAsync();
    }
}
finally
{
    if (Directory.Exists(keyDirectory))
        Directory.Delete(keyDirectory, recursive: true);
}

static ServiceInput BuildService(
    string code,
    string nameEn,
    string uatBaseUrl,
    string productionBaseUrl) =>
    new(
        code,
        "خدمة تجريبية",
        nameEn,
        "تعريف اختبار P06",
        "P06 synthetic metadata definition",
        true,
        [
            new ServiceEnvironmentInput(
                "UAT", uatBaseUrl, "/api/sample", "POST", "application/json",
                "{\"X-Correlation-Mode\":\"generated\"}", 30, "SystemDefault", true, string.Empty,
                "/health", "HEAD", true, null),
            new ServiceEnvironmentInput(
                "Production", productionBaseUrl, "/api/sample", "POST", "application/json",
                "{\"X-Correlation-Mode\":\"generated\"}", 45, "SystemDefault", true, string.Empty,
                "/health", "HEAD", true, null)
        ],
        [
            new ServiceFieldInput(
                "civil-id", "الرقم المدني", "Civil ID", "text", true, "^[0-9]{12}$",
                null, null, 12, 12, "[]", 10, true, "Last4")
        ],
        [
            new ResultMappingInput("$.data.status", "الحالة", "Status", "text", string.Empty, false, 10)
        ]);

static async Task ExpectInvalidAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task ExpectDbUpdateFailureAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (DbUpdateException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task ExpectSecretRejectedAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (SecretReferenceRejectedException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void AssertNoPlaintextProperties(params Type[] types)
{
    var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Plaintext",
        "SecretValue",
        "ProtectedPayload",
        "Password",
        "Token",
        "SecretMaterial"
    };
    foreach (var type in types)
    {
        var exposed = type.GetProperties().Select(x => x.Name).FirstOrDefault(forbidden.Contains);
        if (exposed is not null)
            throw new InvalidOperationException($"Normal read model '{type.Name}' exposes forbidden property '{exposed}'.");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
