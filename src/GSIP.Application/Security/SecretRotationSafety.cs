namespace GSIP.Application.Security;

/// <summary>
/// Security-bound rotation scope used by the rotation coordinator. It is not a
/// persistence model and deliberately carries no secret plaintext or SecretRef
/// implementation. Adapters to the canonical P06 foundation must preserve all
/// dimensions exactly.
/// </summary>
public sealed record SecretRotationScope
{
    private SecretRotationScope(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        long expectedGeneration)
    {
        ServiceId = serviceId;
        EnvironmentId = environmentId;
        AuthProfileId = authProfileId;
        ExpectedGeneration = expectedGeneration;
    }

    public Guid ServiceId { get; }
    public Guid EnvironmentId { get; }
    public Guid AuthProfileId { get; }
    public long ExpectedGeneration { get; }

    public static SecretRotationScope Create(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        long expectedGeneration)
    {
        RequireNonEmpty(serviceId, nameof(serviceId));
        RequireNonEmpty(environmentId, nameof(environmentId));
        RequireNonEmpty(authProfileId, nameof(authProfileId));

        if (expectedGeneration < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedGeneration),
                "Expected secret generation must be at least 1.");
        }

        return new SecretRotationScope(serviceId, environmentId, authProfileId, expectedGeneration);
    }

    private static void RequireNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Rotation scope identifiers cannot be empty.", parameterName);
        }
    }
}

/// <summary>
/// An already-persisted, not-yet-active candidate returned by a canonical
/// foundation adapter. TReference is intentionally opaque so this component does
/// not define or compete with the foundation SecretRef model.
/// </summary>
public sealed record StagedSecretRotation<TReference>
    where TReference : notnull
{
    private StagedSecretRotation(
        SecretRotationScope scope,
        TReference reference,
        long generation)
    {
        Scope = scope;
        Reference = reference;
        Generation = generation;
    }

    public SecretRotationScope Scope { get; }
    public TReference Reference { get; }
    public long Generation { get; }

    public static StagedSecretRotation<TReference> Create(
        SecretRotationScope scope,
        TReference reference,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);

        if (generation <= scope.ExpectedGeneration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                "A staged secret generation must be newer than the expected active generation.");
        }

        return new StagedSecretRotation<TReference>(scope, reference, generation);
    }
}

public sealed record SecretRotationResult<TReference>(TReference ActiveReference, long ActiveGeneration)
    where TReference : notnull;

/// <summary>
/// Safe application-level rotation sequencing over the canonical foundation.
/// The caller supplies foundation adapters; this class owns no vault storage.
///
/// Contract required from activateCandidate:
/// - atomically compare the current active generation with Scope.ExpectedGeneration;
/// - verify exact ServiceId + EnvironmentId + AuthProfileId ownership;
/// - activate only the supplied already-persisted candidate;
/// - return false without mutating the current valid secret on stale generation
///   or scope/reference mismatch.
/// </summary>
public static class SecretRotationSafety
{
    public static async Task<SecretRotationResult<TReference>> RotateAsync<TReference>(
        SecretRotationScope scope,
        Func<CancellationToken, ValueTask> validateNewSecret,
        Func<CancellationToken, ValueTask<StagedSecretRotation<TReference>>> persistCandidate,
        Func<StagedSecretRotation<TReference>, CancellationToken, ValueTask<bool>> activateCandidate,
        Func<StagedSecretRotation<TReference>, CancellationToken, ValueTask> discardCandidate,
        CancellationToken cancellationToken = default)
        where TReference : notnull
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(validateNewSecret);
        ArgumentNullException.ThrowIfNull(persistCandidate);
        ArgumentNullException.ThrowIfNull(activateCandidate);
        ArgumentNullException.ThrowIfNull(discardCandidate);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await validateNewSecret(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Never surface a validation exception because validators can include
            // rejected plaintext in their message/data.
            throw new SecretRotationRejectedException("Secret rotation validation failed.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        StagedSecretRotation<TReference> candidate;
        try
        {
            // Persist first. Nothing is activated before this completes.
            candidate = await persistCandidate(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new SecretRotationRejectedException("Secret rotation could not be staged safely.");
        }

        if (candidate is null)
        {
            throw new SecretRotationRejectedException("Secret rotation staging returned no candidate.");
        }

        if (candidate.Scope != scope || candidate.Generation <= scope.ExpectedGeneration)
        {
            await DiscardBestEffortAsync(candidate, discardCandidate).ConfigureAwait(false);
            throw new SecretRotationRejectedException("Secret rotation candidate did not match the requested security scope.");
        }

        bool activated;
        try
        {
            // After a candidate exists, activation/cleanup uses no caller
            // cancellation token. This prevents cancellation from abandoning a
            // persisted candidate in an indeterminate activation sequence. The
            // foundation activation adapter itself must provide transactional CAS.
            activated = await activateCandidate(candidate, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await DiscardBestEffortAsync(candidate, discardCandidate).ConfigureAwait(false);
            throw new SecretRotationRejectedException("Secret rotation activation failed; the previous secret remains active.");
        }

        if (!activated)
        {
            await DiscardBestEffortAsync(candidate, discardCandidate).ConfigureAwait(false);
            throw new SecretRotationRejectedException("Secret rotation was rejected because the active generation or security scope changed.");
        }

        return new SecretRotationResult<TReference>(candidate.Reference, candidate.Generation);
    }

    private static async ValueTask DiscardBestEffortAsync<TReference>(
        StagedSecretRotation<TReference> candidate,
        Func<StagedSecretRotation<TReference>, CancellationToken, ValueTask> discardCandidate)
        where TReference : notnull
    {
        try
        {
            await discardCandidate(candidate, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A failed cleanup may leave an inert staged value for later
            // maintenance, but must not expose its exception (which may contain
            // plaintext) or mask the primary safe rotation result. Activation has
            // not succeeded on these paths, so the prior active secret remains the
            // only valid reference under the required foundation contract.
        }
    }
}

/// <summary>
/// Constant-message exception safe for validation/API/audit surfaces. No inner
/// exception is retained so rejected secret material cannot escape indirectly.
/// </summary>
public sealed class SecretRotationRejectedException : InvalidOperationException
{
    public SecretRotationRejectedException(string safeMessage)
        : base(safeMessage)
    {
    }
}
