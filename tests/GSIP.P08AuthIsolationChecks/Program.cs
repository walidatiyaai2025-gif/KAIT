using System.Security.Cryptography;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Authentication;
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
        "P08-TARGET", "P08 Explicit Share Target", "https://p08-target-uat.example.invalid", "https://p08-target-prod.example.invalid"));
    var isolatedService = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P08-ISOLATED", "P08 Independently Authenticated Service", "https://p08-isolated-uat.example.invalid", "https://p08-isolated-prod.example.invalid"));
    var unboundService = await catalog.CreateServiceAsync(entity.Id, BuildService(
        "P08-UNBOUND", "P08 Unbound Service", "https://p08-unbound-uat.example.invalid", "https://p08-unbound-prod.example.invalid"));

    var provider = DataProtectionProvider.Create(
        new DirectoryInfo(keyDirectory),
        builder => builder.SetApplicationName("GSIP.P08.AuthIsolationChecks"));
    var vault = new DataProtectionSecretVault(db, provider, clock);
    var profiles = new AuthProfileService(db, vault, clock);

    var ownerFixture = await CreateSyntheticProfileAsync(
        vault,
        profiles,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        "P08 Synthetic MOJ Owner Auth");
    var isolatedFixture = await CreateSyntheticProfileAsync(
        vault,
        profiles,
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        "P08 Synthetic Independent Auth");

    // Independent configuration is the default. Merely belonging to the same Entity
    // must not create an AuthProfile relationship or allow secret material to resolve.
    Require(await profiles.ResolveAsync(unboundService.Id, CatalogEnvironmentCodes.UatId) is null,
        "An unbound service implicitly resolved another service's AuthProfile.");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        unboundService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerFixture.Profile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "An unbound service implicitly resolved another service's credentials.");

    const string shareReason = "P08 synthetic explicit shared-profile isolation probe";
    var ownerProfile = await profiles.ShareAsync(new ShareAuthProfileCommand(
        ownerFixture.Profile.Id,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        "p08-synthetic-security-check",
        shareReason));

    var sharedBinding = ownerProfile.Bindings.Single(binding =>
        binding.ServiceId == targetService.Id
        && binding.EnvironmentId == CatalogEnvironmentCodes.UatId);
    Require(sharedBinding.IsShared && sharedBinding.DecisionReason == shareReason,
        "Synthetic shared AuthProfile decision was not explicit and auditable.");

    await AssertSecretDigestAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        ownerFixture.Secret.Reference,
        ownerFixture.ExpectedDigest,
        "Owner Service + UAT scope could not resolve its exact configured secret.");
    await AssertSecretDigestAsync(
        vault,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        ownerFixture.Secret.Reference,
        ownerFixture.ExpectedDigest,
        "Explicit shared binding did not resolve through the exact configured Service + Environment scope.");
    await AssertSecretDigestAsync(
        vault,
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        isolatedFixture.Profile.Id,
        isolatedFixture.Secret.Reference,
        isolatedFixture.ExpectedDigest,
        "Independent service could not resolve its own exact configured secret.");

    // Forged AuthProfile IDs fail closed before any secret material reaches the callback.
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        Guid.NewGuid(),
        "api-key",
        ownerFixture.Secret.Reference,
        "A forged AuthProfile identifier resolved owner secret material.");

    // A syntactically valid but unknown SecretRef must not be treated as a fallback.
    var forgedReference = new SecretRef($"{SecretRef.Prefix}{new string('A', SecretRef.TokenLength)}");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        forgedReference,
        "A forged SecretRef resolved secret material.");

    // Secret slots are exact. A valid reference cannot be reused under a different secret name.
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "consumer-secret",
        ownerFixture.Secret.Reference,
        "A SecretRef was accepted under the wrong secret slot name.");

    // A service that has its own valid profile cannot borrow another service's SecretRef.
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        isolatedFixture.Profile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "Cross-service credential reuse was accepted despite independent AuthProfiles.");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "Cross-service AuthProfile fallback was accepted without an explicit shared binding.");

    // Production must never fall back to a UAT AuthProfile or UAT SecretRef.
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.ProductionId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "Production resolved a UAT AuthProfile/SecretRef fallback.");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        targetService.Id,
        CatalogEnvironmentCodes.ProductionId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "Production on an explicitly shared UAT service resolved UAT credentials.");

    // Inactive ServiceEnvironmentConfig is not a legal authentication binding.
    var ownerConfig = await db.ServiceEnvironmentConfigs.SingleAsync(config =>
        config.ServiceId == ownerService.Id
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    ownerConfig.Active = false;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "An inactive ServiceEnvironmentConfig resolved secret material.");
    ownerConfig = await db.ServiceEnvironmentConfigs.SingleAsync(config =>
        config.ServiceId == ownerService.Id
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    ownerConfig.Active = true;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    // Disabled/stale AuthProfiles cannot resolve their otherwise valid SecretRefs.
    ownerProfile = await profiles.SetEnabledAsync(ownerProfile.Id, false);
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        ownerService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "A disabled AuthProfile resolved secret material.");
    ownerProfile = await profiles.SetEnabledAsync(ownerProfile.Id, true);

    await VerifyTokenCacheScopeIsolationAsync(
        ownerService.Id,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        CatalogEnvironmentCodes.ProductionId,
        ownerProfile.Id,
        ownerProfile.Version,
        ownerFixture.Secret.Generation);

    // Preserve the binding but remove the canonical ServiceEnvironmentConfig selection.
    // This is the stale/forged relational state that motivated the production guard.
    var targetConfig = await db.ServiceEnvironmentConfigs.SingleAsync(config =>
        config.ServiceId == targetService.Id
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    Require(targetConfig.AuthProfileId == ownerProfile.Id,
        "Synthetic target configuration did not contain the shared AuthProfile before the stale-binding probe.");
    targetConfig.AuthProfileId = null;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    Require(await db.AuthProfileBindings.AsNoTracking().AnyAsync(binding =>
            binding.AuthProfileId == ownerProfile.Id
            && binding.ServiceId == targetService.Id
            && binding.EnvironmentId == CatalogEnvironmentCodes.UatId),
        "Stale-binding probe did not preserve the persisted AuthProfileBinding.");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "A stale AuthProfile binding resolved secret material after canonical configuration stopped selecting it.");

    // Restore canonical selection, then remove the binding. Both halves are mandatory;
    // either side missing must fail closed.
    targetConfig = await db.ServiceEnvironmentConfigs.SingleAsync(config =>
        config.ServiceId == targetService.Id
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId);
    targetConfig.AuthProfileId = ownerProfile.Id;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var removedBindings = await db.AuthProfileBindings
        .Where(binding => binding.AuthProfileId == ownerProfile.Id
                          && binding.ServiceId == targetService.Id
                          && binding.EnvironmentId == CatalogEnvironmentCodes.UatId)
        .ExecuteDeleteAsync();
    Require(removedBindings == 1, "Missing-binding probe did not remove exactly one explicit shared binding.");
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        targetService.Id,
        CatalogEnvironmentCodes.UatId,
        ownerProfile.Id,
        "api-key",
        ownerFixture.Secret.Reference,
        "ServiceEnvironmentConfig alone resolved credentials after its AuthProfileBinding was removed.");

    // A formerly valid reference becomes stale immediately after revocation.
    await vault.RevokeAsync(
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        isolatedFixture.Profile.Id,
        "api-key",
        isolatedFixture.Secret.Reference);
    await ExpectSecretRejectedWithoutMaterialAsync(
        vault,
        isolatedService.Id,
        CatalogEnvironmentCodes.UatId,
        isolatedFixture.Profile.Id,
        "api-key",
        isolatedFixture.Secret.Reference,
        "A revoked/stale SecretRef still resolved secret material.");

    Console.WriteLine("P08_AUTH_SCOPE_ISOLATION=PASS");
    await db.Database.EnsureDeletedAsync();
}
finally
{
    if (Directory.Exists(keyDirectory))
        Directory.Delete(keyDirectory, recursive: true);
}

static async Task<SyntheticProfileFixture> CreateSyntheticProfileAsync(
    DataProtectionSecretVault vault,
    AuthProfileService profiles,
    Guid serviceId,
    Guid environmentId,
    string profileName)
{
    var syntheticSecret = RandomNumberGenerator.GetBytes(48);
    var expectedDigest = SHA256.HashData(syntheticSecret);
    SecretDescriptor secret;
    try
    {
        secret = await vault.CreateActiveAsync(
            serviceId,
            environmentId,
            null,
            "api-key",
            syntheticSecret);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(syntheticSecret);
    }

    var profile = await profiles.CreateAsync(new CreateAuthProfileCommand(
        serviceId,
        environmentId,
        profileName,
        AuthProfileType.ApiKeyPlusBearer,
        "p08-synthetic-security-check",
        new Dictionary<string, SecretRef> { ["api-key"] = secret.Reference }));

    return new SyntheticProfileFixture(profile, secret, expectedDigest);
}

static async Task AssertSecretDigestAsync(
    DataProtectionSecretVault vault,
    Guid serviceId,
    Guid environmentId,
    Guid authProfileId,
    SecretRef secretRef,
    byte[] expectedDigest,
    string failureMessage)
{
    var actualDigest = await vault.UseSecretAsync(
        serviceId,
        environmentId,
        authProfileId,
        "api-key",
        secretRef,
        static (material, _) => ValueTask.FromResult(SHA256.HashData(material.Span)));
    try
    {
        Require(CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest), failureMessage);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(actualDigest);
    }
}

static async Task ExpectSecretRejectedWithoutMaterialAsync(
    DataProtectionSecretVault vault,
    Guid serviceId,
    Guid environmentId,
    Guid authProfileId,
    string secretName,
    SecretRef secretRef,
    string failureMessage)
{
    var materialObserved = false;
    try
    {
        await vault.UseSecretAsync(
            serviceId,
            environmentId,
            authProfileId,
            secretName,
            secretRef,
            (_, _) =>
            {
                materialObserved = true;
                return ValueTask.FromResult(true);
            });
    }
    catch (SecretReferenceRejectedException exception)
    {
        Require(!materialObserved, "Rejected authentication scope exposed secret material before failing closed.");
        Require(exception.Message == "Secret reference is invalid, inactive, stale, or outside the requested security scope.",
            "Secret rejection did not use the canonical non-sensitive failure message.");
        return;
    }

    throw new InvalidOperationException(failureMessage);
}

static async Task VerifyTokenCacheScopeIsolationAsync(
    Guid ownerServiceId,
    Guid sharedTargetServiceId,
    Guid uatEnvironmentId,
    Guid productionEnvironmentId,
    Guid authProfileId,
    long authProfileVersion,
    int secretGeneration)
{
    var clock = CacheClock.Create();
    using var cache = new InMemoryTokenCache(clock, new TokenCacheOptions(TimeSpan.FromSeconds(30)));
    var baseIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-moj-audience",
        ["moj.read"]);
    var refreshes = 0;

    Task<TokenCacheValue> Refresh(string token)
    {
        Interlocked.Increment(ref refreshes);
        return Task.FromResult(TokenCacheValue.Create(token, clock.UtcNow.AddMinutes(5)));
    }

    var ownerToken = await cache.GetOrRefreshAsync(baseIdentity, _ => Refresh("p08-synthetic-owner-token"));
    var ownerHit = await cache.GetOrRefreshAsync(baseIdentity, _ => Refresh("must-not-refresh"));
    Require(refreshes == 1 && ownerHit.AccessToken == ownerToken.AccessToken,
        "Exact token-cache identity did not reuse its own safely valid synthetic token.");

    var sharedTargetIdentity = TokenCacheIdentity.Create(
        sharedTargetServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-moj-audience",
        ["moj.read"]);
    var targetToken = await cache.GetOrRefreshAsync(sharedTargetIdentity, _ => Refresh("p08-synthetic-shared-target-token"));
    Require(refreshes == 2 && targetToken.AccessToken != ownerToken.AccessToken,
        "A cached token crossed ServiceId boundaries even for an explicitly shared AuthProfile.");

    var productionIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        productionEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-moj-audience",
        ["moj.read"]);
    var productionToken = await cache.GetOrRefreshAsync(productionIdentity, _ => Refresh("p08-synthetic-production-token"));
    Require(refreshes == 3 && productionToken.AccessToken != ownerToken.AccessToken,
        "A cached UAT token was reused for Production.");

    var newerProfileIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion + 1,
        secretGeneration,
        "p08-synthetic-moj-audience",
        ["moj.read"]);
    var newerProfileToken = await cache.GetOrRefreshAsync(newerProfileIdentity, _ => Refresh("p08-synthetic-profile-version-token"));
    Require(refreshes == 4 && newerProfileToken.AccessToken != ownerToken.AccessToken,
        "A token cached under a stale AuthProfile version was reused after the profile version changed.");

    var newerGenerationIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration + 1,
        "p08-synthetic-moj-audience",
        ["moj.read"]);
    var newerGenerationToken = await cache.GetOrRefreshAsync(newerGenerationIdentity, _ => Refresh("p08-synthetic-secret-generation-token"));
    Require(refreshes == 5 && newerGenerationToken.AccessToken != ownerToken.AccessToken,
        "A token cached under a stale secret generation was reused after credential generation changed.");

    var wrongScopeIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-moj-audience",
        ["moj.write"]);
    var wrongScopeToken = await cache.GetOrRefreshAsync(wrongScopeIdentity, _ => Refresh("p08-synthetic-wrong-scope-token"));
    Require(refreshes == 6 && wrongScopeToken.AccessToken != ownerToken.AccessToken,
        "A cached token crossed an effective scope boundary.");

    // Expiry safety must treat a near-expiry cached token as stale and refresh it.
    var expiringIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-expiry-audience",
        ["moj.read"]);
    var expiryRefreshes = 0;
    Task<TokenCacheValue> ExpiryRefresh(CancellationToken _)
    {
        var call = Interlocked.Increment(ref expiryRefreshes);
        var lifetime = call == 1 ? TimeSpan.FromSeconds(40) : TimeSpan.FromMinutes(5);
        return Task.FromResult(TokenCacheValue.Create($"p08-synthetic-expiry-token-{call}", clock.UtcNow.Add(lifetime)));
    }
    var expiring = await cache.GetOrRefreshAsync(expiringIdentity, ExpiryRefresh);
    clock.Advance(TimeSpan.FromSeconds(11));
    var refreshedExpiry = await cache.GetOrRefreshAsync(expiringIdentity, ExpiryRefresh);
    Require(expiryRefreshes == 2 && expiring.AccessToken != refreshedExpiry.AccessToken,
        "A near-expiry cached token survived inside the configured safety window.");

    // Same exact identity is single-flight under concurrent acquisition.
    var concurrentIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-concurrency-audience",
        ["moj.read"]);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var concurrentRefreshes = 0;
    async Task<TokenCacheValue> ConcurrentRefresh(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref concurrentRefreshes);
        started.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
        return TokenCacheValue.Create("p08-synthetic-single-flight-token", clock.UtcNow.AddMinutes(5));
    }
    var waiters = Enumerable.Range(0, 16)
        .Select(_ => cache.GetOrRefreshAsync(concurrentIdentity, ConcurrentRefresh))
        .ToArray();
    await started.Task;
    release.TrySetResult();
    var concurrentValues = await Task.WhenAll(waiters);
    Require(concurrentRefreshes == 1,
        "Concurrent token acquisition executed more than one refresh for the same exact auth scope.");
    Require(concurrentValues.All(value => value.AccessToken == "p08-synthetic-single-flight-token"),
        "Single-flight callers did not receive one exact scoped synthetic token result.");

    // A failed acquisition must not poison/populate the cache; the next attempt retries.
    var failureIdentity = TokenCacheIdentity.Create(
        ownerServiceId,
        uatEnvironmentId,
        authProfileId,
        authProfileVersion,
        secretGeneration,
        "p08-synthetic-failure-audience",
        ["moj.read"]);
    var failureRefreshes = 0;
    async Task<TokenCacheValue> FailureThenRecover(CancellationToken _)
    {
        await Task.Yield();
        if (Interlocked.Increment(ref failureRefreshes) == 1)
            throw new InvalidOperationException("Synthetic P08 token refresh failure.");
        return TokenCacheValue.Create("p08-synthetic-recovered-token", clock.UtcNow.AddMinutes(5));
    }
    await ExpectThrowsAsync<InvalidOperationException>(() => cache.GetOrRefreshAsync(failureIdentity, FailureThenRecover));
    var recovered = await cache.GetOrRefreshAsync(failureIdentity, FailureThenRecover);
    Require(failureRefreshes == 2 && recovered.AccessToken == "p08-synthetic-recovered-token",
        "Failed token acquisition was cached or prevented a clean retry.");
}

static ServiceInput BuildService(string code, string nameEn, string uatBaseUrl, string productionBaseUrl) =>
    new(code, "خدمة تجريبية", nameEn, "تعريف اختبار P08", "P08 synthetic auth-isolation definition", true,
    [
        new ServiceEnvironmentInput("UAT", uatBaseUrl, "/synthetic/token", "POST", "application/x-www-form-urlencoded", "{}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null),
        new ServiceEnvironmentInput("Production", productionBaseUrl, "/synthetic/token", "POST", "application/x-www-form-urlencoded", "{}", 30, "SystemDefault", true, string.Empty, "/health", "HEAD", true, null)
    ],
    [],
    []);

static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record SyntheticProfileFixture(
    AuthProfileDescriptor Profile,
    SecretDescriptor Secret,
    byte[] ExpectedDigest);

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

sealed class CacheClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public static CacheClock Create() =>
        new(new DateTimeOffset(2026, 9, 9, 4, 30, 0, TimeSpan.Zero));

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
