using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Application.Security;
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
            Id = Guid.NewGuid(), Code = "P05-PRESERVE", NameAr = "حالة محفوظة", NameEn = "P05 Preserved State",
            Logo = string.Empty, Active = true, DisplayOrder = 1,
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

        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-08T20:10:00Z"));
        var catalog = new MetadataCatalogService(db, clock);
        var entity = await catalog.CreateEntityAsync(new EntityInput(
            "P06-SYNTH", "جهة اختبار الأسرار", "P06 Synthetic Authority", string.Empty, true, 10));
        var serviceA = await catalog.CreateServiceAsync(entity.Id, BuildService(
            "SERVICE-A", "Synthetic Service A", "https://shared-uat.example.invalid", "https://service-a-prod.example.invalid"));
        var serviceB = await catalog.CreateServiceAsync(entity.Id, BuildService(
            "SERVICE-B", "Synthetic Service B", "https://shared-uat.example.invalid", "https://service-b-prod.example.invalid"));

        var provider = DataProtectionProvider.Create(new DirectoryInfo(keyDirectory), builder => builder.SetApplicationName("GSIP.P06.CoreChecks"));
        var vault = new DataProtectionSecretVault(db, provider, clock);
        var profiles = new AuthProfileService(db, vault, clock);
        var rotation = new SecretRotationPersistenceAdapter(vault, profiles);

        const string sentinelA = "P06_SCOPE_SENTINEL_A_7H4v2";
        var aBytes = Encoding.UTF8.GetBytes(sentinelA);
        var secretA = await vault.CreateActiveAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, null, "api-key", aBytes);
        CryptographicOperations.ZeroMemory(aBytes);

        Assert(secretA.Reference.Value.StartsWith(SecretRef.Prefix, StringComparison.Ordinal)
               && secretA.Reference.Value.Length == SecretRef.Prefix.Length + SecretRef.TokenLength,
            "SecretRef is not opaque/versioned.");
        Assert(secretA.ServiceId == serviceA.Id && secretA.EnvironmentId == CatalogEnvironmentCodes.UatId
               && secretA.AuthProfileId is null && secretA.SecretName == "api-key",
            "SecretRef durable Service/Environment scope was not persisted.");

        var persistedA = await db.SecretVaultEntries.AsNoTracking().SingleAsync(x => x.Reference == secretA.Reference.Value);
        Assert(persistedA.ProtectedPayload.Length > 0, "Protected secret payload was not persisted.");
        Assert(!Encoding.UTF8.GetString(persistedA.ProtectedPayload).Contains(sentinelA, StringComparison.Ordinal),
            "Plaintext secret material was persisted.");

        var profileAUat = await profiles.CreateAsync(new CreateAuthProfileCommand(
            serviceA.Id, CatalogEnvironmentCodes.UatId, "Service A UAT", AuthProfileType.ApiKeyHeader,
            "synthetic-admin", new Dictionary<string, SecretRef> { ["api-key"] = secretA.Reference }));
        Assert(profileAUat.Version == 1 && profileAUat.Secrets.Single().Generation == 1,
            "Initial AuthProfile security version/generation is invalid.");
        var claimedA = await vault.GetDescriptorAsync(secretA.Reference);
        Assert(claimedA.AuthProfileId == profileAUat.Id,
            "SecretRef was not durably claimed to its owning AuthProfile boundary.");

        var clearA = await vault.UseSecretAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key", secretA.Reference,
            (material, _) => ValueTask.FromResult(Encoding.UTF8.GetString(material.Span)));
        Assert(clearA == sentinelA, "Scoped decrypt boundary did not resolve the owning secret.");
        await ExpectSecretRejectedAsync(
            () => vault.UseSecretAsync(
                serviceB.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key", secretA.Reference,
                static (_, _) => ValueTask.FromResult(true)),
            "Service B resolved Service A secret material directly.");

        var profileBProd = await profiles.CreateAsync(new CreateAuthProfileCommand(
            serviceB.Id, CatalogEnvironmentCodes.ProductionId, "Service B Production", AuthProfileType.ApiKeyHeader,
            "synthetic-admin"));
        var bBefore = await profiles.GetAsync(profileBProd.Id);
        var crossServiceError = await ExpectSecretRejectedCaptureAsync(
            () => profiles.SetSecretReferenceAsync(profileBProd.Id, "api-key", secretA.Reference),
            "A real valid Service A SecretRef was attachable to unrelated Service B/AuthProfile B.");
        Assert(!crossServiceError.Message.Contains(sentinelA, StringComparison.Ordinal),
            "Cross-service rejection leaked plaintext secret material.");
        var bAfterRejected = await profiles.GetAsync(profileBProd.Id);
        Assert(bAfterRejected.Secrets.Count == bBefore.Secrets.Count && bAfterRejected.Version == bBefore.Version,
            "Rejected cross-service SecretRef attachment mutated Service B.");
        Assert((await profiles.GetAsync(profileAUat.Id)).Secrets.Single().Reference == secretA.Reference,
            "Rejected cross-service SecretRef attachment corrupted Service A.");

        var profileAProd = await profiles.CreateAsync(new CreateAuthProfileCommand(
            serviceA.Id, CatalogEnvironmentCodes.ProductionId, "Service A Production", AuthProfileType.ApiKeyHeader,
            "synthetic-admin"));
        await ExpectSecretRejectedAsync(
            () => profiles.SetSecretReferenceAsync(profileAProd.Id, "api-key", secretA.Reference),
            "Service A UAT SecretRef was attachable to Service A Production.");
        Assert((await profiles.GetAsync(profileAProd.Id)).Secrets.Count == 0,
            "Rejected UAT-to-Production SecretRef attachment mutated Production.");

        var bBytes = Encoding.UTF8.GetBytes("P06_SERVICE_B_PROD_SYNTHETIC");
        var bSecret = await vault.CreateActiveAsync(
            serviceB.Id, CatalogEnvironmentCodes.ProductionId, profileBProd.Id, "api-key", bBytes);
        CryptographicOperations.ZeroMemory(bBytes);
        profileBProd = await profiles.SetSecretReferenceAsync(profileBProd.Id, "api-key", bSecret.Reference);

        var prodBytes = Encoding.UTF8.GetBytes("P06_SERVICE_A_PROD_SYNTHETIC");
        var prodSecret = await vault.CreateActiveAsync(
            serviceA.Id, CatalogEnvironmentCodes.ProductionId, profileAProd.Id, "api-key", prodBytes);
        CryptographicOperations.ZeroMemory(prodBytes);
        profileAProd = await profiles.SetSecretReferenceAsync(profileAProd.Id, "api-key", prodSecret.Reference);

        await ExpectInvalidAsync(
            () => profiles.ShareAsync(new ShareAuthProfileCommand(
                profileAUat.Id, serviceB.Id, CatalogEnvironmentCodes.UatId, "synthetic-admin", string.Empty)),
            "AuthProfile sharing without explicit reason was accepted.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId) is null,
            "Rejected sharing attempt mutated target binding.");
        const string shareReason = "Explicit synthetic shared AuthProfile decision";
        var shared = await profiles.ShareAsync(new ShareAuthProfileCommand(
            profileAUat.Id, serviceB.Id, CatalogEnvironmentCodes.UatId, "synthetic-admin", shareReason));
        var sharedBinding = shared.Bindings.Single(x => x.ServiceId == serviceB.Id && x.EnvironmentId == CatalogEnvironmentCodes.UatId);
        Assert(sharedBinding.IsShared && sharedBinding.DecisionReason == shareReason && sharedBinding.DecisionBy == "synthetic-admin",
            "Explicit Shared AuthProfile decision was not persisted/auditable.");
        Assert((await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId))?.Id == profileAUat.Id,
            "Explicit Shared AuthProfile did not resolve at its exact target.");
        Assert(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.ProductionId) is not null
               && (await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.ProductionId))?.Id == profileBProd.Id,
            "Explicit UAT sharing mutated Service B Production.");

        var beforeRotation = await profiles.GetAsync(profileAUat.Id);
        var beforeSlot = beforeRotation.Secrets.Single(x => x.Name == "api-key");
        var preCache = TokenCacheIdentity.Create(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, beforeRotation.Version, beforeSlot.Generation);

        const string rotatedSentinel = "P06_ROTATED_SENTINEL_NEW_91KQ";
        var rotateBytes = Encoding.UTF8.GetBytes(rotatedSentinel);
        SecretRotationResult<SecretRef> rotationResult;
        try
        {
            var scope = SecretRotationScope.Create(
                serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, beforeSlot.Generation);
            rotationResult = await SecretRotationSafety.RotateAsync<SecretRef>(
                scope,
                static _ => ValueTask.CompletedTask,
                ct => rotation.StageAsync(scope, "api-key", rotateBytes, ct),
                (candidate, ct) => rotation.ActivateAsync(beforeSlot.Reference, "api-key", candidate, ct),
                rotation.DiscardAsync);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rotateBytes);
        }

        var afterRotation = await profiles.GetAsync(profileAUat.Id);
        var afterSlot = afterRotation.Secrets.Single(x => x.Name == "api-key");
        Assert(afterSlot.Reference == rotationResult.ActiveReference
               && afterSlot.Generation == beforeSlot.Generation + 1
               && afterRotation.Version == beforeRotation.Version + 1,
            "Successful rotation did not atomically advance current reference/generation/profile version.");
        await ExpectSecretRejectedAsync(
            () => vault.GetDescriptorAsync(beforeSlot.Reference),
            "Pre-rotation SecretRef remained active after successful activation.");
        Assert((await vault.GetDescriptorAsync(afterSlot.Reference)).Generation == afterSlot.Generation,
            "Post-rotation active SecretRef generation is inconsistent.");
        var postCache = TokenCacheIdentity.Create(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, afterRotation.Version, afterSlot.Generation);
        Assert(!string.Equals(preCache.ToCacheKey(), postCache.ToCacheKey(), StringComparison.Ordinal),
            "Pre/post rotation token-cache identity collided.");

        var staleBytes = Encoding.UTF8.GetBytes("P06_STALE_CAS_SYNTHETIC");
        var staleCandidate = await vault.CreateStagedAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key", afterSlot.Generation + 1, staleBytes);
        CryptographicOperations.ZeroMemory(staleBytes);
        var staleRefRejected = await profiles.ActivateSecretReferenceAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key",
            beforeSlot.Reference, afterSlot.Generation, staleCandidate.Reference);
        Assert(!staleRefRejected, "Stale expected current SecretRef CAS was accepted.");
        var staleGenRejected = await profiles.ActivateSecretReferenceAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key",
            afterSlot.Reference, beforeSlot.Generation, staleCandidate.Reference);
        Assert(!staleGenRejected, "Stale expected generation CAS was accepted.");
        var afterStale = await profiles.GetAsync(profileAUat.Id);
        Assert(afterStale.Secrets.Single().Reference == afterSlot.Reference
               && afterStale.Secrets.Single().Generation == afterSlot.Generation
               && afterStale.Version == afterRotation.Version,
            "Failed stale CAS mutated current secret state.");
        await vault.DiscardStagedAsync(staleCandidate.Reference);

        var crossBytes = Encoding.UTF8.GetBytes("P06_CROSS_SCOPE_ROTATION_SYNTHETIC");
        var crossCandidate = await vault.CreateStagedAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key", afterSlot.Generation + 1, crossBytes);
        CryptographicOperations.ZeroMemory(crossBytes);
        var bSlot = (await profiles.GetAsync(profileBProd.Id)).Secrets.Single(x => x.Name == "api-key");
        Assert(!await profiles.ActivateSecretReferenceAsync(
                serviceB.Id, CatalogEnvironmentCodes.ProductionId, profileBProd.Id, "api-key",
                bSlot.Reference, bSlot.Generation, crossCandidate.Reference),
            "Service B activated Service A staged secret identity.");
        var prodSlot = (await profiles.GetAsync(profileAProd.Id)).Secrets.Single(x => x.Name == "api-key");
        Assert(!await profiles.ActivateSecretReferenceAsync(
                serviceA.Id, CatalogEnvironmentCodes.ProductionId, profileAProd.Id, "api-key",
                prodSlot.Reference, prodSlot.Generation, crossCandidate.Reference),
            "Production activated UAT staged secret identity.");
        Assert((await profiles.GetAsync(profileBProd.Id)).Secrets.Single().Reference == bSlot.Reference,
            "Rejected cross-service rotation mutated Service B.");
        Assert((await profiles.GetAsync(profileAProd.Id)).Secrets.Single().Reference == prodSlot.Reference,
            "Rejected cross-environment rotation mutated Production.");
        await vault.DiscardStagedAsync(crossCandidate.Reference);

        var failureBytes = Encoding.UTF8.GetBytes("P06_ATOMIC_FAILURE_SYNTHETIC");
        var failureCandidate = await vault.CreateStagedAsync(
            serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key", afterSlot.Generation + 1, failureBytes);
        CryptographicOperations.ZeroMemory(failureBytes);
        var quotedProfileId = profileAUat.Id.ToString("D");
        await db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER [TR_P06_ForceRotationFailure] ON [AuthProfiles] AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Id] = '{quotedProfileId}') THROW 51001, 'Synthetic profile version update failure', 1; END");
        try
        {
            var safeFailure = await ExpectRotationPersistenceFailureAsync(() => profiles.ActivateSecretReferenceAsync(
                serviceA.Id, CatalogEnvironmentCodes.UatId, profileAUat.Id, "api-key",
                afterSlot.Reference, afterSlot.Generation, failureCandidate.Reference));
            Assert(safeFailure.InnerException is null
                   && !safeFailure.Message.Contains(rotatedSentinel, StringComparison.Ordinal)
                   && !safeFailure.Message.Contains("P06_ATOMIC_FAILURE_SYNTHETIC", StringComparison.Ordinal),
                "Failed rotation exposed plaintext or retained a secret-bearing inner exception.");
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS [TR_P06_ForceRotationFailure]");
        }
        var afterForcedFailure = await profiles.GetAsync(profileAUat.Id);
        Assert(afterForcedFailure.Secrets.Single().Reference == afterSlot.Reference
               && afterForcedFailure.Secrets.Single().Generation == afterSlot.Generation
               && afterForcedFailure.Version == afterRotation.Version,
            "Database failure during activation partially mutated current AuthProfile state.");
        var currentPersisted = await db.SecretVaultEntries.AsNoTracking().SingleAsync(x => x.Reference == afterSlot.Reference.Value);
        var failedPersisted = await db.SecretVaultEntries.AsNoTracking().SingleAsync(x => x.Reference == failureCandidate.Reference.Value);
        Assert(currentPersisted.State == SecretLifecycleState.Active && failedPersisted.State == SecretLifecycleState.Staged,
            "Database failure did not roll back secret lifecycle mutations atomically.");
        await vault.DiscardStagedAsync(failureCandidate.Reference);

        var forgedRef = new SecretRef($"{SecretRef.Prefix}{new string('A', SecretRef.TokenLength)}");
        await ExpectSecretRejectedAsync(() => vault.GetDescriptorAsync(forgedRef), "Forged opaque SecretRef resolved.");

        AssertNoPlaintextProperties(
            typeof(SecretDescriptor), typeof(SecretBindingScope), typeof(AuthProfileDescriptor),
            typeof(AuthProfileSecretDescriptor), typeof(AuthProfileBindingDescriptor));
        Console.WriteLine("P06_SECRET_SCOPE_OWNERSHIP=PASS");
        Console.WriteLine("P06_ROTATION_CAS_GENERATION=PASS");
        Console.WriteLine("P06_SECRET_AUTH_FOUNDATION=PASS");
        await db.Database.EnsureDeletedAsync();
    }

    await using (var cleanDb = new GsipDbContext(new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options))
    {
        await cleanDb.Database.MigrateAsync();
        Assert(await cleanDb.SecretVaultEntries.CountAsync() == 0
               && await cleanDb.AuthProfiles.CountAsync() == 0
               && await cleanDb.CatalogEnvironments.CountAsync() == 2,
            "Clean database migration did not produce expected P00-P06 schema.");
        Console.WriteLine("P06_CLEAN_DATABASE_MIGRATION=PASS");
        await cleanDb.Database.EnsureDeletedAsync();
    }
}
finally
{
    if (Directory.Exists(keyDirectory)) Directory.Delete(keyDirectory, recursive: true);
}

static ServiceInput BuildService(string code, string nameEn, string uatBaseUrl, string productionBaseUrl) =>
    new(code, "خدمة تجريبية", nameEn, "تعريف اختبار P06", "P06 synthetic metadata definition", true,
    [
        new ServiceEnvironmentInput("UAT", uatBaseUrl, "/api/sample", "POST", "application/json", "{\"X-Correlation-Mode\":\"generated\"}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null),
        new ServiceEnvironmentInput("Production", productionBaseUrl, "/api/sample", "POST", "application/json", "{\"X-Correlation-Mode\":\"generated\"}", 45, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null)
    ],
    [new ServiceFieldInput("civil-id", "الرقم المدني", "Civil ID", "text", true, "^[0-9]{12}$", null, null, 12, 12, "[]", 10, true, "Last4")],
    [new ResultMappingInput("$.data.status", "الحالة", "Status", "text", string.Empty, false, 10)]);

static async Task ExpectInvalidAsync(Func<Task> action, string message)
{
    try { await action(); }
    catch (InvalidOperationException) { return; }
    throw new InvalidOperationException(message);
}

static async Task ExpectSecretRejectedAsync(Func<Task> action, string message)
{
    try { await action(); }
    catch (SecretReferenceRejectedException) { return; }
    throw new InvalidOperationException(message);
}

static async Task<SecretReferenceRejectedException> ExpectSecretRejectedCaptureAsync(Func<Task> action, string message)
{
    try { await action(); }
    catch (SecretReferenceRejectedException exception) { return exception; }
    throw new InvalidOperationException(message);
}

static async Task<SecretRotationPersistenceException> ExpectRotationPersistenceFailureAsync(Func<Task<bool>> action)
{
    try { await action(); }
    catch (SecretRotationPersistenceException exception) { return exception; }
    throw new InvalidOperationException("Forced activation failure did not fail safely.");
}

static void AssertNoPlaintextProperties(params Type[] types)
{
    var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "Plaintext", "SecretValue", "ProtectedPayload", "Password", "Token", "SecretMaterial" };
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
