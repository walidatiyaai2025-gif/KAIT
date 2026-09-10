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

var connection = Environment.GetEnvironmentVariable("GSIP_P17_SQL");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("GSIP_P17_SQL is required.");

var keyDirectory = Path.Combine(Path.GetTempPath(), $"gsip-p17-auth-share-keys-{Guid.NewGuid():N}");
Directory.CreateDirectory(keyDirectory);

try
{
    var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
    await using var db = new GsipDbContext(options);
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();

    var clock = new FixedClock(DateTimeOffset.Parse("2026-09-10T17:30:00Z"));
    var catalog = new MetadataCatalogService(db, clock);
    var entity = await catalog.CreateEntityAsync(new EntityInput(
        "P17-AUTH-SHARE", "جهة اختبار عزل المصادقة", "P17 Auth Isolation Authority", string.Empty, true, 10));
    var serviceA = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P17-AUTH-A", "P17 Auth Service A", "https://service-a-uat.example.invalid", "https://service-a-prod.example.invalid"));
    var serviceB = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P17-AUTH-B", "P17 Auth Service B", "https://service-b-uat.example.invalid", "https://service-b-prod.example.invalid"));

    var provider = DataProtectionProvider.Create(
        new DirectoryInfo(keyDirectory),
        builder => builder.SetApplicationName("GSIP.P17.AuthProfileIsolationChecks"));
    var vault = new DataProtectionSecretVault(db, provider, clock);
    var profiles = new AuthProfileService(db, vault, clock);

    var secretBytes = Encoding.UTF8.GetBytes("P17_SYNTHETIC_UAT_SECRET_NOT_LIVE");
    SecretDescriptor secret;
    try
    {
        secret = await vault.CreateActiveAsync(
            serviceA.Id,
            CatalogEnvironmentCodes.UatId,
            null,
            "api-key",
            secretBytes);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secretBytes);
    }

    var uatProfile = await profiles.CreateAsync(new CreateAuthProfileCommand(
        serviceA.Id,
        CatalogEnvironmentCodes.UatId,
        "Service A UAT",
        AuthProfileType.ApiKeyHeader,
        "p17-acceptance",
        new Dictionary<string, SecretRef> { ["api-key"] = secret.Reference }));

    var productionBefore = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleAsync(
        x => x.ServiceId == serviceB.Id && x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(productionBefore.AuthProfileId is null, "Synthetic Production target must start unbound.");

    await ExpectInvalidAsync(
        () => profiles.ShareAsync(new ShareAuthProfileCommand(
            uatProfile.Id,
            serviceB.Id,
            CatalogEnvironmentCodes.ProductionId,
            "p17-acceptance",
            "cross-environment sharing must fail closed")),
        "UAT AuthProfile was shareable into an unbound Production target.");

    db.ChangeTracker.Clear();
    var productionAfter = await db.ServiceEnvironmentConfigs.AsNoTracking().SingleAsync(
        x => x.ServiceId == serviceB.Id && x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Check(productionAfter.AuthProfileId is null, "Rejected UAT-to-Production share mutated Production configuration.");
    Check(!await db.AuthProfileBindings.AsNoTracking().AnyAsync(
            x => x.AuthProfileId == uatProfile.Id
                 && x.ServiceId == serviceB.Id
                 && x.EnvironmentId == CatalogEnvironmentCodes.ProductionId),
        "Rejected UAT-to-Production share persisted an AuthProfile binding.");
    Check(await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.ProductionId) is null,
        "Rejected UAT-to-Production share became resolvable at runtime.");

    const string sameEnvironmentReason = "explicit same-UAT shared profile acceptance";
    var shared = await profiles.ShareAsync(new ShareAuthProfileCommand(
        uatProfile.Id,
        serviceB.Id,
        CatalogEnvironmentCodes.UatId,
        "p17-acceptance",
        sameEnvironmentReason));
    var sharedBinding = shared.Bindings.Single(
        x => x.ServiceId == serviceB.Id && x.EnvironmentId == CatalogEnvironmentCodes.UatId);
    Check(sharedBinding.IsShared && sharedBinding.DecisionReason == sameEnvironmentReason,
        "Legitimate explicit same-environment AuthProfile sharing regressed.");
    Check((await profiles.ResolveAsync(serviceB.Id, CatalogEnvironmentCodes.UatId))?.Id == uatProfile.Id,
        "Legitimate same-UAT shared profile did not resolve at its exact target.");

    Console.WriteLine("P17_AUTH_PROFILE_CROSS_ENVIRONMENT_SHARE=REJECTED");
    Console.WriteLine("P17_AUTH_PROFILE_SAME_ENVIRONMENT_SHARE=PASS");
    await db.Database.EnsureDeletedAsync();
}
finally
{
    if (Directory.Exists(keyDirectory))
        Directory.Delete(keyDirectory, recursive: true);
}

static ServiceInput BuildService(string code, string nameEn, string uatBaseUrl, string productionBaseUrl) =>
    new(code, "خدمة تجريبية", nameEn, "تعريف اختبار P17", "P17 synthetic metadata definition", true,
    [
        new ServiceEnvironmentInput("UAT", uatBaseUrl, "/api/sample", "POST", "application/json", "{}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null),
        new ServiceEnvironmentInput("Production", productionBaseUrl, "/api/sample", "POST", "application/json", "{}", 45, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null)
    ],
    [new ServiceFieldInput("synthetic-id", "معرف تجريبي", "Synthetic ID", "text", true, "^[A-Z0-9-]{1,32}$", null, null, 1, 32, "[]", 10, false, "None")],
    [new ResultMappingInput("$.data.status", "الحالة", "Status", "text", string.Empty, false, 10)]);

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

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
