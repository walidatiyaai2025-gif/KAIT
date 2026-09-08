using GSIP.Domain.Secrets;

namespace GSIP.Application.Secrets;

public sealed record SecretDescriptor(
    SecretRef Reference,
    SecretLifecycleState State,
    int Generation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed class SecretReferenceRejectedException : InvalidOperationException
{
    public SecretReferenceRejectedException()
        : base("Secret reference is invalid, inactive, or stale.")
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

public interface ISecretVault
{
    Task<SecretDescriptor> CreateActiveAsync(ReadOnlyMemory<byte> secretMaterial, CancellationToken cancellationToken = default);
    Task<SecretDescriptor> GetDescriptorAsync(SecretRef secretRef, CancellationToken cancellationToken = default);
    Task<SecretDescriptor> RevokeAsync(SecretRef secretRef, CancellationToken cancellationToken = default);
}

public interface ISecretMaterialResolver
{
    Task<TResult> UseSecretAsync<TResult>(
        SecretRef secretRef,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);
}

public sealed record AuthProfileSecretDescriptor(string Name, SecretRef Reference);

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

public interface IAuthProfileService
{
    Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> SetSecretReferenceAsync(
        Guid authProfileId,
        string secretName,
        SecretRef secretRef,
        CancellationToken cancellationToken = default);
    Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default);
}
