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
    public async Task<AuthProfileDescriptor> CreateAsync(
        CreateAuthProfileCommand command,
        CancellationToken cancellationToken = default)
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

        foreach (var secret in secrets.Values)
            await secretVault.GetDescriptorAsync(secret, cancellationToken);

        var now = clock.UtcNow;
        var profile = new AuthProfile
        {
            Id = Guid.NewGuid(),
            OwnerServiceId = command.ServiceId,
            OwnerEnvironmentId = command.EnvironmentId,
            Name = name,
            AuthType = command.AuthType,
            IsEnabled = true,
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
        foreach (var secret in secrets)
            profile.Secrets.Add(new AuthProfileSecret
            {
                AuthProfileId = profile.Id,
                SecretName = secret.Key,
                SecretReference = secret.Value.Value,
                UpdatedAtUtc = now
            });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.AuthProfiles.Add(profile);
        config.AuthProfileId = profile.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(profile);
    }

    public async Task<AuthProfileDescriptor> GetAsync(
        Guid authProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(authProfileId, asTracking: false, cancellationToken);
        return Map(profile);
    }

    public async Task<AuthProfileDescriptor?> ResolveAsync(
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var binding = await dbContext.AuthProfileBindings.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.ServiceId == serviceId && x.EnvironmentId == environmentId,
                cancellationToken);
        if (binding is null) return null;

        var profile = await LoadProfileAsync(binding.AuthProfileId, asTracking: false, cancellationToken);
        return profile.IsEnabled ? Map(profile) : null;
    }

    public async Task<AuthProfileDescriptor> ShareAsync(
        ShareAuthProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = NormalizeRequired(command.Actor, 160, "actor");
        var reason = NormalizeRequired(command.Reason, 500, "sharing reason");
        var profile = await LoadProfileAsync(command.AuthProfileId, asTracking: true, cancellationToken);

        if (!profile.IsEnabled)
            throw new InvalidOperationException("A disabled AuthProfile cannot be shared.");
        if (profile.OwnerServiceId == command.TargetServiceId && profile.OwnerEnvironmentId == command.TargetEnvironmentId)
            throw new InvalidOperationException("The owner binding already exists and is not a shared binding.");

        var config = await dbContext.ServiceEnvironmentConfigs.SingleOrDefaultAsync(
            x => x.ServiceId == command.TargetServiceId && x.EnvironmentId == command.TargetEnvironmentId,
            cancellationToken) ?? throw new KeyNotFoundException("Target service/environment configuration not found.");
        if (config.AuthProfileId.HasValue)
            throw new InvalidOperationException("The target service/environment already has an AuthProfile binding.");

        var alreadyBound = await dbContext.AuthProfileBindings.AnyAsync(
            x => x.ServiceId == command.TargetServiceId && x.EnvironmentId == command.TargetEnvironmentId,
            cancellationToken);
        if (alreadyBound)
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
        profile.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
        await secretVault.GetDescriptorAsync(secretRef, cancellationToken);

        var profile = await LoadProfileAsync(authProfileId, asTracking: true, cancellationToken);
        var existing = profile.Secrets.SingleOrDefault(x => x.SecretName == normalizedName);
        if (existing is null)
        {
            existing = new AuthProfileSecret
            {
                AuthProfileId = profile.Id,
                SecretName = normalizedName
            };
            profile.Secrets.Add(existing);
        }

        existing.SecretReference = secretRef.Value;
        existing.UpdatedAtUtc = clock.UtcNow;
        profile.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(profile);
    }

    public async Task<AuthProfileDescriptor> SetEnabledAsync(
        Guid authProfileId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(authProfileId, asTracking: true, cancellationToken);
        profile.IsEnabled = enabled;
        profile.UpdatedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(profile);
    }

    private async Task<AuthProfile> LoadProfileAsync(
        Guid authProfileId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<AuthProfile> query = dbContext.AuthProfiles
            .Include(x => x.Secrets)
            .Include(x => x.Bindings);
        if (!asTracking)
            query = query.AsNoTracking();

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
            profile.CreatedBy,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            profile.Secrets.OrderBy(x => x.SecretName, StringComparer.Ordinal)
                .Select(x => new AuthProfileSecretDescriptor(x.SecretName, new SecretRef(x.SecretReference)))
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

    private static string NormalizeSecretName(string value) =>
        NormalizeRequired(value, 80, "secret slot").ToLowerInvariant();

    private static string NormalizeRequired(string value, int maximumLength, string label)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength || normalized.Any(char.IsControl))
            throw new InvalidOperationException($"{label} is invalid.");
        return normalized;
    }
}
