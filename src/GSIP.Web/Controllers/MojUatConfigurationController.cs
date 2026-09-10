using System.Data;
using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Authorization;
using GSIP.Application.Secrets;
using GSIP.Application.Security;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Secrets;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
[Authorize(Policy = GsipPermissions.ServicesManage)]
[Route("moj-uat")]
public sealed class MojUatConfigurationController(
    GsipDbContext db,
    IAuthProfileService authProfiles,
    ISecretVault secretVault,
    SecretRotationPersistenceAdapter rotationPersistence) : Controller
{
    private const string ServiceCode = "MARRIAGECASES";
    private const string UatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage";
    private const string RelativePath = "/marriageCasesAPIGEE";
    private const string ApiKeySecretName = "x-api-key";
    private const int MinimumSecretCharacters = 8;
    private const int MaximumSecretCharacters = 4096;
    private static readonly HashSet<string> LegacyTokenSecretNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "username", "password", "bearer", "token"
    };

    [HttpGet("")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(
            TempData["MojUatError"] as string,
            TempData["MojUatSuccess"] as string,
            cancellationToken);
        return View(model);
    }

    [HttpPost("api129/configure")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ConfigureApi129(string apiKey, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(apiKey));
        if (!IsAcceptableSecretValue(apiKey))
        {
            TempData["MojUatError"] = "INVALID_API_KEY";
            return RedirectToAction(nameof(Index));
        }

        var service = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Code == ServiceCode && item.IsCurrent, cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        var environmentExists = await db.CatalogEnvironments
            .AsNoTracking()
            .AnyAsync(item => item.Id == CatalogEnvironmentCodes.UatId && item.Code == CatalogEnvironmentCodes.Uat, cancellationToken);
        if (!environmentExists)
        {
            return NotFound();
        }

        var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        if (config is null)
        {
            return NotFound();
        }

        AuthProfileDescriptor profile;
        if (config.AuthProfileId is Guid profileId)
        {
            profile = await authProfiles.GetAsync(profileId, cancellationToken);
        }
        else
        {
            profile = await authProfiles.CreateAsync(
                new CreateAuthProfileCommand(
                    service.Id,
                    CatalogEnvironmentCodes.UatId,
                    "MOJ API 129 UAT API key",
                    AuthProfileType.ApiKeyHeader,
                    User.Identity?.Name ?? "GSIP-UAT-ADMIN"),
                cancellationToken);
        }

        if (!HasExclusiveOwnerBinding(profile, service.Id))
        {
            return Conflict("The UAT authentication binding is not an exclusive exact owner binding.");
        }

        var incompatibleSecrets = profile.Secrets
            .Where(secret => !string.Equals(secret.Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (incompatibleSecrets.Any(secret => !LegacyTokenSecretNames.Contains(secret.Name))
            || (incompatibleSecrets.Length != 0 && profile.AuthType != AuthProfileType.TokenEndpoint))
        {
            TempData["MojUatError"] = "INCOMPATIBLE_SECRET_SLOTS";
            return RedirectToAction(nameof(Index));
        }

        // Fail closed before changing any authentication shape or secret reference.
        profile = await authProfiles.SetEnabledAsync(profile.Id, false, cancellationToken);

        try
        {
            // Retire only the known obsolete token-flow slots and convert the profile/config
            // inside one serializable database transaction. ISecretVault is scoped to the same
            // DbContext, so a failed slot delete or profile/config update rolls the revoke back.
            await using (var transitionTransaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                try
                {
                    foreach (var legacy in incompatibleSecrets)
                    {
                        await secretVault.RevokeAsync(
                            service.Id,
                            CatalogEnvironmentCodes.UatId,
                            profile.Id,
                            legacy.Name,
                            legacy.Reference,
                            cancellationToken);

                        var deleted = await db.AuthProfileSecrets
                            .Where(slot => slot.AuthProfileId == profile.Id
                                && slot.SecretName == legacy.Name
                                && slot.SecretReference == legacy.Reference.Value
                                && slot.Generation == legacy.Generation)
                            .ExecuteDeleteAsync(cancellationToken);
                        if (deleted != 1)
                        {
                            throw new InvalidOperationException("The legacy secret slot changed during UAT conversion.");
                        }
                    }

                    profile = await authProfiles.UpdateAsync(
                        new UpdateAuthProfileCommand(profile.Id, "MOJ API 129 UAT API key", AuthProfileType.ApiKeyHeader),
                        cancellationToken);

                    await db.CatalogServices
                        .Where(item => item.Id == service.Id && item.IsCurrent)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Active, true), cancellationToken);
                    await db.CatalogEnvironments
                        .Where(item => item.Id == CatalogEnvironmentCodes.UatId && item.Code == CatalogEnvironmentCodes.Uat)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Active, true), cancellationToken);
                    var configUpdated = await db.ServiceEnvironmentConfigs
                        .Where(item => item.ServiceId == service.Id && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(item => item.BaseUrl, UatBaseUrl)
                            .SetProperty(item => item.RelativePath, RelativePath)
                            .SetProperty(item => item.HttpMethod, "POST")
                            .SetProperty(item => item.ContentType, "application/x-www-form-urlencoded")
                            .SetProperty(item => item.NonSecretHeadersJson, "{}")
                            .SetProperty(item => item.TimeoutSeconds, 30)
                            .SetProperty(item => item.TlsPolicy, "SystemDefault")
                            .SetProperty(item => item.ValidateServerCertificate, true)
                            .SetProperty(item => item.ProxyUrl, string.Empty)
                            .SetProperty(item => item.Active, true)
                            .SetProperty(item => item.AuthProfileId, profile.Id)
                            .SetProperty(item => item.LastTestedAtUtc, (DateTimeOffset?)null)
                            .SetProperty(item => item.LastTestStatus, "API_KEY_CONFIGURATION_IN_PROGRESS"),
                            cancellationToken);
                    if (configUpdated != 1)
                    {
                        throw new InvalidOperationException("The exact UAT configuration changed during update.");
                    }

                    await transitionTransaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transitionTransaction.RollbackAsync(CancellationToken.None);
                    db.ChangeTracker.Clear();
                    throw;
                }
            }

            db.ChangeTracker.Clear();

            var clearBytes = Encoding.UTF8.GetBytes(apiKey);
            try
            {
                profile = await authProfiles.GetAsync(profile.Id, cancellationToken);
                var existing = profile.Secrets.SingleOrDefault(secret =>
                    string.Equals(secret.Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    SecretRef? createdReference = null;
                    try
                    {
                        var created = await secretVault.CreateActiveAsync(
                            service.Id,
                            CatalogEnvironmentCodes.UatId,
                            profile.Id,
                            ApiKeySecretName,
                            clearBytes,
                            cancellationToken);
                        createdReference = created.Reference;
                        await authProfiles.SetSecretReferenceAsync(profile.Id, ApiKeySecretName, created.Reference, cancellationToken);
                        createdReference = null;
                    }
                    finally
                    {
                        if (createdReference is SecretRef orphanedReference)
                        {
                            try
                            {
                                await secretVault.RevokeAsync(
                                    service.Id,
                                    CatalogEnvironmentCodes.UatId,
                                    profile.Id,
                                    ApiKeySecretName,
                                    orphanedReference,
                                    CancellationToken.None);
                            }
                            catch
                            {
                                // Best-effort cleanup only; the AuthProfile remains disabled.
                            }
                        }
                    }
                }
                else
                {
                    var scope = SecretRotationScope.Create(
                        service.Id,
                        CatalogEnvironmentCodes.UatId,
                        profile.Id,
                        existing.Generation);
                    await SecretRotationSafety.RotateAsync(
                        scope,
                        _ => ValueTask.CompletedTask,
                        token => rotationPersistence.StageAsync(scope, ApiKeySecretName, clearBytes, token),
                        (candidate, token) => rotationPersistence.ActivateAsync(existing.Reference, ApiKeySecretName, candidate, token),
                        (candidate, token) => rotationPersistence.DiscardAsync(candidate, token),
                        cancellationToken);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clearBytes);
            }

            profile = await authProfiles.GetAsync(profile.Id, cancellationToken);
            if (!HasExclusiveOwnerBinding(profile, service.Id)
                || profile.AuthType != AuthProfileType.ApiKeyHeader
                || profile.Secrets.Count != 1
                || !string.Equals(profile.Secrets[0].Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The API-key-only UAT profile did not converge to the exact expected scope.");
            }

            await authProfiles.SetEnabledAsync(profile.Id, true, cancellationToken);
            await SetStatusAsync(service.Id, "READY_FOR_UAT_EXECUTION", cancellationToken);
            TempData["MojUatSuccess"] = "READY_FOR_UAT_EXECUTION";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await SetStatusAsync(service.Id, "API_KEY_CONFIGURATION_FAILED", CancellationToken.None);
            TempData["MojUatError"] = "API_KEY_CONFIGURATION_FAILED";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("api129/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableApi129(CancellationToken cancellationToken)
    {
        var service = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Code == ServiceCode && item.IsCurrent, cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        var config = service.EnvironmentConfigs.SingleOrDefault(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        if (config is null)
        {
            return NotFound();
        }

        if (config.AuthProfileId is Guid profileId)
        {
            var profile = await authProfiles.GetAsync(profileId, cancellationToken);
            if (!HasExclusiveOwnerBinding(profile, service.Id))
            {
                await DisableConfigurationOnlyAsync(service.Id, "BINDING_MISMATCH_DISABLED", cancellationToken);
                return Conflict("The UAT authentication binding is not an exclusive exact owner binding.");
            }

            await authProfiles.SetEnabledAsync(profileId, false, cancellationToken);
        }

        await DisableConfigurationOnlyAsync(service.Id, "DISABLED_BY_ADMIN", cancellationToken);
        TempData["MojUatSuccess"] = "UAT_DISABLED";
        return RedirectToAction(nameof(Index));
    }

    private async Task DisableConfigurationOnlyAsync(
        Guid serviceId,
        string status,
        CancellationToken cancellationToken)
    {
        var updated = await db.ServiceEnvironmentConfigs
            .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Active, false)
                .SetProperty(item => item.LastTestStatus, status)
                .SetProperty(item => item.LastTestedAtUtc, (DateTimeOffset?)null),
                cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("The exact UAT configuration changed while disabling it.");
        }
    }

    private async Task SetStatusAsync(Guid serviceId, string status, CancellationToken cancellationToken)
    {
        await db.ServiceEnvironmentConfigs
            .Where(item => item.ServiceId == serviceId && item.EnvironmentId == CatalogEnvironmentCodes.UatId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LastTestStatus, status), cancellationToken);
    }

    private async Task<MojUatConfigurationViewModel> BuildModelAsync(
        string? errorMessage,
        string? successMessage,
        CancellationToken cancellationToken)
    {
        var service = await db.CatalogServices
            .AsNoTracking()
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Code == ServiceCode && item.IsCurrent, cancellationToken)
            ?? throw new InvalidOperationException("The current Marriage Cases service metadata is missing.");

        var environment = await db.CatalogEnvironments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == CatalogEnvironmentCodes.UatId && item.Code == CatalogEnvironmentCodes.Uat, cancellationToken)
            ?? throw new InvalidOperationException("The canonical UAT environment is missing.");

        var config = service.EnvironmentConfigs.Single(item => item.EnvironmentId == CatalogEnvironmentCodes.UatId);
        AuthProfileDescriptor? profile = null;
        if (config.AuthProfileId is Guid profileId)
        {
            try
            {
                profile = await authProfiles.GetAsync(profileId, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                profile = null;
            }
        }

        var apiKeyConfigured = profile is not null
            && profile.Secrets.Count == 1
            && string.Equals(profile.Secrets[0].Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase);
        var exactContract = string.Equals(config.BaseUrl, UatBaseUrl, StringComparison.Ordinal)
            && string.Equals(config.RelativePath, RelativePath, StringComparison.Ordinal)
            && string.Equals(config.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            && string.Equals(config.ContentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            && string.Equals(config.NonSecretHeadersJson.Trim(), "{}", StringComparison.Ordinal);
        var exactBinding = profile is not null && HasExclusiveOwnerBinding(profile, service.Id);
        var ready = service.Active
            && environment.Active
            && config.Active
            && exactContract
            && exactBinding
            && profile?.AuthType == AuthProfileType.ApiKeyHeader
            && profile.IsEnabled
            && apiKeyConfigured;

        var status = ready
            ? "READY_FOR_UAT_EXECUTION"
            : apiKeyConfigured
                ? "CONFIGURATION_REVIEW_REQUIRED"
                : "X_API_KEY_REQUIRED";

        return new MojUatConfigurationViewModel(
            service.EntityId,
            service.Id,
            CatalogEnvironmentCodes.UatId,
            profile?.Id,
            service.Code,
            service.NameAr,
            service.NameEn,
            config.BaseUrl,
            config.RelativePath,
            config.HttpMethod,
            config.ContentType,
            service.Active,
            environment.Active,
            config.Active,
            profile?.AuthType.ToString() ?? "None",
            profile?.IsEnabled ?? false,
            apiKeyConfigured,
            ready,
            status,
            errorMessage,
            successMessage);
    }

    private static bool HasExclusiveOwnerBinding(AuthProfileDescriptor profile, Guid serviceId)
    {
        if (profile.OwnerServiceId != serviceId
            || profile.OwnerEnvironmentId != CatalogEnvironmentCodes.UatId
            || profile.Bindings.Count != 1)
        {
            return false;
        }

        var binding = profile.Bindings[0];
        return binding.ServiceId == serviceId
            && binding.EnvironmentId == CatalogEnvironmentCodes.UatId
            && !binding.IsShared;
    }

    private static bool IsAcceptableSecretValue(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= MinimumSecretCharacters and <= MaximumSecretCharacters
        && !value.Any(character => character is '\r' or '\n' or '\0');
}
