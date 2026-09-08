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

    public async Task<SecretDescriptor> CreateActiveAsync(
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default)
    {
        if (secretMaterial.IsEmpty || secretMaterial.Length > MaximumSecretBytes)
            throw new ArgumentException("Secret material length is outside the allowed range.", nameof(secretMaterial));

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
        var now = clock.UtcNow;
        var entry = new SecretVaultEntry
        {
            Reference = secretRef.Value,
            ProtectedPayload = protectedPayload,
            State = SecretLifecycleState.Active,
            Generation = 1,
            CreatedAtUtc = now
        };

        dbContext.SecretVaultEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDescriptor(entry);
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

    public async Task<SecretDescriptor> RevokeAsync(
        SecretRef secretRef,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.SecretVaultEntries
            .SingleOrDefaultAsync(x => x.Reference == secretRef.Value, cancellationToken);

        if (entry is null || entry.State != SecretLifecycleState.Active)
            throw new SecretReferenceRejectedException();

        entry.State = SecretLifecycleState.Revoked;
        entry.RevokedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDescriptor(entry);
    }

    public async Task<TResult> UseSecretAsync<TResult>(
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var entry = await dbContext.SecretVaultEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Reference == secretRef.Value, cancellationToken);
        if (entry is null || entry.State != SecretLifecycleState.Active)
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

    private async Task<SecretRef> CreateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            CryptographicOperations.ZeroMemory(bytes);
            var candidate = new SecretRef($"{SecretRef.Prefix}{token}");
            if (!await dbContext.SecretVaultEntries.AsNoTracking()
                    .AnyAsync(x => x.Reference == candidate.Value, cancellationToken))
                return candidate;
        }

        throw new InvalidOperationException("Unable to allocate an opaque secret reference.");
    }

    private static SecretDescriptor ToDescriptor(SecretVaultEntry entry) =>
        new(new SecretRef(entry.Reference), entry.State, entry.Generation, entry.CreatedAtUtc, entry.RevokedAtUtc);
}
