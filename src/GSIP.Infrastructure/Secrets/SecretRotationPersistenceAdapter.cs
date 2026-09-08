using GSIP.Application.Secrets;
using GSIP.Application.Security;
using GSIP.Domain.Secrets;

namespace GSIP.Infrastructure.Secrets;

public sealed class SecretRotationPersistenceAdapter(
    ISecretVault secretVault,
    IAuthProfileService authProfiles)
{
    public async ValueTask<StagedSecretRotation<SecretRef>> StageAsync(
        SecretRotationScope scope,
        string secretName,
        ReadOnlyMemory<byte> secretMaterial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var descriptor = await secretVault.CreateStagedAsync(
            scope.ServiceId,
            scope.EnvironmentId,
            scope.AuthProfileId,
            secretName,
            checked((int)scope.ExpectedGeneration + 1),
            secretMaterial,
            cancellationToken);
        return StagedSecretRotation<SecretRef>.Create(scope, descriptor.Reference, descriptor.Generation);
    }

    public async ValueTask<bool> ActivateAsync(
        SecretRef expectedCurrentReference,
        string secretName,
        StagedSecretRotation<SecretRef> candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Scope.ExpectedGeneration > int.MaxValue)
            return false;
        return await authProfiles.ActivateSecretReferenceAsync(
            candidate.Scope.ServiceId,
            candidate.Scope.EnvironmentId,
            candidate.Scope.AuthProfileId,
            secretName,
            expectedCurrentReference,
            (int)candidate.Scope.ExpectedGeneration,
            candidate.Reference,
            cancellationToken);
    }

    public async ValueTask DiscardAsync(
        StagedSecretRotation<SecretRef> candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await secretVault.DiscardStagedAsync(candidate.Reference, cancellationToken);
    }
}
