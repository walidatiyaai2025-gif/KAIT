using System.Text.Json.Serialization;

namespace GSIP.Application.Authentication;

/// <summary>
/// Immutable runtime token value. The bearer value is deliberately excluded from
/// normal JSON serialization and string formatting so cache diagnostics cannot
/// accidentally disclose credential material.
/// </summary>
public sealed class TokenCacheValue
{
    private TokenCacheValue(string accessToken, DateTimeOffset expiresAtUtc)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    [JsonIgnore]
    public string AccessToken { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public static TokenCacheValue Create(string accessToken, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("A refreshed token value is required.", nameof(accessToken));
        }

        return new TokenCacheValue(accessToken, expiresAtUtc);
    }

    public override string ToString() =>
        $"TokenCacheValue {{ AccessToken = [REDACTED], ExpiresAtUtc = {ExpiresAtUtc:O} }}";
}

/// <summary>
/// Safety controls for the in-memory runtime token cache.
/// </summary>
public sealed class TokenCacheOptions
{
    public TokenCacheOptions()
        : this(TimeSpan.FromSeconds(30))
    {
    }

    public TokenCacheOptions(TimeSpan expirySafetyWindow)
    {
        if (expirySafetyWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expirySafetyWindow),
                "The token expiry safety window cannot be negative.");
        }

        ExpirySafetyWindow = expirySafetyWindow;
    }

    public TimeSpan ExpirySafetyWindow { get; }
}

/// <summary>
/// Runtime-only token cache. Acquisition remains the caller's responsibility;
/// the cache only coalesces refresh work and reuses a value while it is safely
/// outside the configured expiry window.
/// </summary>
public interface ITokenCache
{
    Task<TokenCacheValue> GetOrRefreshAsync(
        TokenCacheIdentity identity,
        Func<CancellationToken, Task<TokenCacheValue>> refreshFactory,
        CancellationToken cancellationToken = default);
}
