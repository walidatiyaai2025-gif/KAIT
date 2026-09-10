using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GSIP.Infrastructure.Execution;

internal sealed record TokenEndpointContractMetadata(
    string Path,
    string RequestContentType,
    string ResponsePath,
    string UsernameField,
    string PasswordField,
    string? GehaField,
    string GehaValueType,
    int? DocumentedTtlSeconds,
    bool ApiKeyRequired)
{
    public const string PathKey = "X-GSIP-TokenEndpointPath";
    public const string RequestContentTypeKey = "X-GSIP-TokenRequestContentType";
    public const string ResponsePathKey = "X-GSIP-TokenResponsePath";
    public const string UsernameFieldKey = "X-GSIP-TokenUsernameField";
    public const string PasswordFieldKey = "X-GSIP-TokenPasswordField";
    public const string GehaFieldKey = "X-GSIP-TokenGehaField";
    public const string GehaValueTypeKey = "X-GSIP-TokenGehaValueType";
    public const string DocumentedTtlSecondsKey = "X-GSIP-TokenDocumentedTtlSeconds";
    public const string ApiKeyRequiredKey = "X-GSIP-TokenApiKeyRequired";
    public const string ApiKeySecretName = "x-api-key";
    public const string GehaSecretName = "geha";
    public const int MaximumResponseBytes = 64 * 1024;

    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        PathKey,
        RequestContentTypeKey,
        ResponsePathKey,
        UsernameFieldKey,
        PasswordFieldKey,
        GehaFieldKey,
        GehaValueTypeKey,
        DocumentedTtlSecondsKey,
        ApiKeyRequiredKey
    };

    public static bool IsReservedMetadataKey(string name) => ReservedKeys.Contains(name);

    public static TokenEndpointContractMetadata Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Token endpoint metadata is unavailable.");

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Token endpoint metadata is invalid.");

            var path = RequiredString(document.RootElement, PathKey);
            if (!path.StartsWith("/", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal)
                || path.Contains('\\') || path.Contains('?') || path.Contains('#') || path.Any(char.IsControl))
                throw new InvalidOperationException("Token endpoint metadata is invalid.");

            // Preserve the P08 legacy contract when only the token path existed.
            var contentType = OptionalString(document.RootElement, RequestContentTypeKey) ?? "application/x-www-form-urlencoded";
            if (!string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Token endpoint metadata is invalid.");

            var responsePath = OptionalString(document.RootElement, ResponsePathKey) ?? "data";
            var usernameField = OptionalString(document.RootElement, UsernameFieldKey) ?? "username";
            var passwordField = OptionalString(document.RootElement, PasswordFieldKey) ?? "password";
            var gehaField = OptionalString(document.RootElement, GehaFieldKey);
            var gehaValueType = OptionalString(document.RootElement, GehaValueTypeKey) ?? "string";
            ValidateWireName(responsePath);
            ValidateWireName(usernameField);
            ValidateWireName(passwordField);
            if (gehaField is not null) ValidateWireName(gehaField);
            if (gehaField is null && document.RootElement.TryGetProperty(GehaValueTypeKey, out _))
                throw new InvalidOperationException("Token endpoint metadata is invalid.");
            if (!string.Equals(gehaValueType, "string", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(gehaValueType, "integer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Token endpoint metadata is invalid.");

            int? ttl = null;
            if (document.RootElement.TryGetProperty(DocumentedTtlSecondsKey, out var ttlElement))
            {
                if (ttlElement.ValueKind != JsonValueKind.Number || !ttlElement.TryGetInt32(out var seconds) || seconds is < 1 or > 86400)
                    throw new InvalidOperationException("Token endpoint metadata is invalid.");
                ttl = seconds;
            }

            var apiKeyRequired = false;
            if (document.RootElement.TryGetProperty(ApiKeyRequiredKey, out var requiredElement))
            {
                apiKeyRequired = requiredElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(requiredElement.GetString(), out var parsed) => parsed,
                    _ => throw new InvalidOperationException("Token endpoint metadata is invalid.")
                };
            }

            return new TokenEndpointContractMetadata(path, contentType, responsePath, usernameField, passwordField, gehaField, gehaValueType, ttl, apiKeyRequired);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Token endpoint metadata is invalid.", exception);
        }
    }

    public HttpContent BuildRequestContent(string username, string password, string? geha)
    {
        if (string.Equals(RequestContentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [UsernameField] = username,
                [PasswordField] = password
            };
            if (GehaField is not null)
                values[GehaField] = ParseGehaJsonValue(geha);

            var content = new StringContent(JsonSerializer.Serialize(values), Encoding.UTF8, "application/json");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(RequestContentType);
            return content;
        }

        var formValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UsernameField] = username,
            [PasswordField] = password
        };
        if (GehaField is not null)
        {
            if (string.IsNullOrWhiteSpace(geha))
                throw new InvalidOperationException("Token endpoint credential metadata is incomplete.");
            if (string.Equals(GehaValueType, "integer", StringComparison.OrdinalIgnoreCase)
                && !long.TryParse(geha, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Token endpoint credential metadata is incomplete.");
            formValues[GehaField] = geha;
        }
        return new FormUrlEncodedContent(formValues);
    }

    public string ResolveToken(JsonElement root)
    {
        var current = root;
        foreach (var segment in ResponsePath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                throw new InvalidOperationException("Token endpoint response is invalid.");
        }
        if (current.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Token endpoint response is invalid.");
        var token = current.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024 || token.Any(character => character is '\r' or '\n' or '\0'))
            throw new InvalidOperationException("Token endpoint response is invalid.");
        return token;
    }

    public DateTimeOffset ResolveExpiry(string accessToken, DateTimeOffset now)
    {
        var documented = DocumentedTtlSeconds is int ttl ? now.AddSeconds(ttl) : (DateTimeOffset?)null;
        var jwt = TryGetJwtExpiry(accessToken);
        if (documented is DateTimeOffset documentedExpiry && jwt is DateTimeOffset jwtExpiry)
            return documentedExpiry < jwtExpiry ? documentedExpiry : jwtExpiry;
        if (documented is DateTimeOffset onlyDocumented)
            return onlyDocumented;
        if (jwt is DateTimeOffset onlyJwt)
            return onlyJwt > now ? onlyJwt : now;
        return now;
    }

    public IReadOnlyDictionary<string, string?> ValidityParameters(int usernameGeneration, int passwordGeneration, int? gehaGeneration, int? apiKeyGeneration)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["token-path"] = Path,
            ["token-content-type"] = RequestContentType,
            ["token-response-path"] = ResponsePath,
            ["token-username-field"] = UsernameField,
            ["token-password-field"] = PasswordField,
            ["username-generation"] = usernameGeneration.ToString(CultureInfo.InvariantCulture),
            ["password-generation"] = passwordGeneration.ToString(CultureInfo.InvariantCulture),
            ["token-ttl-seconds"] = DocumentedTtlSeconds?.ToString(CultureInfo.InvariantCulture)
        };
        if (GehaField is not null)
        {
            result["token-geha-field"] = GehaField;
            result["token-geha-value-type"] = GehaValueType;
            result["geha-generation"] = gehaGeneration?.ToString(CultureInfo.InvariantCulture);
        }
        if (apiKeyGeneration is int apiGeneration)
        {
            result["api-key-header"] = ApiKeySecretName;
            result["api-key-generation"] = apiGeneration.ToString(CultureInfo.InvariantCulture);
        }
        return result;
    }

    private object ParseGehaJsonValue(string? geha)
    {
        if (string.IsNullOrWhiteSpace(geha))
            throw new InvalidOperationException("Token endpoint credential metadata is incomplete.");
        if (string.Equals(GehaValueType, "integer", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(geha, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException("Token endpoint credential metadata is incomplete.");
            return value;
        }
        return geha;
    }

    private static string RequiredString(JsonElement root, string name) => OptionalString(root, name)
        ?? throw new InvalidOperationException("Token endpoint metadata is invalid.");

    private static string? OptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element)) return null;
        if (element.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Token endpoint metadata is invalid.");
        var value = element.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => character is '\r' or '\n' or '\0'))
            throw new InvalidOperationException("Token endpoint metadata is invalid.");
        return value;
    }

    private static void ValidateWireName(string value)
    {
        if (value.Length > 120 || value.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
            throw new InvalidOperationException("Token endpoint metadata is invalid.");
    }

    private static DateTimeOffset? TryGetJwtExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = (payload.Length % 4) switch
            {
                0 => payload,
                2 => payload + "==",
                3 => payload + "=",
                _ => throw new FormatException()
            };
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (!document.RootElement.TryGetProperty("exp", out var exp)
                || exp.ValueKind != JsonValueKind.Number || !exp.TryGetInt64(out var seconds))
                return null;
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
