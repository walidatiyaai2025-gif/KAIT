using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Abstractions;
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
    SecretRotationPersistenceAdapter rotationPersistence,
    ISystemClock clock) : Controller
{
    private const string ServiceCode = "MARRIAGECASES";
    private const string UatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage";
    private const string RelativePath = "/marriageCasesAPIGEE";
    private const string ApiKeySecretName = "x-api-key";
    private const int MinimumSecretCharacters = 8;
    private const int MaximumSecretCharacters = 4096;

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
    public async Task<IActionResult> ConfigureApi129(
        string apiKey,
        CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(apiKey));
        if (!IsAcceptableSecretValue(apiKey))
        {
            TempData["MojUatError"] = "INVALID_API_KEY";
            return RedirectToAction(nameof(Index));
        }

        var service = await db.CatalogServices
            .Include(item => item.EnvironmentConfigs)
            .SingleOrDefaultAsync(item => item.Code == ServiceCode && item.IsCurrent, cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        var environment = await db.CatalogEnvironments
            .SingleOrDefaultAsync(item => item.Id == CatalogEnvironmentCodes.UatId && item.Code == CatalogEnvironmentCodes.Uat, cancellationToken);
        if (environment is null)
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
            if (profile.OwnerServiceId != service.Id
                || profile.OwnerEnvironmentId != CatalogEnvironmentCodes.UatId
                || profile.Bindings.Count(binding => binding.ServiceId == service.Id && binding.EnvironmentId == CatalogEnvironmentCodes.UatId) != 1)
            {
                return Conflict("The UAT authentication binding is not an exact owner binding.");
            }
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

        var incompatibleSecrets = profile.Secrets
            .Where(secret => !string.Equals(secret.Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase))
            .Select(secret => secret.Name)
            .ToArray();
        if (incompatibleSecrets.Length != 0)
        {
            TempData["MojUatError"] = "INCOMPATIBLE_SECRET_SLOTS";
            return RedirectToAction(nameof(Index));
        }

        // Fail closed while the profile is being reconfigured. No request can run with
        // a partially changed authentication contract.
        profile = await authProfiles.SetEnabledAsync(profile.Id, false, cancellationToken);
        profile = await authProfiles.UpdateAsync(
            new UpdateAuthProfileCommand(profile.Id, "MOJ API 129 UAT API key", AuthProfileType.ApiKeyHeader),
            cancellationToken);

        service.Active = true;
        service.UpdatedAtUtc = clock.UtcNow;
        environment.Active = true;
        config.BaseUrl = UatBaseUrl;
        config.RelativePath = RelativePath;
        config.HttpMethod = "POST";
        config.ContentType = "application/x-www-form-urlencoded";
        config.NonSecretHeadersJson = "{}";
        config.TimeoutSeconds = 30;
        config.TlsPolicy = "SystemDefault";
        config.ValidateServerCertificate = true;
        config.ProxyUrl = string.Empty;
        config.Active = true;
        config.AuthProfileId = profile.Id;
        config.LastTestedAtUtc = null;
        config.LastTestStatus = "API_KEY_CONFIGURATION_IN_PROGRESS";
        await db.SaveChangesAsync(cancellationToken);

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
                            // Best-effort cleanup only. The profile remains disabled on failure.
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
        catch
        {
            config.LastTestStatus = "API_KEY_CONFIGURATION_FAILED";
            await db.SaveChangesAsync(CancellationToken.None);
            TempData["MojUatError"] = "API_KEY_CONFIGURATION_FAILED";
            return RedirectToAction(nameof(Index));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }

        await authProfiles.SetEnabledAsync(profile.Id, true, cancellationToken);
        config.LastTestStatus = "READY_FOR_UAT_EXECUTION";
        await db.SaveChangesAsync(cancellationToken);

        TempData["MojUatSuccess"] = "READY_FOR_UAT_EXECUTION";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("api129/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableApi129(CancellationToken cancellationToken)
    {
        var service = await db.CatalogServices
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
            await authProfiles.SetEnabledAsync(profileId, false, cancellationToken);
        }

        config.Active = false;
        config.LastTestStatus = "DISABLED_BY_ADMIN";
        config.LastTestedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        TempData["MojUatSuccess"] = "UAT_DISABLED";
        return RedirectToAction(nameof(Index));
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

        var apiKeyConfigured = profile?.Secrets.Count(secret =>
            string.Equals(secret.Name, ApiKeySecretName, StringComparison.OrdinalIgnoreCase)) == 1;
        var exactContract = string.Equals(config.BaseUrl, UatBaseUrl, StringComparison.Ordinal)
            && string.Equals(config.RelativePath, RelativePath, StringComparison.Ordinal)
            && string.Equals(config.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            && string.Equals(config.ContentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            && string.Equals(config.NonSecretHeadersJson.Trim(), "{}", StringComparison.Ordinal);
        var exactBinding = profile is not null
            && profile.OwnerServiceId == service.Id
            && profile.OwnerEnvironmentId == CatalogEnvironmentCodes.UatId
            && profile.Bindings.Count(binding => binding.ServiceId == service.Id && binding.EnvironmentId == CatalogEnvironmentCodes.UatId) == 1;
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

    private static bool IsAcceptableSecretValue(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= MinimumSecretCharacters and <= MaximumSecretCharacters
        && !value.Any(character => character is '\r' or '\n' or '\0');
}
