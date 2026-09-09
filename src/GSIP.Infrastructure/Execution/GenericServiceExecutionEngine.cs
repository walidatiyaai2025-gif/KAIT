using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GSIP.Application.Authentication;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Execution;

public sealed class GenericServiceExecutionEngine(
    IServiceExecutionSecurityGate securityGate,
    IMetadataCatalogService metadataCatalog,
    IAuthProfileService authProfiles,
    ISecretMaterialResolver secretResolver,
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceExecutionRuntimeOptions> options,
    ILogger<GenericServiceExecutionEngine> logger,
    ITokenCache? tokenCache = null) : IServiceExecutionEngine
{
    private const string ClientName = "GSIP.Execution";
    private const string MojTokenPathMetadataKey = "X-GSIP-TokenEndpointPath";
    private const int MaximumTokenResponseBytes = 64 * 1024;
    private static readonly HashSet<string> BodylessMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };
    private static readonly HashSet<string> ForbiddenConfiguredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization", "Content-Length", "Host",
        "X-Request-ID", "X-Correlation-ID"
    };
    private static readonly Regex HeaderNamePattern = new("^[A-Za-z0-9!#$%&'*+.^_`|~-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly ServiceExecutionRuntimeOptions _options = options.Value;

    public async Task<ServiceExecutionResult> ExecuteAsync(ServiceExecutionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Principal);

        var binding = await securityGate.AuthorizeAsync(command.Principal, command.ServiceId, command.EnvironmentId, cancellationToken);
        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        var service = snapshot.Services.SingleOrDefault(candidate =>
            candidate.Id == binding.ServiceId && candidate.IsCurrent && candidate.Active)
            ?? throw new ServiceExecutionRejectedException();

        var validatedInputs = ValidateInputs(service.Fields, command.Inputs ?? new Dictionary<string, string?>());
        var headerPolicy = ParseConfiguredHeaders(binding.ConfiguredHeadersJson);
        var endpoint = BuildEndpoint(binding, validatedInputs);
        ValidateTransportPolicy(binding);

        AuthProfileDescriptor? profile = null;
        if (binding.AuthProfileId is Guid profileId)
        {
            try
            {
                profile = await authProfiles.GetAsync(profileId, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                throw new ServiceExecutionRejectedException();
            }

            if (!profile.IsEnabled || profile.Id != profileId || profile.Version != binding.AuthProfileVersion)
                throw new ServiceExecutionRejectedException();
        }

        var requestId = $"GSIP-{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString("D");
        var endpointAlias = $"{binding.ServiceCode}:{binding.EnvironmentCode}";
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        var maxAttempts = Math.Clamp(_options.MaxAttempts, 1, 3);
        var retryAllowed = BodylessMethods.Contains(binding.HttpMethod) || headerPolicy.SafeToRetry;
        var client = httpClientFactory.CreateClient(ClientName);

        await metadataCatalog.MarkServiceUsedAsync(service.Id, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(binding.TimeoutSeconds, 1, 300)));

        try
        {
            while (true)
            {
                attempts++;
                using var request = BuildRequest(binding, endpoint, validatedInputs, headerPolicy.Headers, requestId, correlationId);
                try
                {
                    using var response = await SendAuthenticatedAsync(client, request, binding, profile, timeout.Token);
                    var statusCode = (int)response.StatusCode;
                    if (retryAllowed && attempts < maxAttempts && IsTransientStatus(response.StatusCode))
                    {
                        await DelayBeforeRetryAsync(response, timeout.Token);
                        continue;
                    }

                    string rawResponse;
                    try
                    {
                        rawResponse = await ReadBoundedResponseAsync(response.Content, timeout.Token);
                    }
                    catch (ResponseTooLargeException)
                    {
                        return Complete(requestId, correlationId, binding, endpointAlias, statusCode, stopwatch,
                            attempts, ServiceExecutionOutcome.ResponseTooLarge, "ResponseTooLarge", [], string.Empty);
                    }

                    var outcome = ClassifyStatus(response.StatusCode);
                    var structured = outcome == ServiceExecutionOutcome.Success
                        ? MapStructuredResult(service.ResultMappings, rawResponse)
                        : [];
                    return Complete(requestId, correlationId, binding, endpointAlias, statusCode, stopwatch,
                        attempts, outcome, MessageCode(outcome), structured, rawResponse);
                }
                catch (AuthenticationUnavailableException)
                {
                    return Complete(requestId, correlationId, binding, endpointAlias, null, stopwatch,
                        attempts, ServiceExecutionOutcome.AuthenticationUnavailable, "AuthenticationUnavailable", [], string.Empty);
                }
                catch (HttpRequestException exception) when (IsTlsFailure(exception))
                {
                    return Complete(requestId, correlationId, binding, endpointAlias, null, stopwatch,
                        attempts, ServiceExecutionOutcome.TlsFailure, "TlsFailure", [], string.Empty);
                }
                catch (HttpRequestException) when (retryAllowed && attempts < maxAttempts)
                {
                    await Task.Delay(Math.Max(0, _options.RetryDelayMilliseconds), timeout.Token);
                }
                catch (HttpRequestException)
                {
                    return Complete(requestId, correlationId, binding, endpointAlias, null, stopwatch,
                        attempts, ServiceExecutionOutcome.NetworkFailure, "NetworkFailure", [], string.Empty);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    return Complete(requestId, correlationId, binding, endpointAlias, null, stopwatch,
                        attempts, ServiceExecutionOutcome.Timeout, "Timeout", [], string.Empty);
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private ServiceExecutionResult Complete(
        string requestId,
        string correlationId,
        AuthorizedServiceExecutionBinding binding,
        string endpointAlias,
        int? statusCode,
        Stopwatch stopwatch,
        int attempts,
        ServiceExecutionOutcome outcome,
        string messageCode,
        IReadOnlyList<StructuredServiceResultItem> structured,
        string rawResponse)
    {
        stopwatch.Stop();
        logger.LogInformation(
            "GSIP service execution completed RequestId={RequestId} CorrelationId={CorrelationId} Service={ServiceCode} Environment={EnvironmentCode} EndpointAlias={EndpointAlias} StatusCode={StatusCode} DurationMs={DurationMs} Attempts={Attempts} Outcome={Outcome}",
            requestId, correlationId, binding.ServiceCode, binding.EnvironmentCode, endpointAlias, statusCode,
            stopwatch.ElapsedMilliseconds, attempts, outcome);
        return new ServiceExecutionResult(requestId, correlationId, binding.ServiceCode, binding.EnvironmentCode,
            endpointAlias, statusCode, stopwatch.ElapsedMilliseconds, attempts, outcome, messageCode, structured, rawResponse);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpClient client,
        HttpRequestMessage request,
        AuthorizedServiceExecutionBinding binding,
        AuthProfileDescriptor? profile,
        CancellationToken cancellationToken)
    {
        if (profile is null)
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        try
        {
            if (profile.AuthType == AuthProfileType.TokenEndpoint)
                return await SendTokenEndpointAuthenticatedAsync(client, request, binding, profile, cancellationToken);

            var secretHeaders = BuildSecretHeaderPlan(profile);
            return await ResolveSecretAndSendAsync(0);

            Task<HttpResponseMessage> ResolveSecretAndSendAsync(int index)
            {
                if (index >= secretHeaders.Count)
                    return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var planned = secretHeaders[index];
                return secretResolver.UseSecretAsync(
                    binding.ServiceId,
                    binding.EnvironmentId,
                    profile.Id,
                    planned.Secret.Name,
                    planned.Secret.Reference,
                    async (material, token) =>
                    {
                        var secretValue = DecodeHeaderSecret(material);
                        try
                        {
                            ApplySecretHeader(request, planned.Kind, planned.HeaderName, secretValue);
                            return await ResolveSecretAndSendAsync(index + 1);
                        }
                        finally
                        {
                            RemoveSecretHeader(request, planned.Kind, planned.HeaderName);
                        }
                    },
                    cancellationToken);
            }
        }
        catch (SecretReferenceRejectedException)
        {
            throw new AuthenticationUnavailableException();
        }
        catch (SecretProtectionException)
        {
            throw new AuthenticationUnavailableException();
        }
    }

    private async Task<HttpResponseMessage> SendTokenEndpointAuthenticatedAsync(
        HttpClient client,
        HttpRequestMessage request,
        AuthorizedServiceExecutionBinding binding,
        AuthProfileDescriptor profile,
        CancellationToken cancellationToken)
    {
        if (tokenCache is null)
            throw new AuthenticationUnavailableException();

        var tokenPath = ResolveTokenEndpointPath(binding.ConfiguredHeadersJson);
        var usernameSecret = SingleSecret(profile, "username");
        var passwordSecret = SingleSecret(profile, "password");
        if (profile.Secrets.Count != 2)
            throw new AuthenticationUnavailableException();

        return await secretResolver.UseSecretAsync(
            binding.ServiceId,
            binding.EnvironmentId,
            profile.Id,
            usernameSecret.Name,
            usernameSecret.Reference,
            async (usernameMaterial, usernameToken) =>
            {
                var username = DecodeFormSecret(usernameMaterial);
                return await secretResolver.UseSecretAsync(
                    binding.ServiceId,
                    binding.EnvironmentId,
                    profile.Id,
                    passwordSecret.Name,
                    passwordSecret.Reference,
                    async (passwordMaterial, passwordToken) =>
                    {
                        var password = DecodeFormSecret(passwordMaterial);
                        var identity = TokenCacheIdentity.Create(
                            binding.ServiceId,
                            binding.EnvironmentId,
                            profile.Id,
                            profile.Version,
                            Math.Max(usernameSecret.Generation, passwordSecret.Generation),
                            validityParameters: new Dictionary<string, string?>(StringComparer.Ordinal)
                            {
                                ["token-path"] = tokenPath,
                                ["username-generation"] = usernameSecret.Generation.ToString(CultureInfo.InvariantCulture),
                                ["password-generation"] = passwordSecret.Generation.ToString(CultureInfo.InvariantCulture)
                            });

                        var cached = await tokenCache.GetOrRefreshAsync(
                            identity,
                            refreshToken => AcquireMojTokenAsync(client, binding, tokenPath, username, password, refreshToken),
                            passwordToken);
                        var bearer = ValidateBearerToken(cached.AccessToken);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                        try
                        {
                            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, passwordToken);
                        }
                        finally
                        {
                            request.Headers.Authorization = null;
                        }
                    },
                    usernameToken);
            },
            cancellationToken);
    }

    private static AuthProfileSecretDescriptor SingleSecret(AuthProfileDescriptor profile, string name)
    {
        var matches = profile.Secrets
            .Where(secret => string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length != 1 || matches[0].Generation < 1)
            throw new AuthenticationUnavailableException();
        return matches[0];
    }

    private static string ResolveTokenEndpointPath(string? configuredMetadataJson)
    {
        if (string.IsNullOrWhiteSpace(configuredMetadataJson))
            throw new AuthenticationUnavailableException();

        try
        {
            using var document = JsonDocument.Parse(configuredMetadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(MojTokenPathMetadataKey, out var pathElement)
                || pathElement.ValueKind != JsonValueKind.String)
                throw new AuthenticationUnavailableException();

            var path = pathElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith('/', StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal)
                || path.Contains('\\')
                || path.Contains('?')
                || path.Contains('#')
                || path.Any(character => char.IsControl(character)))
                throw new AuthenticationUnavailableException();

            return path;
        }
        catch (JsonException)
        {
            throw new AuthenticationUnavailableException();
        }
    }

    private static async Task<TokenCacheValue> AcquireMojTokenAsync(
        HttpClient client,
        AuthorizedServiceExecutionBinding binding,
        string tokenPath,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        Uri tokenEndpoint;
        try
        {
            if (!Uri.TryCreate(binding.BaseUrl, UriKind.Absolute, out var baseUri)
                || baseUri.Scheme is not ("http" or "https"))
                throw new AuthenticationUnavailableException();
            tokenEndpoint = new Uri(baseUri, tokenPath);
        }
        catch (UriFormatException)
        {
            throw new AuthenticationUnavailableException();
        }

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            ])
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await client.SendAsync(tokenRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new AuthenticationUnavailableException();
        }

        using (tokenResponse)
        {
            if (!tokenResponse.IsSuccessStatusCode)
                throw new AuthenticationUnavailableException();

            byte[] body;
            try
            {
                body = await tokenResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new AuthenticationUnavailableException();
            }

            if (body.Length == 0 || body.Length > MaximumTokenResponseBytes)
                throw new AuthenticationUnavailableException();

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("data", out var tokenElement)
                    || tokenElement.ValueKind != JsonValueKind.String)
                    throw new AuthenticationUnavailableException();

                var accessToken = ValidateBearerToken(tokenElement.GetString());
                return TokenCacheValue.Create(accessToken, ResolveTokenExpiry(accessToken));
            }
            catch (JsonException)
            {
                throw new AuthenticationUnavailableException();
            }
        }
    }

    private static DateTimeOffset ResolveTokenExpiry(string accessToken)
    {
        var now = DateTimeOffset.UtcNow;
        var parts = accessToken.Split('.');
        if (parts.Length != 3)
            return now;

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
                || exp.ValueKind != JsonValueKind.Number
                || !exp.TryGetInt64(out var seconds))
                return now;
            var expiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return expiry > now ? expiry : now;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return now;
        }
    }

    private static string DecodeFormSecret(ReadOnlyMemory<byte> material)
    {
        var value = Encoding.UTF8.GetString(material.Span);
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => character is '\r' or '\n' or '\0'))
            throw new AuthenticationUnavailableException();
        return value;
    }

    private static string ValidateBearerToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 16 * 1024
            || value.Any(character => character is '\r' or '\n' or '\0'))
            throw new AuthenticationUnavailableException();
        return value.Trim();
    }

    private static IReadOnlyList<PlannedSecretHeader> BuildSecretHeaderPlan(AuthProfileDescriptor profile)
    {
        var secrets = profile.Secrets.OrderBy(secret => secret.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return profile.AuthType switch
        {
            AuthProfileType.None when secrets.Length == 0 => [],
            AuthProfileType.ApiKeyHeader when secrets.Length == 1 =>
                [new PlannedSecretHeader(SecretHeaderKind.Header, ValidateSecretHeaderName(secrets[0].Name), secrets[0])],
            AuthProfileType.StaticBearer when secrets.Length == 1 =>
                [new PlannedSecretHeader(SecretHeaderKind.Bearer, "Authorization", secrets[0])],
            AuthProfileType.CustomHeaders when secrets.Length > 0 => secrets
                .Select(secret => new PlannedSecretHeader(SecretHeaderKind.Header, ValidateSecretHeaderName(secret.Name), secret)).ToArray(),
            AuthProfileType.ApiKeyPlusBearer => BuildApiKeyPlusBearerPlan(secrets),
            AuthProfileType.TokenEndpoint => throw new AuthenticationUnavailableException(),
            _ => throw new AuthenticationUnavailableException()
        };
    }

    private static IReadOnlyList<PlannedSecretHeader> BuildApiKeyPlusBearerPlan(AuthProfileSecretDescriptor[] secrets)
    {
        var bearer = secrets.SingleOrDefault(secret => string.Equals(secret.Name, "bearer", StringComparison.OrdinalIgnoreCase));
        var headerSecrets = secrets.Where(secret => !string.Equals(secret.Name, "bearer", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (bearer is null || headerSecrets.Length == 0)
            throw new AuthenticationUnavailableException();

        var result = new List<PlannedSecretHeader>
        {
            new(SecretHeaderKind.Bearer, "Authorization", bearer)
        };
        result.AddRange(headerSecrets.Select(secret =>
            new PlannedSecretHeader(SecretHeaderKind.Header, ValidateSecretHeaderName(secret.Name), secret)));
        return result;
    }

    private static string DecodeHeaderSecret(ReadOnlyMemory<byte> material)
    {
        var value = Encoding.UTF8.GetString(material.Span);
        if (string.IsNullOrEmpty(value) || value.Any(character => character is '\r' or '\n' or '\0'))
            throw new AuthenticationUnavailableException();
        return value;
    }

    private static void ApplySecretHeader(HttpRequestMessage request, SecretHeaderKind kind, string headerName, string value)
    {
        if (kind == SecretHeaderKind.Bearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", value);
            return;
        }

        if (!request.Headers.TryAddWithoutValidation(headerName, value))
            throw new AuthenticationUnavailableException();
    }

    private static void RemoveSecretHeader(HttpRequestMessage request, SecretHeaderKind kind, string headerName)
    {
        if (kind == SecretHeaderKind.Bearer)
            request.Headers.Authorization = null;
        else
            request.Headers.Remove(headerName);
    }

    private static string ValidateSecretHeaderName(string name)
    {
        var normalized = name.Trim();
        if (!HeaderNamePattern.IsMatch(normalized)
            || ForbiddenConfiguredHeaders.Contains(normalized)
            || string.Equals(normalized, ServiceExecutionRuntimeOptions.SafeToRetryMetadataHeader, StringComparison.OrdinalIgnoreCase))
            throw new AuthenticationUnavailableException();
        return normalized;
    }

    private static HttpRequestMessage BuildRequest(
        AuthorizedServiceExecutionBinding binding,
        Uri endpoint,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyDictionary<string, string> configuredHeaders,
        string requestId,
        string correlationId)
    {
        var request = new HttpRequestMessage(new HttpMethod(binding.HttpMethod), endpoint);
        request.Headers.TryAddWithoutValidation("X-Request-ID", requestId);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        foreach (var header in configuredHeaders)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                throw new ServiceExecutionRejectedException();
        }

        if (!BodylessMethods.Contains(binding.HttpMethod) && inputs.Count > 0)
        {
            request.Content = BuildContent(binding.ContentType, inputs);
        }
        return request;
    }

    private static HttpContent BuildContent(string contentType, IReadOnlyDictionary<string, object?> inputs)
    {
        var mediaType = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            var content = new StringContent(JsonSerializer.Serialize(inputs), Encoding.UTF8, "application/json");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            return content;
        }
        if (string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return new FormUrlEncodedContent(inputs.Select(pair =>
                new KeyValuePair<string, string>(pair.Key, Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty)));
        }
        if (string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase) && inputs.Count == 1)
        {
            var content = new StringContent(Convert.ToString(inputs.Values.Single(), CultureInfo.InvariantCulture) ?? string.Empty, Encoding.UTF8);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            return content;
        }
        throw new ServiceExecutionRejectedException();
    }

    private static Uri BuildEndpoint(AuthorizedServiceExecutionBinding binding, IReadOnlyDictionary<string, object?> inputs)
    {
        if (!Uri.TryCreate(binding.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
            throw new ServiceExecutionRejectedException();

        var relative = binding.RelativePath.StartsWith('/') ? binding.RelativePath[1..] : binding.RelativePath;
        if (!Uri.TryCreate(baseUri.ToString().TrimEnd('/') + "/" + relative, UriKind.Absolute, out var endpoint))
            throw new ServiceExecutionRejectedException();

        if (!BodylessMethods.Contains(binding.HttpMethod) || inputs.Count == 0)
            return endpoint;

        var builder = new UriBuilder(endpoint);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
            query.Add(builder.Query.TrimStart('?'));
        query.AddRange(inputs.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty)}"));
        builder.Query = string.Join("&", query.Where(value => value.Length > 0));
        return builder.Uri;
    }

    private static void ValidateTransportPolicy(AuthorizedServiceExecutionBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.ProxyUrl)
            || !binding.ValidateServerCertificate
            || !string.Equals(binding.TlsPolicy, "SystemDefault", StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceExecutionRejectedException();
        }
    }

    private static IReadOnlyDictionary<string, object?> ValidateInputs(
        IEnumerable<ServiceFieldDefinition> fields,
        IReadOnlyDictionary<string, string?> supplied)
    {
        var definitions = fields.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suppliedKey in supplied.Keys)
        {
            if (!definitions.ContainsKey(suppliedKey))
                errors[suppliedKey] = "UnknownField";
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in definitions.Values.OrderBy(field => field.DisplayOrder))
        {
            supplied.TryGetValue(field.Key, out var rawValue);
            var value = rawValue?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                if (field.Required) errors[field.Key] = "Required";
                continue;
            }

            if (field.MinLength is int minLength && value.Length < minLength) errors[field.Key] = "MinLength";
            if (field.MaxLength is int maxLength && value.Length > maxLength) errors[field.Key] = "MaxLength";
            if (!string.IsNullOrWhiteSpace(field.Regex))
            {
                try
                {
                    if (!Regex.IsMatch(value, field.Regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                        errors[field.Key] = "Pattern";
                }
                catch (ArgumentException)
                {
                    errors[field.Key] = "InvalidMetadataPattern";
                }
                catch (RegexMatchTimeoutException)
                {
                    errors[field.Key] = "PatternTimeout";
                }
            }

            object typedValue = value;
            if (IsIntegerType(field.FieldType))
            {
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    errors[field.Key] = "Integer";
                else
                {
                    typedValue = integer;
                    ValidateNumericRange(field, integer, errors);
                }
            }
            else if (IsNumberType(field.FieldType))
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    errors[field.Key] = "Number";
                else
                {
                    typedValue = number;
                    ValidateNumericRange(field, number, errors);
                }
            }
            else if (IsBooleanType(field.FieldType))
            {
                if (!bool.TryParse(value, out var boolean)) errors[field.Key] = "Boolean";
                else typedValue = boolean;
            }

            if (!IsAllowedOption(field.OptionsJson, value)) errors[field.Key] = "Option";
            if (!errors.ContainsKey(field.Key)) result[field.Key] = typedValue;
        }

        if (errors.Count > 0) throw new ServiceExecutionValidationException(errors);
        return result;
    }

    private static void ValidateNumericRange(ServiceFieldDefinition field, decimal value, IDictionary<string, string> errors)
    {
        if (field.Minimum is decimal minimum && value < minimum) errors[field.Key] = "Minimum";
        if (field.Maximum is decimal maximum && value > maximum) errors[field.Key] = "Maximum";
    }

    private static bool IsAllowedOption(string? optionsJson, string value)
    {
        if (string.IsNullOrWhiteSpace(optionsJson) || optionsJson.Trim() == "[]") return true;
        try
        {
            using var document = JsonDocument.Parse(optionsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
            var options = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) options.Add(item.GetString() ?? string.Empty);
                else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var property))
                    options.Add(property.ToString());
            }
            return options.Count == 0 || options.Contains(value, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsIntegerType(string type) => type.Equals("int", StringComparison.OrdinalIgnoreCase)
        || type.Equals("integer", StringComparison.OrdinalIgnoreCase)
        || type.Equals("long", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumberType(string type) => type.Equals("number", StringComparison.OrdinalIgnoreCase)
        || type.Equals("decimal", StringComparison.OrdinalIgnoreCase)
        || type.Equals("double", StringComparison.OrdinalIgnoreCase);

    private static bool IsBooleanType(string type) => type.Equals("bool", StringComparison.OrdinalIgnoreCase)
        || type.Equals("boolean", StringComparison.OrdinalIgnoreCase);

    private static HeaderPolicy ParseConfiguredHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HeaderPolicy(new Dictionary<string, string>(), false);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ServiceExecutionRejectedException();
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var safeToRetry = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, ServiceExecutionRuntimeOptions.SafeToRetryMetadataHeader, StringComparison.OrdinalIgnoreCase))
                {
                    safeToRetry = property.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String when bool.TryParse(property.Value.GetString(), out var parsed) => parsed,
                        _ => throw new ServiceExecutionRejectedException()
                    };
                    continue;
                }

                if (string.Equals(property.Name, MojTokenPathMetadataKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new ServiceExecutionRejectedException();
                    continue;
                }

                if (!HeaderNamePattern.IsMatch(property.Name)
                    || ForbiddenConfiguredHeaders.Contains(property.Name)
                    || property.Name.StartsWith("X-GSIP-", StringComparison.OrdinalIgnoreCase))
                    throw new ServiceExecutionRejectedException();
                var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                if (value is null || value.Any(character => character is '\r' or '\n' or '\0'))
                    throw new ServiceExecutionRejectedException();
                headers[property.Name] = value;
            }
            return new HeaderPolicy(headers, safeToRetry);
        }
        catch (JsonException)
        {
            throw new ServiceExecutionRejectedException();
        }
    }

    private async Task<string> ReadBoundedResponseAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var maxBytes = Math.Clamp(_options.MaxRawResponseBytes, 1024, 4 * 1024 * 1024);
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes) throw new ResponseTooLargeException();
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static IReadOnlyList<StructuredServiceResultItem> MapStructuredResult(
        IEnumerable<ResultMappingDefinition> mappings,
        string rawResponse)
    {
        var ordered = mappings.OrderBy(mapping => mapping.DisplayOrder).ToArray();
        if (ordered.Length == 0 || string.IsNullOrWhiteSpace(rawResponse)) return [];
        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var result = new List<StructuredServiceResultItem>();
            foreach (var mapping in ordered)
            {
                if (!TryResolveJsonPath(document.RootElement, mapping.SourcePath, out var element)) continue;
                var value = element.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? element.GetRawText()
                    : element.ToString();
                value = mapping.Sensitive ? "[MASKED]" : ApplyFormatter(value, mapping.Formatter);
                result.Add(new StructuredServiceResultItem(mapping.SourcePath, mapping.LabelAr, mapping.LabelEn,
                    mapping.ResultType, value, mapping.Sensitive, mapping.DisplayOrder));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryResolveJsonPath(JsonElement root, string path, out JsonElement result)
    {
        result = root;
        var normalized = path.Trim();
        if (normalized.StartsWith("$.", StringComparison.Ordinal)) normalized = normalized[2..];
        else if (normalized == "$") return true;
        if (normalized.Length == 0) return false;

        foreach (var rawSegment in normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segment = rawSegment;
            var bracket = segment.IndexOf('[');
            var propertyName = bracket >= 0 ? segment[..bracket] : segment;
            if (propertyName.Length > 0)
            {
                if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(propertyName, out result)) return false;
            }
            while (bracket >= 0)
            {
                var end = segment.IndexOf(']', bracket + 1);
                if (end < 0 || !int.TryParse(segment[(bracket + 1)..end], out var index)
                    || result.ValueKind != JsonValueKind.Array || index < 0 || index >= result.GetArrayLength()) return false;
                result = result[index];
                bracket = segment.IndexOf('[', end + 1);
            }
        }
        return true;
    }

    private static string ApplyFormatter(string value, string formatter)
    {
        if (string.IsNullOrWhiteSpace(formatter)) return value;
        if (formatter.Equals("uppercase", StringComparison.OrdinalIgnoreCase)) return value.ToUpperInvariant();
        if (formatter.Equals("lowercase", StringComparison.OrdinalIgnoreCase)) return value.ToLowerInvariant();
        if (formatter.Equals("date", StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (formatter.Equals("datetime", StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (Regex.IsMatch(formatter, "^N[0-4]$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50))
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            return number.ToString(formatter.ToUpperInvariant(), CultureInfo.InvariantCulture);
        return value;
    }

    private async Task DelayBeforeRetryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var delay = response.Headers.RetryAfter?.Delta
            ?? TimeSpan.FromMilliseconds(Math.Max(0, _options.RetryDelayMilliseconds));
        if (delay > TimeSpan.FromSeconds(2)) delay = TimeSpan.FromSeconds(2);
        if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode) => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static ServiceExecutionOutcome ClassifyStatus(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code is >= 200 and < 300) return ServiceExecutionOutcome.Success;
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ServiceExecutionOutcome.BadRequest,
            HttpStatusCode.Unauthorized => ServiceExecutionOutcome.Unauthorized,
            HttpStatusCode.Forbidden => ServiceExecutionOutcome.Forbidden,
            HttpStatusCode.NotFound => ServiceExecutionOutcome.NotFound,
            HttpStatusCode.Conflict => ServiceExecutionOutcome.Conflict,
            HttpStatusCode.TooManyRequests => ServiceExecutionOutcome.RateLimited,
            _ when code >= 500 => ServiceExecutionOutcome.ServerError,
            _ => ServiceExecutionOutcome.InvalidResponse
        };
    }

    private static string MessageCode(ServiceExecutionOutcome outcome) => outcome.ToString();

    private static bool IsTlsFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is AuthenticationException) return true;
            if (current.InnerException is null) break;
        }
        return false;
    }

    private sealed record HeaderPolicy(IReadOnlyDictionary<string, string> Headers, bool SafeToRetry);
    private sealed record PlannedSecretHeader(SecretHeaderKind Kind, string HeaderName, AuthProfileSecretDescriptor Secret);
    private enum SecretHeaderKind { Header, Bearer }
    private sealed class AuthenticationUnavailableException : InvalidOperationException;
    private sealed class ResponseTooLargeException : InvalidOperationException;
}
