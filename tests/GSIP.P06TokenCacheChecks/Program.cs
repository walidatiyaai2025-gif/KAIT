using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;
using GSIP.Infrastructure.Authentication;

await CacheHitAndIdentityIsolationAsync();
await ExpirySafetyWindowForcesEarlyRefreshAsync();
await ConcurrentCallersShareOneRefreshAsync();
await CallerCancellationDoesNotPoisonSharedRefreshAsync();
await FailedRefreshIsNotCachedAsync();
await UnsafeLifetimeIsRejectedWithoutCachingAsync();
SecretSafeDiagnosticsDoNotExposeToken();

Console.WriteLine("P06 token cache runtime checks: PASS");
return;

static async Task CacheHitAndIdentityIsolationAsync()
{
    var clock = FakeClock.Create();
    using var cache = NewCache(clock);
    var serviceA = Identity(serviceId: Guid.NewGuid());
    var serviceB = Identity(serviceId: Guid.NewGuid());
    var calls = 0;

    async Task<TokenCacheValue> Refresh(string token, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref calls);
        return TokenCacheValue.Create(token, clock.UtcNow.AddMinutes(10));
    }

    var first = await cache.GetOrRefreshAsync(serviceA, ct => Refresh("synthetic-token-a", ct));
    var second = await cache.GetOrRefreshAsync(serviceA, ct => Refresh("must-not-run", ct));
    var isolated = await cache.GetOrRefreshAsync(serviceB, ct => Refresh("synthetic-token-b", ct));

    Check(calls == 2, "Cache hits must reuse an exact identity while a different Service identity refreshes independently.");
    Check(first.AccessToken == second.AccessToken, "Exact identity cache hit must return the cached token value.");
    Check(isolated.AccessToken != first.AccessToken, "Different Service identities must never share a cached token value.");
}

static async Task ExpirySafetyWindowForcesEarlyRefreshAsync()
{
    var clock = FakeClock.Create();
    using var cache = NewCache(clock, TimeSpan.FromSeconds(30));
    var identity = Identity();
    var calls = 0;

    Task<TokenCacheValue> Refresh(CancellationToken _)
    {
        var call = Interlocked.Increment(ref calls);
        var token = call == 1 ? "short-lived-token" : "replacement-token";
        var lifetime = call == 1 ? TimeSpan.FromSeconds(40) : TimeSpan.FromMinutes(5);
        return Task.FromResult(TokenCacheValue.Create(token, clock.UtcNow.Add(lifetime)));
    }

    var first = await cache.GetOrRefreshAsync(identity, Refresh);
    clock.Advance(TimeSpan.FromSeconds(11));
    var second = await cache.GetOrRefreshAsync(identity, Refresh);

    Check(calls == 2, "A cached token inside the expiry safety window must refresh before actual expiry.");
    Check(first.AccessToken != second.AccessToken, "Safety-window refresh must replace the near-expiry cached value.");
}

static async Task ConcurrentCallersShareOneRefreshAsync()
{
    var clock = FakeClock.Create();
    using var cache = NewCache(clock);
    var identity = Identity();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0;

    async Task<TokenCacheValue> Refresh(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        started.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
        return TokenCacheValue.Create("single-flight-token", clock.UtcNow.AddMinutes(5));
    }

    var waiters = Enumerable.Range(0, 32)
        .Select(_ => cache.GetOrRefreshAsync(identity, Refresh))
        .ToArray();

    await started.Task;
    release.TrySetResult();
    var values = await Task.WhenAll(waiters);

    Check(calls == 1, "Concurrent callers for one exact identity must execute exactly one refresh factory.");
    Check(values.All(value => value.AccessToken == "single-flight-token"), "All single-flight waiters must receive the shared refreshed value.");
}

static async Task CallerCancellationDoesNotPoisonSharedRefreshAsync()
{
    var clock = FakeClock.Create();
    using var cache = NewCache(clock);
    var identity = Identity();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0;

    async Task<TokenCacheValue> Refresh(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        started.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
        return TokenCacheValue.Create("surviving-shared-token", clock.UtcNow.AddMinutes(5));
    }

    using var callerCancellation = new CancellationTokenSource();
    var canceledWaiter = cache.GetOrRefreshAsync(identity, Refresh, callerCancellation.Token);
    await started.Task;
    callerCancellation.Cancel();
    await ExpectThrowsAsync<OperationCanceledException>(async () => await canceledWaiter);

    var survivingWaiter = cache.GetOrRefreshAsync(identity, Refresh);
    release.TrySetResult();
    var value = await survivingWaiter;
    var cached = await cache.GetOrRefreshAsync(identity, _ => throw new InvalidOperationException("cache miss after successful shared refresh"));

    Check(calls == 1, "Canceling one caller must not cancel the refresh shared by other callers.");
    Check(value.AccessToken == cached.AccessToken, "A shared refresh must remain cacheable after another waiter cancels.");
}

static async Task FailedRefreshIsNotCachedAsync()
{
    var clock = FakeClock.Create();
    using var cache = NewCache(clock);
    var identity = Identity();
    var calls = 0;

    async Task<TokenCacheValue> Refresh(CancellationToken _)
    {
        await Task.Yield();
        if (Interlocked.Increment(ref calls) == 1)
        {
            throw new InvalidOperationException("synthetic refresh failure");
        }

        return TokenCacheValue.Create("recovered-token", clock.UtcNow.AddMinutes(5));
    }

    await ExpectThrowsAsync<InvalidOperationException>(() => cache.GetOrRefreshAsync(identity, Refresh));
    var recovered = await cache.GetOrRefreshAsync(identity, Refresh);

    Check(calls == 2, "A failed refresh must not be cached; the next request must retry.");
    Check(recovered.AccessToken == "recovered-token", "A retry after refresh failure must return the newly refreshed value.");
}

static async Task UnsafeLifetimeIsRejectedWithoutCachingAsync()
{
    var clock = FakeClock.Create();
    var safetyWindow = TimeSpan.FromSeconds(30);
    using var cache = NewCache(clock, safetyWindow);
    var identity = Identity();
    const string sentinel = "P06-NEAR-EXPIRY-SECRET-SENTINEL";

    var exception = await ExpectThrowsAsync<InvalidOperationException>(() =>
        cache.GetOrRefreshAsync(
            identity,
            _ => Task.FromResult(TokenCacheValue.Create(sentinel, clock.UtcNow.Add(safetyWindow)))));

    Check(!exception.Message.Contains(sentinel, StringComparison.Ordinal), "Near-expiry rejection must not include token plaintext in the error.");

    var recovered = await cache.GetOrRefreshAsync(
        identity,
        _ => Task.FromResult(TokenCacheValue.Create("fresh-after-reject", clock.UtcNow.AddMinutes(5))));

    Check(recovered.AccessToken == "fresh-after-reject", "An unsafe near-expiry refresh result must not poison or populate the cache.");
}

static void SecretSafeDiagnosticsDoNotExposeToken()
{
    const string sentinel = "P06-TOKEN-CACHE-PLAINTEXT-SENTINEL";
    var value = TokenCacheValue.Create(sentinel, DateTimeOffset.UtcNow.AddMinutes(5));
    var serialized = JsonSerializer.Serialize(value);
    var formatted = value.ToString();

    Check(!serialized.Contains(sentinel, StringComparison.Ordinal), "JSON serialization must not disclose cached token plaintext.");
    Check(!serialized.Contains("AccessToken", StringComparison.Ordinal), "JSON serialization must omit the token-bearing property entirely.");
    Check(!formatted.Contains(sentinel, StringComparison.Ordinal), "TokenCacheValue.ToString() must not disclose cached token plaintext.");
    Check(formatted.Contains("[REDACTED]", StringComparison.Ordinal), "TokenCacheValue.ToString() must explicitly redact the token field.");
}

static InMemoryTokenCache NewCache(FakeClock clock, TimeSpan? safetyWindow = null) =>
    new(clock, new TokenCacheOptions(safetyWindow ?? TimeSpan.FromSeconds(30)));

static TokenCacheIdentity Identity(Guid? serviceId = null) =>
    TokenCacheIdentity.Create(
        serviceId ?? Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        authProfileVersion: 1,
        secretGeneration: 1,
        audience: "synthetic-audience",
        scopes: ["read", "write"]);

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

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

sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public static FakeClock Create() => new(new DateTimeOffset(2026, 9, 8, 21, 0, 0, TimeSpan.Zero));

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
