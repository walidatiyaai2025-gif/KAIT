using System.Data;
using GSIP.Application.Abstractions;
using GSIP.Application.Secrets;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Secrets;

public sealed class AuthProfileService(
    GsipDbContext dbContext,
    ISecretVault secretVault,
    ISystemClock clock) : IAuthProfileService
{
    public async Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var name = NormalizeRequired(command.Name, 160, "AuthProfile name");
        var actor = NormalizeRequired(command.Actor, 160, "actor");
        var secrets = NormalizeSecrets(command.Secrets);

        var config = await dbContext.ServiceEnvironmentConfigs.SingleOrDefaultAsync(
            x => x.ServiceId == command.ServiceId && x.EnvironmentId == command.EnvironmentId,
            cancellationToken) ?? throw new KeyNotFoundException("Service/environment configuration not found.");
        if (config.AuthProfileId.HasValue)
            throw new InvalidOperationException("The service/environment already has an AuthProfile binding.");

        foreach (var item in secrets)
        {
            var descriptor = await secretVault.GetDescriptorAsync(item.Value, cancellationToken);
            if (descriptor.ServiceId != command.ServiceId
                || descriptor.EnvironmentId != command.EnvironmentId
                || descriptor.AuthProfileId.HasValue
                || !string.Equals(descriptor.SecretName, item.Key, StringComparison.Ordinal))
                throw new SecretReferenceRejectedException();
        }

        var now = clock.UtcNow;
        var profile = new AuthProfile
        {
            Id = Guid.NewGuid(),
            OwnerServiceId = command.ServiceId,
            OwnerEnvironmentId = command.EnvironmentId,
            Name = name,
            AuthType = command.AuthType,
            IsEnabled = true,
            Version = 1,
            CreatedBy = actor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        profile.Bindings.Add(new AuthProfileBinding
        {
            Id = Guid.NewGuid(),
            AuthProfileId = profile.Id,
            ServiceId = command.ServiceId,
            EnvironmentId = command.EnvironmentId,
            IsShared = false,
            DecisionBy = actor,
            DecisionReason = "OwnerBinding",
            DecisionAtUtc = now
        });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.AuthProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in secrets)
            {
                var claimed = await secretVault.ClaimAsync(
                    command.ServiceId,
                    command.EnvironmentId,
                    profile.Id,
                    item.Key,
                    item.Value,
                    cancellationToken);
                profile.Secrets.Add(new AuthProfileSecret
                {
                    AuthProfileId = profile.Id,
                    SecretName = item.Key,
                    SecretReference = item.Value.Value,
                    Generation = claimed.Generation,
                    UpdatedAtUtc = now
                });
            }

            config.AuthProfileId = profile.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        dbContext.ChangeTracker.Clear();
        return Map(await LoadProfileAsync(profile.Id, asTracking: false, cancellationToken));
    }

    public async Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        Map(await LoadProfileAsync(authProfileId, asTracking: false, cancellationToken));

    public async Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default)
    {
        var binding = await dbContext.AuthProfileBindings.AsNoTracking().SingleOrDefaultAsync(
            x => x.ServiceId == serviceId && x.EnvironmentId == environmentId,
            cancellationToken);
        if (binding is null) return null;
        var profile = await LoadProfileAsync(binding.AuthProfileId, asTracking: false, cancellationToken);
        return profile.IsEnabled ? Map(profile) : null;
    }

    public async Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = NormalizeRequired(command.Actor, 160, "actor");
        var reason = NormalizeRequired(command.Reason, 500, "sharing reason");
        var profile = await LoadProfileAsync(command.AuthProfileId, asTracking: true, cancellationToken);
        if (!profile.IsEnabled)
            throw new InvalidOperationException("A disabled AuthProfile cannot be shared.");
        if (profile.OwnerEnvironmentId != command.TargetEnvironmentId)
            throw new InvalidOperationException("AuthProfile sharing cannot cross environment boundaries.");
        if (profile.OwnerServiceId == command.TargetServiceId && profile.OwnerEnvironmentId == command.TargetEnvironmentId)
            throw new InvalidOperationException("The owner binding already exists and is not a shared binding.");

        var config = await dbContext.ServiceEnvironmentConfigs.SingleOrDefaultAsync(
            x => x.ServiceId == command.TargetServiceId && x.EnvironmentId == command.TargetEnvironmentId,
            cancellationToken) ?? throw new KeyNotFoundException("Target service/environment configuration not found.");
        if (config.AuthProfileId.HasValue)
            throw new InvalidOperationException("The target service/environment already has an AuthProfile binding.");
        if (await dbContext.AuthProfileBindings.AnyAsync(
                x => x.ServiceId == command.TargetServiceId && x.EnvironmentId == command.TargetEnvironmentId,
                cancellationToken))
            throw new InvalidOperationException("The target service/environment already has a persisted AuthProfile binding.");

        var binding = new AuthProfileBinding
        {
            Id = Guid.NewGuid(),
            AuthProfileId = profile.Id,
            ServiceId = command.TargetServiceId,
            EnvironmentId = command.TargetEnvironmentId,
            IsShared = true,
            DecisionBy = actor,
            DecisionReason = reason,
            DecisionAtUtc = clock.UtcNow
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.AuthProfileBindings.Add(binding);
        config.AuthProfileId = profile.Id;
        profile.Version++;
        profile.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return Map(await LoadProfileAsync(profile.Id, asTracking: false, cancellationToken));
    }

    public async Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var name = NormalizeRequired(command.Name, 160, "AuthProfile name");
        var profile = await LoadProfileAsync(command.AuthProfileId, asTracking: true, cancellationToken);
        if (await dbContext.AuthProfiles.AsNoTracking().AnyAsync(
                x => x.Id != profile.Id
                     && x.OwnerServiceId == profile.OwnerServiceId
                     && x.OwnerEnvironmentId == profile.OwnerEnvironmentId
                     && x.Name == name,
                cancellationToken))
            throw new InvalidOperationException("An AuthProfile with the same name already exists in this service/environment.");

        if (!string.Equals(profile.Name, name, StringComparison.Ordinal) || profile.AuthType != command.AuthType)
        {
            profile.Name = name;
            profile.AuthType = command.AuthType;
            profile.Version++;
            profile.UpdatedAtUtc = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return Map(profile);
    }

    public async Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var profile = await LoadProfileAsync(command.AuthProfileId, asTracking: true, cancellationToken);
        if (profile.OwnerServiceId == command.ServiceId && profile.OwnerEnvironmentId == command.EnvironmentId)
            throw new InvalidOperationException("The owner AuthProfile binding cannot be removed. Disable the profile or move dependent configuration through a governed replacement flow.");

        var binding = profile.Bindings.SingleOrDefault(
            x => x.ServiceId == command.ServiceId && x.EnvironmentId == command.EnvironmentId);
        if (binding is null || !binding.IsShared)
            throw new KeyNotFoundException("Shared AuthProfile binding not found.");

        var config = await dbContext.ServiceEnvironmentConfigs.SingleOrDefaultAsync(
            x => x.ServiceId == command.ServiceId
                 && x.EnvironmentId == command.EnvironmentId
                 && x.AuthProfileId == profile.Id,
            cancellationToken) ?? throw new InvalidOperationException("Persisted shared binding is not consistent with service/environment configuration.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            config.AuthProfileId = null;
            dbContext.AuthProfileBindings.Remove(binding);
            profile.Version++;
            profile.UpdatedAtUtc = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        dbContext.ChangeTracker.Clear();
        return Map(await LoadProfileAsync(profile.Id, asTracking: false, cancellationToken));
    }

    public async Task<AuthProfileDescriptor> SetSecretReferenceAsync(
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSecretName(secretName);
        var profile = await LoadProfileAsync(authProfileId, asTracking: true, cancellationToken);
        var existing = profile.Secrets.SingleOrDefault(x => x.SecretName == normalizedName);
        if (existing is not null)
        {
            if (string.Equals(existing.SecretReference, secretRef.Value, StringComparison.Ordinal))
                return Map(profile);
            throw new InvalidOperationException("An existing secret slot can only be replaced through atomic rotation activation.");
        }

        var claimed = await secretVault.ClaimAsync(
            profile.OwnerServiceId,
            profile.OwnerEnvironmentId,
            profile.Id,
            normalizedName,
            secretRef,
            cancellationToken);
        profile.Secrets.Add(new AuthProfileSecret
        {
            AuthProfileId = profile.Id,
            SecretName = normalizedName,
            SecretReference = secretRef.Value,
            Generation = claimed.Generation,
            UpdatedAtUtc = clock.UtcNow
        });
        profile.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(profile);
    }

    public async Task<bool> ActivateSecretReferenceAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef expectedCurrentReference,
        int expectedGeneration,
        SecretRef stagedReference,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSecretName(secretName);
        if (serviceId == Guid.Empty || environmentId == Guid.Empty || authProfileId == Guid.Empty || expectedGeneration < 1)
            return false;

        var ownerProfile = await dbContext.AuthProfiles.AsNoTracking().AnyAsync(
            x => x.Id == authProfileId
                 && x.OwnerServiceId == serviceId
                 && x.OwnerEnvironmentId == environmentId,
            cancellationToken);
        if (!ownerProfile)
            return false;

        var candidate = await dbContext.SecretVaultEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.Reference == stagedReference.Value
                 && x.State == SecretLifecycleState.Staged
                 && x.OwnerServiceId == serviceId
                 && x.OwnerEnvironmentId == environmentId
                 && x.OwnerAuthProfileId == authProfileId
                 && x.SecretName == normalizedName
                 && x.Generation == expectedGeneration + 1,
            cancellationToken);
        if (candidate is null)
            return false;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = clock.UtcNow;
            var slotChanged = await dbContext.AuthProfileSecrets
                .Where(x => x.AuthProfileId == authProfileId
                            && x.SecretName == normalizedName
                            && x.SecretReference == expectedCurrentReference.Value
                            && x.Generation == expectedGeneration)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.SecretReference, stagedReference.Value)
                        .SetProperty(x => x.Generation, candidate.Generation)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);
            if (slotChanged != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return false;
            }

            var previousChanged = await dbContext.SecretVaultEntries
                .Where(x => x.Reference == expectedCurrentReference.Value
                            && x.State == SecretLifecycleState.Active
                            && x.OwnerServiceId == serviceId
                            && x.OwnerEnvironmentId == environmentId
                            && x.OwnerAuthProfileId == authProfileId
                            && x.SecretName == normalizedName
                            && x.Generation == expectedGeneration)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.State, SecretLifecycleState.Revoked)
                        .SetProperty(x => x.RevokedAtUtc, now),
                    cancellationToken);
            if (previousChanged != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return false;
            }

            var candidateChanged = await dbContext.SecretVaultEntries
                .Where(x => x.Reference == stagedReference.Value
                            && x.State == SecretLifecycleState.Staged
                            && x.OwnerServiceId == serviceId
                            && x.OwnerEnvironmentId == environmentId
                            && x.OwnerAuthProfileId == authProfileId
                            && x.SecretName == normalizedName
                            && x.Generation == candidate.Generation)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.State, SecretLifecycleState.Active),
                    cancellationToken);
            if (candidateChanged != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return false;
            }

            var profileChanged = await dbContext.AuthProfiles
                .Where(x => x.Id == authProfileId
                            && x.OwnerServiceId == serviceId
                            && x.OwnerEnvironmentId == environmentId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Version, x => x.Version + 1)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);
            if (profileChanged != 1)
                throw new InvalidOperationException("Profile CAS target disappeared.");

            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw new SecretRotationPersistenceException();
        }
    }

    public async Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(authProfileId, asTracking: true, cancellationToken);
        if (profile.IsEnabled != enabled)
        {
            profile.IsEnabled = enabled;
            profile.Version++;
            profile.UpdatedAtUtc = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return Map(profile);
    }

    private async Task<AuthProfile> LoadProfileAsync(Guid authProfileId, bool asTracking, CancellationToken cancellationToken)
    {
        IQueryable<AuthProfile> query = dbContext.AuthProfiles.Include(x => x.Secrets).Include(x => x.Bindings);
        if (!asTracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == authProfileId, cancellationToken)
            ?? throw new KeyNotFoundException("AuthProfile not found.");
    }

    private static AuthProfileDescriptor Map(AuthProfile profile) =>
        new(
            profile.Id,
            profile.OwnerServiceId,
            profile.OwnerEnvironmentId,
            profile.Name,
            profile.AuthType,
            profile.IsEnabled,
            profile.Version,
            profile.CreatedBy,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            profile.Secrets.OrderBy(x => x.SecretName, StringComparer.Ordinal)
                .Select(x => new AuthProfileSecretDescriptor(x.SecretName, new SecretRef(x.SecretReference), x.Generation))
                .ToList(),
            profile.Bindings.OrderBy(x => x.ServiceId).ThenBy(x => x.EnvironmentId)
                .Select(x => new AuthProfileBindingDescriptor(
                    x.ServiceId,
                    x.EnvironmentId,
                    x.IsShared,
                    x.DecisionBy,
                    x.DecisionReason,
                    x.DecisionAtUtc))
                .ToList());

    private static Dictionary<string, SecretRef> NormalizeSecrets(IReadOnlyDictionary<string, SecretRef>? secrets)
    {
        var normalized = new Dictionary<string, SecretRef>(StringComparer.Ordinal);
        if (secrets is null) return normalized;
        if (secrets.Count > 32) throw new InvalidOperationException("An AuthProfile cannot contain more than 32 secret references.");
        foreach (var item in secrets)
        {
            var name = NormalizeSecretName(item.Key);
            if (!normalized.TryAdd(name, item.Value))
                throw new InvalidOperationException($"Duplicate AuthProfile secret slot '{name}'.");
        }
        return normalized;
    }

    private static string NormalizeSecretName(string value) => NormalizeRequired(value, 80, "secret slot").ToLowerInvariant();

    private static string NormalizeRequired(string value, int maximumLength, string label)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength || normalized.Any(char.IsControl))
            throw new InvalidOperationException($"{label} is invalid.");
        return normalized;
    }
}
