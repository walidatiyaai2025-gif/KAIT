using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSIP.Application.Authorization;
using GSIP.Application.Secrets;
using GSIP.Application.Security;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
[Authorize(Policy = GsipPermissions.ServicesManage)]
[Route("uat")]
public sealed class UatConfigurationController(
    GsipDbContext db,
    IAuthProfileService authProfiles,
    ISecretVault secretVault,
    SecretRotationPersistenceAdapter rotationPersistence) : Controller
{
    private const string UsernameSecretName = "username";
    private const string PasswordSecretName = "password";
    private const string ApiKeySecretName = "x-api-key";
    private const string AuxiliarySecretName = "geha";
    private const string TokenPathKey = "X-GSIP-TokenEndpointPath";
    private const string TokenGehaFieldKey = "X-GSIP-TokenGehaField";
    private const string TokenGehaValueTypeKey = "X-GSIP-TokenGehaValueType";
    private const string TokenApiKeyRequiredKey = "X-GSIP-TokenApiKeyRequired";
    private const int MaximumSecretCharacters = 4096;

    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(Guid? serviceId, CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(
            serviceId,
            TempData["UatError"] as string,
            TempData["UatSuccess"] as string,
            cancellationToken);
        return View(model);
    }

    [HttpPost("{serviceId:guid}/configure")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Configure(
        Guid serviceId,
        string? username,
        string? password,
        string? apiKey,
        string? auxiliaryCredential,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();

        var service = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.Entity)
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsCurrent, cancellationToken);
        if (service is null)
            return NotFound();

        var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        if (!HasUsableUatContract(config))
            return RedirectWithError(serviceId, "UAT_CONTRACT_REQUIRED");
        if (config!.AuthProfileId is not Guid profileId)
            return RedirectWithError(serviceId, "UAT_AUTH_PROFILE_REQUIRED");

        AuthProfileDescriptor profile;
        try
        {
            profile = await authProfiles.GetAsync(profileId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return RedirectWithError(serviceId, "UAT_AUTH_PROFILE_REQUIRED");
        }

        if (!HasExclusiveOwnerBinding(profile, serviceId))
            return Conflict("The UAT authentication binding is not an exclusive exact owner binding.");

        var credentialContract = ResolveCredentialContract(service.Entity?.Code ?? string.Empty, config, profile.AuthType);
        if (!credentialContract.DirectEditorSupported)
            return RedirectWithError(serviceId, "UAT_AUTH_EDITOR_UNSUPPORTED");

        if (credentialContract.RequiresUsername && !IsAcceptableCredential(username))
            return RedirectWithError(serviceId, "INVALID_USERNAME");
        if (credentialContract.RequiresPassword && !IsAcceptableCredential(password))
            return RedirectWithError(serviceId, "INVALID_PASSWORD");
        if (credentialContract.RequiresApiKey && !IsAcceptableApiKey(apiKey))
            return RedirectWithError(serviceId, "INVALID_API_KEY");
        if (profile.AuthType == AuthProfileType.TokenEndpoint
            && !credentialContract.RequiresApiKey
            && !string.IsNullOrWhiteSpace(apiKey)
            && !IsAcceptableApiKey(apiKey))
            return RedirectWithError(serviceId, "INVALID_API_KEY");
        if (credentialContract.RequiresAuxiliary && !IsAcceptableAuxiliary(auxiliaryCredential, credentialContract.AuxiliaryValueType))
            return RedirectWithError(serviceId, "INVALID_AUXILIARY_CREDENTIAL");

        var allowedSecretNames = AllowedSecretNames(credentialContract, profile.AuthType);
        var existingSecretNames = profile.Secrets.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existingSecretNames.Except(allowedSecretNames, StringComparer.OrdinalIgnoreCase).Any())
            return RedirectWithError(serviceId, "INCOMPATIBLE_SECRET_SLOTS");

        profile = await authProfiles.SetEnabledAsync(profile.Id, false, cancellationToken);
        try
        {
            foreach (var secret in credentialContract.Values(username, password, apiKey, auxiliaryCredential))
                profile = await StoreOrRotateSecretAsync(serviceId, profile, secret.Key, secret.Value, cancellationToken);

            if (profile.AuthType == AuthProfileType.TokenEndpoint
                && !credentialContract.RequiresApiKey
                && !string.IsNullOrWhiteSpace(apiKey))
            {
                profile = await StoreOrRotateSecretAsync(serviceId, profile, ApiKeySecretName, apiKey, cancellationToken);
            }

            profile = await authProfiles.GetAsync(profile.Id, cancellationToken);
            var finalNames = profile.Secrets.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!HasExclusiveOwnerBinding(profile, serviceId)
                || !credentialContract.SecretNames.IsSubsetOf(finalNames)
                || finalNames.Except(allowedSecretNames, StringComparer.OrdinalIgnoreCase).Any())
                throw new InvalidOperationException("The UAT credential profile did not converge to the required exact scope.");

            await authProfiles.SetEnabledAsync(profile.Id, true, cancellationToken);
            var readyStatus = profile.AuthType == AuthProfileType.TokenEndpoint
                ? "READY_FOR_UAT_TOKEN_EXECUTION"
                : "READY_FOR_UAT_BASIC_EXECUTION";
            var updated = await db.ServiceEnvironmentConfigs
                .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Active, true)
                    .SetProperty(item => item.LastTestedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(item => item.LastTestStatus, readyStatus),
                    cancellationToken);
            if (updated != 1)
                throw new InvalidOperationException("The exact UAT configuration changed during credential activation.");

            TempData["UatSuccess"] = readyStatus;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await db.ServiceEnvironmentConfigs
                .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Active, false)
                    .SetProperty(item => item.LastTestStatus, "UAT_CONFIGURATION_FAILED"),
                    CancellationToken.None);
            TempData["UatError"] = "UAT_CONFIGURATION_FAILED";
        }

        return RedirectToAction(nameof(Index), new { serviceId });
    }

    [HttpPost("{serviceId:guid}/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(Guid serviceId, CancellationToken cancellationToken)
    {
        var service = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsCurrent, cancellationToken);
        if (service is null)
            return NotFound();

        var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        if (config is null)
            return RedirectWithError(serviceId, "UAT_CONTRACT_REQUIRED");

        if (config.AuthProfileId is Guid profileId)
        {
            var profile = await authProfiles.GetAsync(profileId, cancellationToken);
            if (!HasExclusiveOwnerBinding(profile, serviceId))
                return Conflict("The UAT authentication binding is not an exclusive exact owner binding.");
            await authProfiles.SetEnabledAsync(profileId, false, cancellationToken);
        }

        var updated = await db.ServiceEnvironmentConfigs
            .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Active, false)
                .SetProperty(item => item.LastTestedAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.LastTestStatus, "DISABLED_BY_ADMIN"),
                cancellationToken);
        if (updated != 1)
            return RedirectWithError(serviceId, "UAT_CONFIGURATION_CHANGED");

        TempData["UatSuccess"] = "UAT_DISABLED";
        return RedirectToAction(nameof(Index), new { serviceId });
    }

    private IActionResult RedirectWithError(Guid serviceId, string error)
    {
        TempData["UatError"] = error;
        return RedirectToAction(nameof(Index), new { serviceId });
    }

    private async Task<UatConfigurationViewModel> BuildModelAsync(
        Guid? requestedServiceId,
        string? errorMessage,
        string? successMessage,
        CancellationToken cancellationToken)
    {
        var services = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.Entity)
            .Include(item => item.EnvironmentConfigs)
            .Where(item => item.IsCurrent)
            .OrderBy(item => item.Entity!.DisplayOrder)
            .ThenBy(item => item.Entity!.Code)
            .ThenBy(item => item.NameEn)
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
            return new UatConfigurationViewModel([], null, errorMessage, successMessage);

        var profileIds = services
            .SelectMany(item => item.EnvironmentConfigs)
            .Where(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId && item.AuthProfileId.HasValue)
            .Select(item => item.AuthProfileId!.Value)
            .Distinct()
            .ToArray();

        var profileById = await db.AuthProfiles
            .AsNoTracking()
            .Include(item => item.Secrets)
            .Include(item => item.Bindings)
            .Where(item => profileIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var selected = requestedServiceId is Guid requested
            ? services.SingleOrDefault(item => item.Id == requested)
            : null;
        selected ??= services.FirstOrDefault(item => item.EnvironmentConfigs.Any(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId));
        selected ??= services[0];

        var options = services.Select(service =>
        {
            var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
            profileById.TryGetValue(config?.AuthProfileId ?? Guid.Empty, out var profile);
            var readiness = EvaluateReadiness(service, config, profile);
            return new UatServiceOptionViewModel(
                service.Id,
                service.EntityId,
                service.Entity?.Code ?? string.Empty,
                service.Entity?.NameAr ?? string.Empty,
                service.Entity?.NameEn ?? string.Empty,
                service.Code,
                service.NameAr,
                service.NameEn,
                readiness.ContractConfigured,
                config?.Active == true,
                readiness.Ready,
                readiness.Status);
        }).ToArray();

        var selectedConfig = selected.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        profileById.TryGetValue(selectedConfig?.AuthProfileId ?? Guid.Empty, out var selectedProfile);
        var selectedReadiness = EvaluateReadiness(selected, selectedConfig, selectedProfile);
        var credentialContract = selectedConfig is null || selectedProfile is null
            ? CredentialContract.Unsupported
            : ResolveCredentialContract(selected.Entity?.Code ?? string.Empty, selectedConfig, selectedProfile.AuthType);

        var detail = new UatServiceDetailViewModel(
            selected.Id,
            selected.EntityId,
            CatalogEnvironmentCodes.UatId,
            selectedConfig?.AuthProfileId,
            selected.Entity?.Code ?? string.Empty,
            selected.Entity?.NameAr ?? string.Empty,
            selected.Entity?.NameEn ?? string.Empty,
            selected.Code,
            selected.NameAr,
            selected.NameEn,
            selectedReadiness.ContractConfigured,
            selectedConfig?.BaseUrl ?? string.Empty,
            selectedConfig?.RelativePath ?? string.Empty,
            selectedConfig?.HttpMethod ?? string.Empty,
            selectedConfig?.ContentType ?? string.Empty,
            selectedConfig?.TimeoutSeconds ?? 0,
            selectedConfig?.ValidateServerCertificate ?? false,
            selectedProfile?.AuthType.ToString() ?? "NotConfigured",
            selectedProfile?.IsEnabled ?? false,
            selectedConfig?.Active ?? false,
            credentialContract.RequiresUsername,
            credentialContract.RequiresPassword,
            credentialContract.RequiresApiKey,
            credentialContract.RequiresAuxiliary,
            credentialContract.AuxiliaryLabel,
            selectedReadiness.CredentialsConfigured,
            selectedReadiness.Ready,
            credentialContract.TokenPath,
            selectedConfig?.LastTestedAtUtc,
            selectedReadiness.Status,
            credentialContract.DirectEditorSupported);

        return new UatConfigurationViewModel(options, detail, errorMessage, successMessage);
    }

    private static Readiness EvaluateReadiness(
        CatalogService service,
        ServiceEnvironmentConfig? config,
        AuthProfile? profile)
    {
        if (!HasUsableUatContract(config))
            return new Readiness(false, false, false, "UAT_CONTRACT_REQUIRED");

        if (config!.AuthProfileId is null)
        {
            var readyWithoutAuth = service.Active && config.Active;
            return new Readiness(true, true, readyWithoutAuth, readyWithoutAuth ? "READY_FOR_UAT_EXECUTION" : config.LastTestStatus);
        }

        if (profile is null || !HasExclusiveOwnerBinding(profile, service.Id))
            return new Readiness(true, false, false, "UAT_AUTH_PROFILE_REQUIRED");

        var contract = ResolveCredentialContract(service.Entity?.Code ?? string.Empty, config, profile.AuthType);
        var secretNames = profile.Secrets.Select(item => item.SecretName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedSecretNames = AllowedSecretNames(contract, profile.AuthType);
        var credentialsConfigured = contract.DirectEditorSupported
            ? contract.SecretNames.IsSubsetOf(secretNames)
                && !secretNames.Except(allowedSecretNames, StringComparer.OrdinalIgnoreCase).Any()
            : secretNames.Count > 0;
        var ready = service.Active && config.Active && profile.IsEnabled && credentialsConfigured;
        var status = ready
            ? profile.AuthType == AuthProfileType.TokenEndpoint ? "READY_FOR_UAT_TOKEN_EXECUTION" : "READY_FOR_UAT_EXECUTION"
            : string.IsNullOrWhiteSpace(config.LastTestStatus) ? "UAT_CREDENTIALS_REQUIRED" : config.LastTestStatus;
        return new Readiness(true, credentialsConfigured, ready, status);
    }

    private static HashSet<string> AllowedSecretNames(CredentialContract contract, AuthProfileType authType)
    {
        var allowed = new HashSet<string>(contract.SecretNames, StringComparer.OrdinalIgnoreCase);
        if (authType == AuthProfileType.TokenEndpoint)
            allowed.Add(ApiKeySecretName);
        return allowed;
    }

    private static bool HasUsableUatContract(ServiceEnvironmentConfig? config) =>
        config is not null
        && config.EnvironmentId == CatalogEnvironmentCodes.UatId
        && !string.IsNullOrWhiteSpace(config.BaseUrl)
        && !string.IsNullOrWhiteSpace(config.RelativePath)
        && !string.IsNullOrWhiteSpace(config.HttpMethod);

    private static CredentialContract ResolveCredentialContract(
        string entityCode,
        ServiceEnvironmentConfig config,
        AuthProfileType authType)
    {
        if (authType == AuthProfileType.TokenEndpoint)
        {
            var metadata = ParseTokenMetadata(config.NonSecretHeadersJson);
            if (metadata is null)
                return CredentialContract.Unsupported;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                UsernameSecretName,
                PasswordSecretName
            };
            if (metadata.ApiKeyRequired)
                names.Add(ApiKeySecretName);
            if (metadata.RequiresAuxiliary)
                names.Add(AuxiliarySecretName);

            return new CredentialContract(
                true,
                true,
                true,
                metadata.ApiKeyRequired,
                metadata.RequiresAuxiliary,
                metadata.AuxiliaryLabel,
                metadata.AuxiliaryValueType,
                metadata.TokenPath,
                names,
                false);
        }

        if (authType == AuthProfileType.CustomHeaders
            && string.Equals(entityCode, "MOE", StringComparison.OrdinalIgnoreCase))
        {
            return new CredentialContract(
                true,
                true,
                true,
                false,
                false,
                string.Empty,
                "string",
                string.Empty,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    BasicAuthenticationTransformHandler.UsernameHeader,
                    BasicAuthenticationTransformHandler.PasswordHeader
                },
                true);
        }

        return CredentialContract.Unsupported;
    }

    private static TokenMetadata? ParseTokenMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryGetNonEmptyString(document.RootElement, TokenPathKey, out var tokenPath))
                return null;

            var requiresAuxiliary = TryGetNonEmptyString(document.RootElement, TokenGehaFieldKey, out var auxiliaryLabel);
            var auxiliaryValueType = TryGetNonEmptyString(document.RootElement, TokenGehaValueTypeKey, out var parsedType)
                ? parsedType
                : "string";
            var apiKeyRequired = false;
            if (document.RootElement.TryGetProperty(TokenApiKeyRequiredKey, out var apiKeyElement))
            {
                apiKeyRequired = apiKeyElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(apiKeyElement.GetString(), out var parsed) => parsed,
                    _ => false
                };
            }

            return new TokenMetadata(tokenPath, apiKeyRequired, requiresAuxiliary, auxiliaryLabel, auxiliaryValueType);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetNonEmptyString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0 && !value.Any(character => character is '\r' or '\n' or '\0');
    }

    private async Task<AuthProfileDescriptor> StoreOrRotateSecretAsync(
        Guid serviceId,
        AuthProfileDescriptor profile,
        string secretName,
        string value,
        CancellationToken cancellationToken)
    {
        var clearBytes = Encoding.UTF8.GetBytes(value);
        try
        {
            var existing = profile.Secrets.SingleOrDefault(secret =>
                string.Equals(secret.Name, secretName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                SecretRef? createdReference = null;
                try
                {
                    var created = await secretVault.CreateActiveAsync(
                        serviceId,
                        CatalogEnvironmentCodes.UatId,
                        profile.Id,
                        secretName,
                        clearBytes,
                        cancellationToken);
                    createdReference = created.Reference;
                    profile = await authProfiles.SetSecretReferenceAsync(profile.Id, secretName, created.Reference, cancellationToken);
                    createdReference = null;
                    return profile;
                }
                finally
                {
                    if (createdReference is SecretRef orphanedReference)
                    {
                        try
                        {
                            await secretVault.RevokeAsync(
                                serviceId,
                                CatalogEnvironmentCodes.UatId,
                                profile.Id,
                                secretName,
                                orphanedReference,
                                CancellationToken.None);
                        }
                        catch
                        {
                            // Best-effort orphan cleanup. The profile remains disabled if configuration fails.
                        }
                    }
                }
            }

            var scope = SecretRotationScope.Create(
                serviceId,
                CatalogEnvironmentCodes.UatId,
                profile.Id,
                existing.Generation);
            await SecretRotationSafety.RotateAsync(
                scope,
                _ => ValueTask.CompletedTask,
                token => rotationPersistence.StageAsync(scope, secretName, clearBytes, token),
                (candidate, token) => rotationPersistence.ActivateAsync(existing.Reference, secretName, candidate, token),
                (candidate, token) => rotationPersistence.DiscardAsync(candidate, token),
                cancellationToken);
            return await authProfiles.GetAsync(profile.Id, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    private static bool IsAcceptableApiKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 8 and <= MaximumSecretCharacters
        && !value.Any(character => character is '\r' or '\n' or '\0');

    private static bool IsAcceptableCredential(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumSecretCharacters
        && !value.Any(character => character is '\r' or '\n' or '\0');

    private static bool IsAcceptableAuxiliary(string? value, string valueType)
    {
        if (!IsAcceptableCredential(value))
            return false;
        return !string.Equals(valueType, "integer", StringComparison.OrdinalIgnoreCase)
            || long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool HasExclusiveOwnerBinding(AuthProfileDescriptor profile, Guid serviceId) =>
        profile.OwnerServiceId == serviceId
        && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.Bindings.Count == 1
        && profile.Bindings[0].ServiceId == serviceId
        && profile.Bindings[0].EnvironmentId == CatalogEnvironmentCodes.UatId
        && !profile.Bindings[0].IsShared;

    private static bool HasExclusiveOwnerBinding(AuthProfile profile, Guid serviceId) =>
        profile.OwnerServiceId == serviceId
        && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
        && profile.Bindings.Count == 1
        && profile.Bindings.Single().ServiceId == serviceId
        && profile.Bindings.Single().EnvironmentId == CatalogEnvironmentCodes.UatId
        && !profile.Bindings.Single().IsShared;

    private sealed record Readiness(bool ContractConfigured, bool CredentialsConfigured, bool Ready, string Status);

    private sealed record TokenMetadata(
        string TokenPath,
        bool ApiKeyRequired,
        bool RequiresAuxiliary,
        string AuxiliaryLabel,
        string AuxiliaryValueType);

    private sealed record CredentialContract(
        bool DirectEditorSupported,
        bool RequiresUsername,
        bool RequiresPassword,
        bool RequiresApiKey,
        bool RequiresAuxiliary,
        string AuxiliaryLabel,
        string AuxiliaryValueType,
        string TokenPath,
        HashSet<string> SecretNames,
        bool IsBasic)
    {
        public static CredentialContract Unsupported { get; } = new(
            false, false, false, false, false, string.Empty, "string", string.Empty,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), false);

        public IEnumerable<KeyValuePair<string, string>> Values(
            string? username,
            string? password,
            string? apiKey,
            string? auxiliary)
        {
            if (IsBasic)
            {
                yield return new(BasicAuthenticationTransformHandler.UsernameHeader, username!);
                yield return new(BasicAuthenticationTransformHandler.PasswordHeader, password!);
                yield break;
            }

            if (RequiresUsername) yield return new(UsernameSecretName, username!);
            if (RequiresPassword) yield return new(PasswordSecretName, password!);
            if (RequiresApiKey) yield return new(ApiKeySecretName, apiKey!);
            if (RequiresAuxiliary) yield return new(AuxiliarySecretName, auxiliary!);
        }
    }
}