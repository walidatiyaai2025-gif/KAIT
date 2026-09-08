using System.Security.Cryptography;
using GSIP.Application.Abstractions;
using GSIP.Application.Secrets;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Secrets;

public sealed class DataProtectionSecretVault(
    GsipDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    ISystemClock clock) : ISecretVault, ISecretMaterialResolver
{
    private const int MaximumSecretBytes = 65_536;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("GSIP.SecretVault.v1");

    public Task<SecretDescriptor> CreateActiveAsync(
        Guid serviceId,
        Guid environmentId,
        Guid? authProfileId,
        string secretName,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default) =>
        CreateAsync(serviceId, environmentId, authProfileId, secretName, 1, SecretLifecycleState.Active, secretMaterial, cancellationToken);

    public async Task<SecretDescriptor> CreateStagedAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        int generation,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSecretName(secretName);
        if (generation < 2)
            throw new SecretReferenceRejectedException();

        await ValidateOwnerScopeAsync(serviceId, environmentId, authProfileId, cancellationToken);
        var current = await dbContext.AuthProfileSecrets.AsNoTracking().SingleOrDefaultAsync(
            x => x.AuthProfileId == authProfileId && x.SecretName == normalizedName,
            cancellationToken);
        if (current is null || current.Generation + 1 != generation)
            throw new SecretReferenceRejectedException();

        return await CreateAsync(
            serviceId,
            environmentId,
            authProfileId,
            normalizedName,
            generation,
            SecretLifecycleState.Staged,
            secretMaterial,
            cancellationToken);
    }

    public async Task<SecretDescriptor> ClaimAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSecretName(secretName);
        await ValidateOwnerScopeAsync(serviceId, environmentId, authProfileId, cancellationToken);

        var existing = await dbContext.SecretVaultEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.Reference == secretRef.Value,
            cancellationToken);
        if (existing is null
            || existing.State != SecretLifecycleState.Active
            || existing.OwnerServiceId != serviceId
            || existing.OwnerEnvironmentId != environmentId
            || !string.Equals(existing.SecretName, normalizedName, StringComparison.Ordinal)
            || (existing.OwnerAuthProfileId.HasValue && existing.OwnerAuthProfileId != authProfileId))
            throw new SecretReferenceRejectedException();

        if (!existing.OwnerAuthProfileId.HasValue)
        {
            var claimed = await dbContext.SecretVaultEntries
                .Where(x => x.Reference == secretRef.Value
                            && x.State == SecretLifecycleState.Active
                            && x.OwnerServiceId == serviceId
                            && x.OwnerEnvironmentId == environmentId
                            && x.OwnerAuthProfileId == null
                            && x.SecretName == normalizedName)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.OwnerAuthProfileId, authProfileId),
                    cancellationToken);
            if (claimed != 1)
                throw new SecretReferenceRejectedException();
        }

        var result = await dbContext.SecretVaultEntries.AsNoTracking().SingleAsync(
            x => x.Reference == secretRef.Value,
            cancellationToken);
        if (result.OwnerAuthProfileId != authProfileId)
            throw new SecretReferenceRejectedException();
        return ToDescriptor(result);
    }

    public async Task<SecretDescriptor> GetDescriptorAsync(
        SecretRef secretRef,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.SecretVaultEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Reference == secretRef.Value, cancellationToken);
        if (entry is null || entry.State != SecretLifecycleState.Active)
            throw new SecretReferenceRejectedException();
        return ToDescriptor(entry);
    }

    public async Task DiscardStagedAsync(SecretRef secretRef, CancellationToken cancellationToken = default)
    {
        var changed = await dbContext.SecretVaultEntries
            .Where(x => x.Reference == secretRef.Value && x.State == SecretLifecycleState.Staged)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.State, SecretLifecycleState.Revoked)
                    .SetProperty(x => x.RevokedAtUtc, clock.UtcNow),
                cancellationToken);
        if (changed == 0)
        {
            var state = await dbContext.SecretVaultEntries.AsNoTracking()
                .Where(x => x.Reference == secretRef.Value)
                .Select(x => (SecretLifecycleState?)x.State)
                .SingleOrDefaultAsync(cancellationToken);
            if (state != SecretLifecycleState.Revoked)
                throw new SecretReferenceRejectedException();
        }
    }

    public async Task<SecretDescriptor> RevokeAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSecretName(secretName);
        var entry = await dbContext.SecretVaultEntries.SingleOrDefaultAsync(
            x => x.Reference == secretRef.Value
                 && x.OwnerServiceId == serviceId
                 && x.OwnerEnvironmentId == environmentId
                 && x.OwnerAuthProfileId == authProfileId
                 && x.SecretName == normalizedName,
            cancellationToken);
        if (entry is null || entry.State != SecretLifecycleState.Active)
            throw new SecretReferenceRejectedException();

        entry.State = SecretLifecycleState.Revoked;
        entry.RevokedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDescriptor(entry);
    }

    public async Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var normalizedName = NormalizeSecretName(secretName);
        if (serviceId == Guid.Empty || environmentId == Guid.Empty || authProfileId == Guid.Empty)
            throw new SecretReferenceRejectedException();

        var bindingAllowed = await dbContext.AuthProfileBindings.AsNoTracking().AnyAsync(
            x => x.AuthProfileId == authProfileId
                 && x.ServiceId == serviceId
                 && x.EnvironmentId == environmentId,
            cancellationToken);
        var profileEnabled = await dbContext.AuthProfiles.AsNoTracking().AnyAsync(
            x => x.Id == authProfileId && x.IsEnabled,
            cancellationToken);
        if (!bindingAllowed || !profileEnabled)
            throw new SecretReferenceRejectedException();

        var entry = await dbContext.SecretVaultEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.Reference == secretRef.Value
                 && x.State == SecretLifecycleState.Active
                 && x.OwnerAuthProfileId == authProfileId
                 && x.SecretName == normalizedName,
            cancellationToken);
        if (entry is null)
            throw new SecretReferenceRejectedException();

        byte[] cleartext;
        try
        {
            cleartext = _protector.Unprotect(entry.ProtectedPayload);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SecretReferenceRejectedException();
        }

        try
        {
            return await operation(cleartext, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cleartext);
        }
    }

    private async Task<SecretDescriptor> CreateAsync(
        Guid serviceId,
        Guid environmentId,
        Guid? authProfileId,
        string secretName,
        int generation,
        SecretLifecycleState state,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty || environmentId == Guid.Empty)
            throw new SecretReferenceRejectedException();
        var normalizedName = NormalizeSecretName(secretName);
        if (secretMaterial.IsEmpty || secretMaterial.Length > MaximumSecretBytes)
            throw new ArgumentException("Secret material length is outside the allowed range.", nameof(secretMaterial));

        var configExists = await dbContext.ServiceEnvironmentConfigs.AsNoTracking().AnyAsync(
            x => x.ServiceId == serviceId && x.EnvironmentId == environmentId,
            cancellationToken);
        if (!configExists)
            throw new SecretReferenceRejectedException();
        if (authProfileId.HasValue)
            await ValidateOwnerScopeAsync(serviceId, environmentId, authProfileId.Value, cancellationToken);

        var working = secretMaterial.ToArray();
        byte[] protectedPayload;
        try
        {
            protectedPayload = _protector.Protect(working);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SecretProtectionException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(working);
        }

        var secretRef = await CreateUniqueReferenceAsync(cancellationToken);
        var entry = new SecretVaultEntry
        {
            Reference = secretRef.Value,
            ProtectedPayload = protectedPayload,
            State = state,
            Generation = generation,
            OwnerServiceId = serviceId,
            OwnerEnvironmentId = environmentId,
            OwnerAuthProfileId = authProfileId,
            SecretName = normalizedName,
            CreatedAtUtc = clock.UtcNow
        };
        dbContext.SecretVaultEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDescriptor(entry);
    }

    private async Task ValidateOwnerScopeAsync(Guid serviceId, Guid environmentId, Guid authProfileId, CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty || environmentId == Guid.Empty || authProfileId == Guid.Empty)
            throw new SecretReferenceRejectedException();
        var valid = await dbContext.AuthProfiles.AsNoTracking().AnyAsync(
            x => x.Id == authProfileId
                 && x.OwnerServiceId == serviceId
                 && x.OwnerEnvironmentId == environmentId,
            cancellationToken);
        if (!valid)
            throw new SecretReferenceRejectedException();
    }

    private async Task<SecretRef> CreateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            CryptographicOperations.ZeroMemory(bytes);
            var candidate = new SecretRef($"{SecretRef.Prefix}{token}");
            if (!await dbContext.SecretVaultEntries.AsNoTracking().AnyAsync(x => x.Reference == candidate.Value, cancellationToken))
                return candidate;
        }
        throw new InvalidOperationException("Unable to allocate an opaque secret reference.");
    }

    private static string NormalizeSecretName(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > 80 || normalized.Any(char.IsControl))
            throw new SecretReferenceRejectedException();
        return normalized;
    }

    private static SecretDescriptor ToDescriptor(SecretVaultEntry entry) =>
        new(
            new SecretRef(entry.Reference),
            entry.State,
            entry.Generation,
            entry.OwnerServiceId,
            entry.OwnerEnvironmentId,
            entry.OwnerAuthProfileId,
            entry.SecretName,
            entry.CreatedAtUtc,
            entry.RevokedAtUtc);
}
