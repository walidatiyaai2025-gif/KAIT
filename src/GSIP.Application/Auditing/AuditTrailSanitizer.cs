using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GSIP.Application.Security;

namespace GSIP.Application.Auditing;

public static partial class AuditTrailSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string SanitizeMetadata(IReadOnlyDictionary<string, object?>? metadata, int maximumUtf8Bytes)
    {
        if (metadata is null || metadata.Count == 0)
            return "{}";

        maximumUtf8Bytes = Math.Clamp(maximumUtf8Bytes, 256, 64 * 1024);
        var safe = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var key = Truncate(SecretRedaction.RedactText(pair.Key), 120);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (IsPersonalPayloadName(key) || SecretRedaction.IsSensitiveName(key))
            {
                safe[key] = SecretRedaction.Redacted;
                continue;
            }

            var value = pair.Value switch
            {
                null => null,
                string text => SanitizeText(text),
                _ => SecretRedaction.ToSafeJson(pair.Value)
            };
            safe[key] = Truncate(value, 1000);
        }

        var json = SecretRedaction.RedactJson(JsonSerializer.Serialize(safe, JsonOptions));
        if (Encoding.UTF8.GetByteCount(json) <= maximumUtf8Bytes)
            return json;

        return "{\"metadata\":\"[REDACTED]\",\"reason\":\"bounded\"}";
    }

    public static string SanitizeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = SecretRedaction.RedactText(value);
        if (BearerPattern().IsMatch(redacted) || JwtPattern().IsMatch(redacted))
            return SecretRedaction.Redacted;
        return redacted;
    }

    public static bool IsPersonalPayloadName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.Contains("civilid", StringComparison.Ordinal)
            || normalized.Contains("civilnumber", StringComparison.Ordinal)
            || normalized.Contains("nationalid", StringComparison.Ordinal)
            || normalized.Contains("personalnumber", StringComparison.Ordinal)
            || normalized.Contains("personidentifier", StringComparison.Ordinal)
            || normalized.Contains("requestpayload", StringComparison.Ordinal)
            || normalized.Contains("responsepayload", StringComparison.Ordinal)
            || normalized.Contains("requestbody", StringComparison.Ordinal)
            || normalized.Contains("responsebody", StringComparison.Ordinal)
            || normalized.Contains("rawrequest", StringComparison.Ordinal)
            || normalized.Contains("rawresponse", StringComparison.Ordinal);
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength ? value : value[..maximumLength];

    [GeneratedRegex(@"(?i)\bbearer\s+\S+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"\b[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();
}
