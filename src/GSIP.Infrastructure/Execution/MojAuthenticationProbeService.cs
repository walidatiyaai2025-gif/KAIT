using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Secrets;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Administration diagnostic for the canonical MOJ TokenEndpoint contract.
/// It performs only a fresh token-generation probe, returns no token material,
/// and validates the exact catalog/AuthProfile binding before secret resolution.
/// It is not a second service-execution runtime.
/// </summary>
public sealed class MojAuthenticationProbeService(
    IMetadataCatalogService metadataCatalog,
    IAuthProfileService authProfiles,
    ISecretMaterialResolver secretResolver,
    IHttpClientFactory httpClientFactory) : IAuthenticationProbeService
{
    private const string ClientName = "GSIP.Execution";
    private const string TokenPathMetadataKey = "X-GSIP-TokenEndpointPath";
    private const string ApiKeySecretName = "x-api-key";
    private const int MaximumTokenResponseBytes = 64 * 1024;

    public async Task TestTokenGenerationAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        CancellationToken cancellationToken = default)
    {
        if (serviceId == Guid.Empty || environmentId == Guid.Empty || authProfileId == Guid.Empty)
            throw new AuthenticationProbeRejectedException();

        try
        {
            var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
            var serviceMatches = snapshot.Services
                .Where(service => service.Id == serviceId && service.IsCurrent && service.Active)
                .Take(2)
                .ToArray();
            if (serviceMatches.Length != 1)
                throw new AuthenticationProbeRejectedException();

            var environmentMatches = snapshot.Environments
                .Where(environment => environment.Id == environmentId && environment.Active)
                .Take(2)
                .ToArray();
            if (environmentMatches.Length != 1)
                throw new AuthenticationProbeRejectedException();

            var configMatches = serviceMatches[0].EnvironmentConfigs
                .Where(config => config.ServiceId == serviceId
                    && config.EnvironmentId == environmentId
                    && config.Active
                    && config.AuthProfileId == authProfileId)
                .Take(2)
                .ToArray();
            if (configMatches.Length != 1)
                throw new AuthenticationProbeRejectedException();

            var profile = await authProfiles.GetAsync(authProfileId, cancellationToken);
            if (!profile.IsEnabled || profile.Id != authProfileId || profile.AuthType != AuthProfileType.TokenEndpoint)
                throw new AuthenticationProbeRejectedException();

            var exactBindings = profile.Bindings
                .Where(binding => binding.ServiceId == serviceId && binding.EnvironmentId == environmentId)
                .Take(2)
                .ToArray();
            if (exactBindings.Length != 1)
                throw new AuthenticationProbeRejectedException();

            var ownsExactScope = profile.OwnerServiceId == serviceId && profile.OwnerEnvironmentId == environmentId;
            if (!ownsExactScope && !exactBindings[0].IsShared)
                throw new AuthenticationProbeRejectedException();

            var usernameSecret = SingleSecret(profile, "username");
            var passwordSecret = SingleSecret(profile, "password");
            var apiKeySecret = OptionalSingleSecret(profile, ApiKeySecretName);
            if (profile.Secrets.Count != (apiKeySecret is null ? 2 : 3))
                throw new AuthenticationProbeRejectedException();

            var config = configMatches[0];
            var tokenPath = ResolveTokenPath(config.NonSecretHeadersJson);
            var client = httpClientFactory.CreateClient(ClientName);

            await secretResolver.UseSecretAsync(
                serviceId,
                environmentId,
                profile.Id,
                usernameSecret.Name,
                usernameSecret.Reference,
                async (usernameMaterial, usernameToken) =>
                {
                    var username = DecodeSecret(usernameMaterial);
                    return await secretResolver.UseSecretAsync(
                        serviceId,
                        environmentId,
                        profile.Id,
                        passwordSecret.Name,
                        passwordSecret.Reference,
                        async (passwordMaterial, passwordToken) =>
                        {
                            var password = DecodeSecret(passwordMaterial);
                            if (apiKeySecret is null)
                            {
                                await ProbeTokenEndpointAsync(
                                    client,
                                    config.BaseUrl,
                                    tokenPath,
                                    username,
                                    password,
                                    null,
                                    passwordToken);
                                return true;
                            }

                            return await secretResolver.UseSecretAsync(
                                serviceId,
                                environmentId,
                                profile.Id,
                                apiKeySecret.Name,
                                apiKeySecret.Reference,
                                async (apiKeyMaterial, apiKeyToken) =>
                                {
                                    var apiKey = DecodeSecret(apiKeyMaterial);
                                    await ProbeTokenEndpointAsync(
                                        client,
                                        config.BaseUrl,
                                        tokenPath,
                                        username,
                                        password,
                                        apiKey,
                                        apiKeyToken);
                                    return true;
                                },
                                passwordToken);
                        },
                        usernameToken);
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AuthenticationProbeRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            SecretReferenceRejectedException or
            SecretProtectionException or
            KeyNotFoundException or
            InvalidOperationException or
            ArgumentException or
            HttpRequestException)
        {
            throw new AuthenticationProbeRejectedException();
        }
    }

    private static AuthProfileSecretDescriptor SingleSecret(AuthProfileDescriptor profile, string name)
    {
        var matches = profile.Secrets
            .Where(secret => string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length != 1 || matches[0].Generation < 1)
            throw new AuthenticationProbeRejectedException();
        return matches[0];
    }

    private static AuthProfileSecretDescriptor? OptionalSingleSecret(AuthProfileDescriptor profile, string name)
    {
        var matches = profile.Secrets
            .Where(secret => string.Equals(secret.Name, name, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length > 1 || (matches.Length == 1 && matches[0].Generation < 1))
            throw new AuthenticationProbeRejectedException();
        return matches.SingleOrDefault();
    }

    private static string ResolveTokenPath(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            throw new AuthenticationProbeRejectedException();

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(TokenPathMetadataKey, out var value)
                || value.ValueKind != JsonValueKind.String)
                throw new AuthenticationProbeRejectedException();

            var path = value.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith("/", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal)
                || path.Contains('\\')
                || path.Contains('?')
                || path.Contains('#')
                || path.Any(char.IsControl))
                throw new AuthenticationProbeRejectedException();

            return path;
        }
        catch (JsonException)
        {
            throw new AuthenticationProbeRejectedException();
        }
    }

    private static string DecodeSecret(ReadOnlyMemory<byte> material)
    {
        var value = Encoding.UTF8.GetString(material.Span);
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => character is '\r' or '\n' or '\0'))
            throw new AuthenticationProbeRejectedException();
        return value;
    }

    private static async Task ProbeTokenEndpointAsync(
        HttpClient client,
        string baseUrl,
        string tokenPath,
        string username,
        string password,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
            throw new AuthenticationProbeRejectedException();

        var relativeTokenPath = tokenPath.StartsWith('/') ? tokenPath[1..] : tokenPath;
        if (!Uri.TryCreate(baseUri.ToString().TrimEnd('/') + "/" + relativeTokenPath, UriKind.Absolute, out var endpoint))
            throw new AuthenticationProbeRejectedException();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            ])
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (apiKey is not null && !request.Headers.TryAddWithoutValidation(ApiKeySecretName, apiKey))
            throw new AuthenticationProbeRejectedException();

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        finally
        {
            if (apiKey is not null)
                request.Headers.Remove(ApiKeySecretName);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new AuthenticationProbeRejectedException();

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (body.Length == 0 || body.Length > MaximumTokenResponseBytes)
                throw new AuthenticationProbeRejectedException();

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("data", out var tokenElement)
                    || tokenElement.ValueKind != JsonValueKind.String)
                    throw new AuthenticationProbeRejectedException();

                var token = tokenElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(token)
                    || token.Length > 16 * 1024
                    || token.Any(character => character is '\r' or '\n' or '\0')
                    || IsExplicitlyExpiredJwt(token))
                    throw new AuthenticationProbeRejectedException();
            }
            catch (JsonException)
            {
                throw new AuthenticationProbeRejectedException();
            }
        }
    }

    private static bool IsExplicitlyExpiredJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return false;

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
            return document.RootElement.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number
                && exp.TryGetInt64(out var seconds)
                && DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
