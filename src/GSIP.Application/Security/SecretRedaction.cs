using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GSIP.Application.Security;

/// <summary>
/// Central secret masking/redaction used by logs, diagnostics, validation,
/// serialization, audit/evidence and UI-safe projections. This type never
/// attempts to recover or redisplay plaintext secret material.
/// </summary>
public static class SecretRedaction
{
    public const string Redacted = "[REDACTED]";
    public const string UiMask = "••••••••";

    private static readonly JsonSerializerOptions SafeJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> ExplicitSensitiveNames = new(StringComparer.Ordinal)
    {
        "password",
        "passwordhash",
        "passphrase",
        "secret",
        "secretvalue",
        "plaintextsecret",
        "clientsecret",
        "consumersecret",
        "apikey",
        "xapikey",
        "accesstoken",
        "refreshtoken",
        "bearertoken",
        "authorization",
        "proxyauthorization",
        "cookie",
        "setcookie",
        "privatekey",
        "databasepassword",
        "connectionstring"
    };

    // These identifiers describe opaque references/state and are intentionally
    // safe to render. They must not be hidden merely because their name contains
    // the word "secret".
    private static readonly HashSet<string> ExplicitSafeNames = new(StringComparer.Ordinal)
    {
        "secretref",
        "secretrefid",
        "secretreference",
        "secretgeneration",
        "secretversion",
        "hassecret",
        "secretconfigured",
        "maskedsecret",
        "secretstate",
        "secretstatus",
        "authprofileid",
        "authprofileversion"
    };

    private static readonly Regex HeaderPattern = new(
        @"(?im)\b(?:authorization|proxy-authorization|cookie|set-cookie)\s*[:=]\s*[^\r\n]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AssignmentPattern = new(
        @"(?im)\b(?:password|passphrase|client[_-]?secret|consumer[_-]?secret|api[_-]?key|x-api-key|access[_-]?token|refresh[_-]?token|bearer[_-]?token|private[_-]?key)\b\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = NormalizeIdentifier(name);
        if (ExplicitSafeNames.Contains(normalized))
        {
            return false;
        }

        if (ExplicitSensitiveNames.Contains(normalized))
        {
            return true;
        }

        return normalized.EndsWith("password", StringComparison.Ordinal)
            || normalized.EndsWith("passphrase", StringComparison.Ordinal)
            || normalized.EndsWith("secret", StringComparison.Ordinal)
            || normalized.EndsWith("accesstoken", StringComparison.Ordinal)
            || normalized.EndsWith("refreshtoken", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("privatekey", StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes known plaintext values and common secret-bearing header/assignment
    /// forms from arbitrary text. Known secret values are also scrubbed when JSON
    /// escaped or URI escaped.
    /// </summary>
    public static string RedactText(string? text, IEnumerable<string?>? knownSecrets = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;
        foreach (var secret in NormalizeSecrets(knownSecrets))
        {
            foreach (var representation in EnumerateRepresentations(secret))
            {
                result = result.Replace(representation, Redacted, StringComparison.Ordinal);
            }
        }

        result = HeaderPattern.Replace(result, MaskAssignment);
        result = AssignmentPattern.Replace(result, MaskAssignment);
        return result;
    }

    /// <summary>
    /// Produces a safe structured-log projection. Sensitive field values are
    /// replaced with a constant mask; non-sensitive values are still scanned for
    /// known plaintext values before being returned.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> RedactFields(
        IEnumerable<KeyValuePair<string, object?>> fields,
        IEnumerable<string?>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var secrets = NormalizeSecrets(knownSecrets);
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (IsSensitiveName(field.Key))
            {
                result[field.Key] = Redacted;
                continue;
            }

            result[field.Key] = field.Value switch
            {
                null => null,
                string value => RedactText(value, secrets),
                IFormattable value => RedactText(value.ToString(null, CultureInfo.InvariantCulture), secrets),
                _ => ToSafeJson(field.Value, secrets)
            };
        }

        return result;
    }

    /// <summary>
    /// Redacts sensitive JSON property values recursively. Invalid JSON is never
    /// echoed back because it may itself contain plaintext secret material.
    /// </summary>
    public static string RedactJson(string? json, IEnumerable<string?>? knownSecrets = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return Redacted;
        }

        RedactNode(node);
        var safeJson = node?.ToJsonString(SafeJsonOptions) ?? "null";
        return RedactText(safeJson, knownSecrets);
    }

    public static string ToSafeJson(object? value, IEnumerable<string?>? knownSecrets = null)
    {
        if (value is null)
        {
            return "null";
        }

        var json = JsonSerializer.Serialize(value, value.GetType(), SafeJsonOptions);
        return RedactJson(json, knownSecrets);
    }

    public static IReadOnlyList<string> RedactValidationMessages(
        IEnumerable<string> messages,
        IEnumerable<string?>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var secrets = NormalizeSecrets(knownSecrets);
        return messages.Select(message => RedactText(message, secrets)).ToArray();
    }

    public static SafeExceptionInfo ToSafeException(Exception exception, IEnumerable<string?>? knownSecrets = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var secrets = NormalizeSecrets(knownSecrets);
        return BuildSafeException(exception, secrets, depth: 0);
    }

    public static SafeSecretState ToSafeSecretState(bool configured, long generation)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Secret generation cannot be negative.");
        }

        return new SafeSecretState(configured, generation, configured ? UiMask : string.Empty);
    }

    private static SafeExceptionInfo BuildSafeException(Exception exception, IReadOnlyList<string> secrets, int depth)
    {
        // Bound recursive exception rendering to prevent pathological/cyclic
        // diagnostic payloads while never including stack traces or Data values.
        SafeExceptionInfo? inner = null;
        if (exception.InnerException is not null && depth < 8)
        {
            inner = BuildSafeException(exception.InnerException, secrets, depth + 1);
        }

        return new SafeExceptionInfo(
            exception.GetType().FullName ?? exception.GetType().Name,
            RedactText(exception.Message, secrets),
            inner);
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToList())
                {
                    if (IsSensitiveName(property.Key))
                    {
                        jsonObject[property.Key] = Redacted;
                    }
                    else
                    {
                        RedactNode(property.Value);
                    }
                }
                break;

            case JsonArray jsonArray:
                foreach (var child in jsonArray)
                {
                    RedactNode(child);
                }
                break;
        }
    }

    private static string MaskAssignment(Match match)
    {
        var value = match.Value;
        var colon = value.IndexOf(':');
        var equals = value.IndexOf('=');
        var separator = colon switch
        {
            >= 0 when equals < 0 => colon,
            >= 0 when equals >= 0 => Math.Min(colon, equals),
            _ => equals
        };

        var name = separator > 0 ? value[..separator].Trim() : "sensitive";
        return $"{name}={Redacted}";
    }

    private static string NormalizeIdentifier(string value)
    {
        var buffer = new char[value.Length];
        var index = 0;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[index++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer, 0, index);
    }

    private static IReadOnlyList<string> NormalizeSecrets(IEnumerable<string?>? knownSecrets)
    {
        if (knownSecrets is null)
        {
            return Array.Empty<string>();
        }

        return knownSecrets
            .Where(secret => !string.IsNullOrEmpty(secret))
            .Select(secret => secret!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(secret => secret.Length)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateRepresentations(string secret)
    {
        yield return secret;

        var serialized = JsonSerializer.Serialize(secret);
        if (serialized.Length >= 2)
        {
            var escaped = serialized[1..^1];
            if (!string.Equals(escaped, secret, StringComparison.Ordinal))
            {
                yield return escaped;
            }
        }

        var uriEscaped = Uri.EscapeDataString(secret);
        if (!string.Equals(uriEscaped, secret, StringComparison.Ordinal))
        {
            yield return uriEscaped;
        }
    }
}

public sealed record SafeExceptionInfo(string Type, string Message, SafeExceptionInfo? Inner);

public sealed record SafeSecretState(bool Configured, long Generation, string DisplayValue);
