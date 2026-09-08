using System.Text.Json;
using GSIP.Application.Security;

const string Sentinel = "P06_ROTATION_SENTINEL_7D91A42F0BE14D0E";

var serviceA = Guid.Parse("10000000-0000-0000-0000-000000000001");
var serviceB = Guid.Parse("10000000-0000-0000-0000-000000000002");
var uat = Guid.Parse("20000000-0000-0000-0000-000000000001");
var production = Guid.Parse("20000000-0000-0000-0000-000000000002");
var profileA = Guid.Parse("30000000-0000-0000-0000-000000000001");

var scopeA = SecretRotationScope.Create(serviceA, uat, profileA, expectedGeneration: 1);
var scopeB = SecretRotationScope.Create(serviceB, uat, profileA, expectedGeneration: 1);
var scopeProd = SecretRotationScope.Create(serviceA, production, profileA, expectedGeneration: 1);

// Successful rotation: candidate must be persisted before activation and the old
// generation/reference becomes stale rather than resolving to the new value.
var successStore = new FakeRotationStore();
successStore.Seed(scopeA, "opaque-old-a", 1, "synthetic-old");
var success = await SecretRotationSafety.RotateAsync(
    scopeA,
    _ => ValueTask.CompletedTask,
    cancellationToken => successStore.StageAsync(scopeA, Sentinel, cancellationToken),
    successStore.ActivateAsync,
    successStore.DiscardAsync);

Assert(success.ActiveGeneration == 2, "Successful rotation did not advance generation.");
Assert(successStore.Events.SequenceEqual(new[] { "stage", "activate" }), "Rotation did not persist before activation.");
Assert(successStore.Resolve(scopeA, success.ActiveReference, success.ActiveGeneration) == Sentinel, "New active candidate could not resolve in its exact scope.");
Assert(successStore.Resolve(scopeA, "opaque-old-a", 1) is null, "Stale SecretRef/generation still resolved after rotation.");
Assert(successStore.Resolve(scopeB, success.ActiveReference, success.ActiveGeneration) is null, "Cross-service SecretRef/generation misuse resolved a secret.");
Assert(successStore.Resolve(scopeProd, success.ActiveReference, success.ActiveGeneration) is null, "Cross-environment SecretRef/generation misuse resolved a secret.");

// Invalid rotation: a validator may itself include rejected plaintext in an
// exception. The coordinator must not expose it and must not stage or mutate.
var invalidStore = new FakeRotationStore();
invalidStore.Seed(scopeA, "opaque-current-invalid", 1, "synthetic-current");
var invalidBefore = invalidStore.Snapshot(scopeA);
var invalidError = await ExpectRotationRejectedAsync(() => SecretRotationSafety.RotateAsync(
    scopeA,
    _ => throw new InvalidOperationException($"rejected secret={Sentinel}"),
    cancellationToken => invalidStore.StageAsync(scopeA, Sentinel, cancellationToken),
    invalidStore.ActivateAsync,
    invalidStore.DiscardAsync));
AssertNoSentinel(invalidError.Message, "Validation failure leaked plaintext.");
Assert(invalidStore.Snapshot(scopeA) == invalidBefore, "Invalid rotation corrupted the current valid secret.");
Assert(invalidStore.Events.Count == 0, "Invalid rotation persisted a candidate before validation completed.");

// Failed activation: staged value is discarded and current remains unchanged.
var failedStore = new FakeRotationStore { RejectActivation = true };
failedStore.Seed(scopeA, "opaque-current-failed", 1, "synthetic-current");
var failedBefore = failedStore.Snapshot(scopeA);
var failedError = await ExpectRotationRejectedAsync(() => SecretRotationSafety.RotateAsync(
    scopeA,
    _ => ValueTask.CompletedTask,
    cancellationToken => failedStore.StageAsync(scopeA, Sentinel, cancellationToken),
    failedStore.ActivateAsync,
    failedStore.DiscardAsync));
AssertNoSentinel(failedError.Message, "Failed activation leaked plaintext.");
Assert(failedStore.Snapshot(scopeA) == failedBefore, "Failed activation corrupted the current valid secret.");
Assert(failedStore.Events.SequenceEqual(new[] { "stage", "activate", "discard" }), "Failed activation did not clean up the inert staged candidate.");
Assert(failedStore.StagedCount == 0, "Failed activation left an active-looking staged candidate.");

// Thrown activation/cleanup errors may carry plaintext. Both must collapse to a
// safe constant error while preserving the old active value.
var throwingStore = new FakeRotationStore { ThrowOnActivation = true, ThrowOnDiscard = true };
throwingStore.Seed(scopeA, "opaque-current-throw", 1, "synthetic-current");
var throwingBefore = throwingStore.Snapshot(scopeA);
var throwingError = await ExpectRotationRejectedAsync(() => SecretRotationSafety.RotateAsync(
    scopeA,
    _ => ValueTask.CompletedTask,
    cancellationToken => throwingStore.StageAsync(scopeA, Sentinel, cancellationToken),
    throwingStore.ActivateAsync,
    throwingStore.DiscardAsync));
AssertNoSentinel(throwingError.Message, "Activation/cleanup exception leaked plaintext.");
Assert(throwingStore.Snapshot(scopeA) == throwingBefore, "Activation exception corrupted the current valid secret.");

// Stale expected generation: another legitimate rotation wins. The stale
// candidate must not replace or resolve as that unrelated current value.
var staleStore = new FakeRotationStore();
staleStore.Seed(scopeA, "opaque-generation-1", 1, "synthetic-generation-1");
var stagedByStaleCaller = await staleStore.StageAsync(scopeA, Sentinel, CancellationToken.None);
staleStore.ForceCurrent(scopeA, "opaque-generation-2-other", 2, "synthetic-generation-2-other");
var staleActivated = await staleStore.ActivateAsync(stagedByStaleCaller, CancellationToken.None);
Assert(!staleActivated, "Stale generation unexpectedly activated.");
Assert(staleStore.Resolve(scopeA, stagedByStaleCaller.Reference, stagedByStaleCaller.Generation) is null, "Stale reference resolved to an unrelated current secret.");
Assert(staleStore.Resolve(scopeA, "opaque-generation-2-other", 2) == "synthetic-generation-2-other", "Legitimate newer current secret was corrupted by stale activation.");

// Cross-service candidate substitution is rejected before activation and never
// changes either service. This remains true even when AuthProfileId is shared.
var crossStore = new FakeRotationStore();
crossStore.Seed(scopeA, "opaque-service-a", 1, "synthetic-a");
crossStore.Seed(scopeB, "opaque-service-b", 1, "synthetic-b");
var beforeA = crossStore.Snapshot(scopeA);
var beforeB = crossStore.Snapshot(scopeB);
var crossError = await ExpectRotationRejectedAsync(() => SecretRotationSafety.RotateAsync(
    scopeA,
    _ => ValueTask.CompletedTask,
    cancellationToken => crossStore.StageAsync(scopeB, Sentinel, cancellationToken),
    crossStore.ActivateAsync,
    crossStore.DiscardAsync));
AssertNoSentinel(crossError.Message, "Cross-service rotation error leaked plaintext.");
Assert(crossStore.Snapshot(scopeA) == beforeA, "Cross-service candidate mutated Service A.");
Assert(crossStore.Snapshot(scopeB) == beforeB, "Cross-service candidate mutated Service B.");
Assert(!crossStore.Events.Contains("activate", StringComparer.Ordinal), "Cross-service candidate reached activation.");

// Invalid scope/generation contracts fail before any persistence adapter can be
// invoked.
ExpectThrows<ArgumentException>(() => SecretRotationScope.Create(Guid.Empty, uat, profileA, 1), "Empty ServiceId rotation scope was accepted.");
ExpectThrows<ArgumentOutOfRangeException>(() => SecretRotationScope.Create(serviceA, uat, profileA, 0), "Zero expected generation was accepted.");
ExpectThrows<ArgumentOutOfRangeException>(() => StagedSecretRotation<string>.Create(scopeA, "opaque-invalid-generation", 1), "Non-advancing staged generation was accepted.");

var evidenceDirectory = Path.Combine("artifacts", "p06-rotation-redaction-cache");
Directory.CreateDirectory(evidenceDirectory);
var rotationEvidence = new
{
    rotationCoordinator = true,
    persistenceOwnedByFoundation = true,
    candidatePersistedBeforeActivation = true,
    activationRequiresAtomicExpectedGeneration = true,
    invalidRotationPreservesCurrent = true,
    failedRotationPreservesCurrent = true,
    staleReferenceRejected = true,
    serviceIsolation = true,
    environmentIsolation = true,
    sharedAuthProfileDoesNotRemoveServiceBoundary = true,
    safeErrors = true
};
var rotationEvidencePath = Path.Combine(evidenceDirectory, "rotation-safety.json");
await File.WriteAllTextAsync(rotationEvidencePath, JsonSerializer.Serialize(rotationEvidence, new JsonSerializerOptions { WriteIndented = true }));
AssertNoSentinel(await File.ReadAllTextAsync(rotationEvidencePath), "Rotation evidence leaked sentinel plaintext.");

Console.WriteLine("P06_ROTATION_SAFETY_CHECKS=PASS");

async Task<SecretRotationRejectedException> ExpectRotationRejectedAsync(Func<Task<SecretRotationResult<string>>> action)
{
    try
    {
        await action();
    }
    catch (SecretRotationRejectedException exception)
    {
        return exception;
    }

    throw new InvalidOperationException("Expected rotation rejection did not occur.");
}

void AssertNoSentinel(string text, string message)
{
    if (text.Contains(Sentinel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(message);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

sealed class FakeRotationStore
{
    private const string SecretSentinel = "P06_ROTATION_SENTINEL_7D91A42F0BE14D0E";
    private readonly Dictionary<ScopeKey, ActiveSecret> _active = [];
    private readonly Dictionary<string, StagedSecret> _staged = new(StringComparer.Ordinal);
    private int _referenceSequence;

    public bool RejectActivation { get; init; }
    public bool ThrowOnActivation { get; init; }
    public bool ThrowOnDiscard { get; init; }
    public List<string> Events { get; } = [];
    public int StagedCount => _staged.Count;

    public void Seed(SecretRotationScope scope, string reference, long generation, string plaintext) =>
        _active[ScopeKey.From(scope)] = new ActiveSecret(reference, generation, plaintext);

    public ActiveSnapshot Snapshot(SecretRotationScope scope)
    {
        var active = _active[ScopeKey.From(scope)];
        return new ActiveSnapshot(active.Reference, active.Generation);
    }

    public void ForceCurrent(SecretRotationScope scope, string reference, long generation, string plaintext) =>
        _active[ScopeKey.From(scope)] = new ActiveSecret(reference, generation, plaintext);

    public ValueTask<StagedSecretRotation<string>> StageAsync(
        SecretRotationScope scope,
        string plaintext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add("stage");
        var reference = $"opaque-stage-{++_referenceSequence}";
        var generation = scope.ExpectedGeneration + 1;
        _staged[reference] = new StagedSecret(ScopeKey.From(scope), generation, plaintext);
        return ValueTask.FromResult(StagedSecretRotation<string>.Create(scope, reference, generation));
    }

    public ValueTask<bool> ActivateAsync(
        StagedSecretRotation<string> candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add("activate");

        if (ThrowOnActivation)
        {
            throw new InvalidOperationException($"activation failed secret={SecretSentinel}");
        }

        if (RejectActivation || !_staged.TryGetValue(candidate.Reference, out var staged))
        {
            return ValueTask.FromResult(false);
        }

        var key = ScopeKey.From(candidate.Scope);
        if (staged.Scope != key
            || staged.Generation != candidate.Generation
            || !_active.TryGetValue(key, out var current)
            || current.Generation != candidate.Scope.ExpectedGeneration)
        {
            return ValueTask.FromResult(false);
        }

        _active[key] = new ActiveSecret(candidate.Reference, candidate.Generation, staged.Plaintext);
        _staged.Remove(candidate.Reference);
        return ValueTask.FromResult(true);
    }

    public ValueTask DiscardAsync(
        StagedSecretRotation<string> candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add("discard");
        _staged.Remove(candidate.Reference);
        if (ThrowOnDiscard)
        {
            throw new InvalidOperationException($"discard failed secret={SecretSentinel}");
        }

        return ValueTask.CompletedTask;
    }

    public string? Resolve(SecretRotationScope scope, string reference, long generation)
    {
        return _active.TryGetValue(ScopeKey.From(scope), out var active)
            && active.Reference == reference
            && active.Generation == generation
                ? active.Plaintext
                : null;
    }

    private sealed record ActiveSecret(string Reference, long Generation, string Plaintext);
    private sealed record StagedSecret(ScopeKey Scope, long Generation, string Plaintext);
}

readonly record struct ScopeKey(Guid ServiceId, Guid EnvironmentId, Guid AuthProfileId)
{
    public static ScopeKey From(SecretRotationScope scope) =>
        new(scope.ServiceId, scope.EnvironmentId, scope.AuthProfileId);
}

readonly record struct ActiveSnapshot(string Reference, long Generation);
