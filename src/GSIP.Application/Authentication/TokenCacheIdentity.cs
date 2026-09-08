using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GSIP.Application.Authentication;

/// <summary>
/// Future token-cache identity only. This type does not acquire, persist or
/// return tokens. Every security boundary that can make a cached token unsafe to
/// reuse is part of equality and the derived opaque cache key.
/// </summary>
public sealed record TokenCacheIdentity
{
    private TokenCacheIdentity(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        long authProfileVersion,
        long secretGeneration,
        string audience,
        string normalizedScope,
        string validityFingerprint)
    {
        ServiceId = serviceId;
        EnvironmentId = environmentId;
        AuthProfileId = authProfileId;
        AuthProfileVersion = authProfileVersion;
        SecretGeneration = secretGeneration;
        Audience = audience;
        NormalizedScope = normalizedScope;
        ValidityFingerprint = validityFingerprint;
    }

    public Guid ServiceId { get; }
    public Guid EnvironmentId { get; }
    public Guid AuthProfileId { get; }
    public long AuthProfileVersion { get; }
    public long SecretGeneration { get; }
    public string Audience { get; }
    public string NormalizedScope { get; }

    /// <summary>
    /// SHA-256 of additional non-secret auth parameters that affect token
    /// validity. Raw parameter values are deliberately not retained.
    /// </summary>
    public string ValidityFingerprint { get; }

    public static TokenCacheIdentity Create(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        long authProfileVersion,
        long secretGeneration,
        string? audience = null,
        IEnumerable<string>? scopes = null,
        IReadOnlyDictionary<string, string?>? validityParameters = null)
    {
        RequireNonEmpty(serviceId, nameof(serviceId));
        RequireNonEmpty(environmentId, nameof(environmentId));
        RequireNonEmpty(authProfileId, nameof(authProfileId));

        if (authProfileVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(authProfileVersion), "AuthProfile version must be at least 1.");
        }

        if (secretGeneration < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(secretGeneration), "Secret generation must be at least 1.");
        }

        return new TokenCacheIdentity(
            serviceId,
            environmentId,
            authProfileId,
            authProfileVersion,
            secretGeneration,
            NormalizeDimension(audience),
            NormalizeScopes(scopes),
            FingerprintValidityParameters(validityParameters));
    }

    /// <summary>
    /// Returns an opaque stable key. The key contains no token or secret
    /// plaintext and cannot collide merely because an AuthProfile is shared:
    /// ServiceId and EnvironmentId are always part of the hashed payload.
    /// </summary>
    public string ToCacheKey()
    {
        var payload = new CacheKeyPayload(
            ServiceId.ToString("N"),
            EnvironmentId.ToString("N"),
            AuthProfileId.ToString("N"),
            AuthProfileVersion,
            SecretGeneration,
            Audience,
            NormalizedScope,
            ValidityFingerprint);

        var json = JsonSerializer.Serialize(payload);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"gsip:p06:token:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    public override string ToString() => ToCacheKey();

    private static void RequireNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Cache identity dimensions cannot use an empty identifier.", parameterName);
        }
    }

    private static string NormalizeDimension(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);

    private static string NormalizeScopes(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim().Normalize(NormalizationForm.FormC))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(scope => scope, StringComparer.Ordinal));
    }

    private static string FingerprintValidityParameters(IReadOnlyDictionary<string, string?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return string.Empty;
        }

        var normalized = parameters
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ValidityParameter(
                NormalizeDimension(pair.Key),
                NormalizeDimension(pair.Value)))
            .ToArray();

        var json = JsonSerializer.Serialize(normalized);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private sealed record CacheKeyPayload(
        string ServiceId,
        string EnvironmentId,
        string AuthProfileId,
        long AuthProfileVersion,
        long SecretGeneration,
        string Audience,
        string Scope,
        string ValidityFingerprint);

    private sealed record ValidityParameter(string Name, string Value);
}
