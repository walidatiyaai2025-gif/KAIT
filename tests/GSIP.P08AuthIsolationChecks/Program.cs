using System.Security.Cryptography;
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

var connection = Environment.GetEnvironmentVariable("GSIP_P08_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P08_SQL is required.");

var keyDirectory = Path.Combine(Path.GetTempPath(), $"gsip-p08-auth-isolation-{Guid.NewGuid():N}");
Directory.CreateDirectory(keyDirectory);

try
{
    var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
    await using var db = new GsipDbContext(options);
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();

    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-09T04:30:00Z"));
    var catalog = new MetadataCatalogService(db, clock);
    var entity = await catalog.CreateEntityAsync(new EntityInput(
        "P08-AUTH-ISO", "جهة اختبار عزل المصادقة", "P08 Auth Isolation Authority", string.Empty, true, 80));
    var ownerService = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P08-OWNER", "P08 Owner Service", "https://p08-owner-uat.example.invalid", "https://p08-owner-prod.example.invalid"));
    var targetService = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P08-TARGET", "P08 Target Service", "https://p08-target-uat.example.invalid", "https://p08-target-prod.example.invalid"));

    var provider = DataProtectionProvider.Create(
        new DirectoryInfo(keyDirectory),
        builder => builder.SetApplicationName("GSIP.P08.AuthIsolationChecks"));
    var vault = new DataProtectionSecretVault(db, provider, clock);
    var profiles = new AuthProfileService(db, vault, clock);

    var syntheticSecret = RandomNumberGenerator.GetBytes(48);
    var expectedDigest = SHA256.HashData(syntheticSecret);
    SecretDescriptor secret;
    try
    {
        secret = await vault.CreateActiveAsync(
            ownerService.Id,
            CatalogEnvironmentCodes.UatId,
            null,
            "api-key",
            syntheticSecret);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(syntheticSecret);
    }

    var profile = await profiles.CreateAsync(new CreateAuthProfileCommand(
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        "P08 Synthetic MOJ Auth",
        AuthProfileType.ApiKeyPlusBearer,
        "p08-synthetic-security-check",
        new Dictionary<string, SecretRef> { ["api-key"] = secret.Reference }));

    const string shareReason = "P08 synthetic explicit shared-profile isolation probe";
    profile = await profiles.ShareAsync(new ShareAuthProfileCommand(
        profile.Id,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        "p08-synthetic-security-check",
        shareReason));

    var sharedBinding = profile.Bindings.Single(binding =>
        binding.ServiceId == targetService.Id
        && binding.EnvironmentId == CatalogEnvironmentCodes.UatId);
    Require(sharedBinding.IsShared && sharedBinding.DecisionReason == shareReason,
        "Synthetic shared AuthProfile decision was not explicit and auditable.");

    var legalDigest = await vault.UseSecretAsync(
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        profile.Id,
        "api-key",
        secret.Reference,
        static (material, _) => ValueTask.FromResult(SHA256.HashData(material.Span)));
    Require(CryptographicOperations.FixedTimeEquals(legalDigest, expectedDigest),
        "Explicit shared binding did not resolve through the exact configured Service + Environment scope.");
    CryptographicOperations.ZeroMemory(legalDigest);

    var targetConfig = await db.ServiceEnvironmentConfigs.SingleAsync(config =>
        config.ServiceId == targetService.Id
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    Require(targetConfig.AuthProfileId == profile.Id,
        "Synthetic target configuration did not contain the shared AuthProfile before the negative probe.");

    // Simulate stale/forged relational state: the old binding remains, but the canonical
    // ServiceEnvironmentConfig no longer selects this AuthProfile. Secret resolution must
    // fail closed before exposing material to a P08 token request path.
    targetConfig.AuthProfileId = null;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    Require(await db.AuthProfileBindings.AsNoTracking().AnyAsync(binding =>
            binding.AuthProfileId == profile.Id
            && binding.ServiceId == targetService.Id
            && binding.EnvironmentId == CatalogEnvironmentCodes.UatId),
        "Negative probe did not preserve the stale binding needed to test fail-closed isolation.");

    await ExpectSecretRejectedAsync(
        () => vault.UseSecretAsync(
            targetService.Id,
            CatalogEnvironmentCodes.UatId,
            profile.Id,
            "api-key",
            secret.Reference,
            static (_, _) => ValueTask.FromResult(true)),
        "Stale AuthProfile binding resolved secret material after canonical ServiceEnvironmentConfig stopped selecting that profile.");

    Console.WriteLine("P08_AUTH_SCOPE_ISOLATION=PASS");
    await db.Database.EnsureDeletedAsync();
}
finally
{
    if (Directory.Exists(keyDirectory))
        Directory.Delete(keyDirectory, recursive: true);
}

static ServiceInput BuildService(string code, string nameEn, string uatBaseUrl, string productionBaseUrl) =>
    new(code, "خدمة تجريبية", nameEn, "تعريف اختبار P08", "P08 synthetic auth-isolation definition", true,
    [
        new ServiceEnvironmentInput("UAT", uatBaseUrl, "/synthetic/token", "POST", "application/x-www-form-urlencoded", "{}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null),
        new ServiceEnvironmentInput("Production", productionBaseUrl, "/synthetic/token", "POST", "application/x-www-form-urlencoded", "{}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null)
    ],
    [],
    []);

static async Task ExpectSecretRejectedAsync(Func<Task> action, string failureMessage)
{
    try
    {
        await action();
    }
    catch (SecretReferenceRejectedException exception)
    {
        Require(exception.Message == "Secret reference is invalid, inactive, stale, or outside the requested security scope.",
            "Secret rejection did not use the canonical non-sensitive failure message.");
        return;
    }

    throw new InvalidOperationException(failureMessage);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
