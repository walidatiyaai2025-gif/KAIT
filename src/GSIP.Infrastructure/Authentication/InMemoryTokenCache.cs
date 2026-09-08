using System.Collections.Concurrent;
using GSIP.Application.Abstractions;
using GSIP.Application.Authentication;

namespace GSIP.Infrastructure.Authentication;

/// <summary>
/// Process-local P06 token cache with exact identity isolation, expiry safety and
/// single-flight refresh. It deliberately performs no token acquisition,
/// persistence or logging.
/// </summary>
public sealed class InMemoryTokenCache : ITokenCache, IDisposable
{
    private readonly ISystemClock clock;
    private readonly TokenCacheOptions options;
    private readonly ConcurrentDictionary<string, TokenCacheValue> entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<TokenCacheValue>> refreshes = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource shutdown = new();
    private int disposed;

    public InMemoryTokenCache(ISystemClock clock, TokenCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        this.clock = clock;
        this.options = options;
    }

    public async Task<TokenCacheValue> GetOrRefreshAsync(
        TokenCacheIdentity identity,
        Func<CancellationToken, Task<TokenCacheValue>> refreshFactory,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(refreshFactory);
        cancellationToken.ThrowIfCancellationRequested();

        var key = identity.ToCacheKey();
        if (TryGetReusable(key, out var cached))
        {
            return cached;
        }

        var refreshTask = GetOrStartRefresh(key, refreshFactory);

        // A caller owns only its wait. Cancellation must not cancel or poison the
        // refresh shared by other callers for the same exact cache identity.
        return await refreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        shutdown.Cancel();
        entries.Clear();
        refreshes.Clear();
        shutdown.Dispose();
    }

    private bool TryGetReusable(string key, out TokenCacheValue value)
    {
        if (entries.TryGetValue(key, out var cached))
        {
            var remainingLifetime = cached.ExpiresAtUtc - clock.UtcNow;
            if (remainingLifetime > options.ExpirySafetyWindow)
            {
                value = cached;
                return true;
            }

            // Remove only the stale value we observed. Never remove a newer value
            // that another refresh may have installed concurrently.
            ((ICollection<KeyValuePair<string, TokenCacheValue>>)entries)
                .Remove(new KeyValuePair<string, TokenCacheValue>(key, cached));
        }

        value = null!;
        return false;
    }

    private Task<TokenCacheValue> GetOrStartRefresh(
        string key,
        Func<CancellationToken, Task<TokenCacheValue>> refreshFactory)
    {
        while (true)
        {
            if (refreshes.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var completion = new TaskCompletionSource<TokenCacheValue>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!refreshes.TryAdd(key, completion.Task))
            {
                continue;
            }

            // Observe a fault even if every caller cancels its own wait before the
            // shared refresh completes, preventing an unobserved task exception.
            _ = completion.Task.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            _ = CompleteRefreshAsync(key, completion, refreshFactory);
            return completion.Task;
        }
    }

    private async Task CompleteRefreshAsync(
        string key,
        TaskCompletionSource<TokenCacheValue> completion,
        Func<CancellationToken, Task<TokenCacheValue>> refreshFactory)
    {
        var shutdownToken = shutdown.Token;

        try
        {
            var refreshed = await refreshFactory(shutdownToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Token refresh returned no value.");

            if (Volatile.Read(ref disposed) != 0 || shutdownToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(shutdownToken);
                return;
            }

            var remainingLifetime = refreshed.ExpiresAtUtc - clock.UtcNow;
            if (remainingLifetime <= options.ExpirySafetyWindow)
            {
                throw new InvalidOperationException(
                    "Refreshed token lifetime does not exceed the configured expiry safety window.");
            }

            entries[key] = refreshed;
            completion.TrySetResult(refreshed);
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(
                exception.CancellationToken.CanBeCanceled
                    ? exception.CancellationToken
                    : shutdownToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            if (refreshes.TryGetValue(key, out var current)
                && ReferenceEquals(current, completion.Task))
            {
                refreshes.TryRemove(key, out _);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(InMemoryTokenCache));
        }
    }
}
