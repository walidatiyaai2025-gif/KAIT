using GSIP.Domain.Secrets;

namespace GSIP.Application.Secrets;

public sealed record SecretBindingScope(
    Guid ServiceId,
    Guid EnvironmentId,
    Guid? AuthProfileId,
    string SecretName);

public sealed record SecretDescriptor(
    SecretRef Reference,
    SecretLifecycleState State,
    int Generation,
    Guid ServiceId,
    Guid EnvironmentId,
    Guid? AuthProfileId,
    string SecretName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed class SecretReferenceRejectedException : InvalidOperationException
{
    public SecretReferenceRejectedException()
        : base("Secret reference is invalid, inactive, stale, or outside the requested security scope.")
    {
    }
}

public sealed class SecretProtectionException : InvalidOperationException
{
    public SecretProtectionException()
        : base("Secret protection operation failed.")
    {
    }
}

public sealed class SecretRotationPersistenceException : InvalidOperationException
{
    public SecretRotationPersistenceException()
        : base("Secret rotation persistence operation failed safely.")
    {
    }
}

public interface ISecretVault
{
    Task<SecretDescriptor> CreateActiveAsync(
        Guid serviceId,
        Guid environmentId,
        Guid? authProfileId,
        string secretName,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default);

    Task<SecretDescriptor> CreateStagedAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        int generation,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default);

    Task<SecretDescriptor> ClaimAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default);

    Task<SecretDescriptor> GetDescriptorAsync(SecretRef secretRef, CancellationToken cancellationToken = default);
    Task DiscardStagedAsync(SecretRef secretRef, CancellationToken cancellationToken = default);

    Task<SecretDescriptor> RevokeAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default);
}

public interface ISecretMaterialResolver
{
    Task<TResult> UseSecretAsync<TResult>(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);
}

public sealed record AuthProfileSecretDescriptor(string Name, SecretRef Reference, int Generation);

public sealed record AuthProfileBindingDescriptor(
    Guid ServiceId,
    Guid EnvironmentId,
    bool IsShared,
    string DecisionBy,
    string DecisionReason,
    DateTimeOffset DecisionAtUtc);

public sealed record AuthProfileDescriptor(
    Guid Id,
    Guid OwnerServiceId,
    Guid OwnerEnvironmentId,
    string Name,
    AuthProfileType AuthType,
    bool IsEnabled,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AuthProfileSecretDescriptor> Secrets,
    IReadOnlyList<AuthProfileBindingDescriptor> Bindings);

public sealed record CreateAuthProfileCommand(
    Guid ServiceId,
    Guid EnvironmentId,
    string Name,
    AuthProfileType AuthType,
    string Actor,
    IReadOnlyDictionary<string, SecretRef>? Secrets = null);

public sealed record ShareAuthProfileCommand(
    Guid AuthProfileId,
    Guid TargetServiceId,
    Guid TargetEnvironmentId,
    string Actor,
    string Reason);

public sealed record UpdateAuthProfileCommand(
    Guid AuthProfileId,
    string Name,
    AuthProfileType AuthType);

public sealed record UnbindAuthProfileCommand(
    Guid AuthProfileId,
    Guid ServiceId,
    Guid EnvironmentId);

public interface IAuthProfileService
{
    Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> SetSecretReferenceAsync(
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default);

    Task<bool> ActivateSecretReferenceAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string secretName,
        SecretRef expectedCurrentReference,
        int expectedGeneration,
        SecretRef stagedReference,
        CancellationToken cancellationToken = default);

    Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default);
}
