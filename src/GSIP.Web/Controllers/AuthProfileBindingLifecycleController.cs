using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

/// <summary>
/// Exposes the canonical P06 shared-binding removal lifecycle. Persistence stays
/// exclusively in IAuthProfileService; this surface validates exact catalog and
/// profile scope and never accepts secret references or plaintext.
/// </summary>
[Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
[Route("auth-profiles")]
public sealed class AuthProfileBindingLifecycleController(
    IMetadataCatalogService catalog,
    IAuthProfileService authProfiles) : Controller
{
    [HttpPost("{authProfileId:guid}/bindings/{serviceId:guid}/{environmentId:guid}/unbind")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnbindShared(
        Guid authProfileId,
        Guid serviceId,
        Guid environmentId,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (authProfileId == Guid.Empty || serviceId == Guid.Empty || environmentId == Guid.Empty)
        {
            return NotFound();
        }

        var profile = await FindProfileAsync(authProfileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        if (!ProfileBelongsToCatalog(profile, snapshot))
        {
            return NotFound();
        }

        var binding = profile.Bindings.SingleOrDefault(candidate =>
            candidate.ServiceId == serviceId && candidate.EnvironmentId == environmentId);
        if (binding is null)
        {
            return NotFound();
        }
        if (!binding.IsShared)
        {
            return Conflict("The owner AuthProfile binding cannot be removed.");
        }

        var service = snapshot.Services.SingleOrDefault(candidate =>
            candidate.Id == serviceId && candidate.IsCurrent);
        var config = service?.EnvironmentConfigs.SingleOrDefault(candidate =>
            candidate.EnvironmentId == environmentId);
        if (service is null || config is null || config.AuthProfileId != profile.Id)
        {
            return NotFound();
        }

        try
        {
            await authProfiles.UnbindAsync(
                new UnbindAuthProfileCommand(profile.Id, serviceId, environmentId),
                cancellationToken);
            TempData["AuthProfileSuccess"] = "success";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["AuthProfileError"] = "rejected";
        }

        return RedirectToAction("Index", "AuthProfiles", new { culture = NormalizeCulture(culture) });
    }

    private async Task<AuthProfileDescriptor?> FindProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await authProfiles.GetAsync(profileId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool ProfileBelongsToCatalog(
        AuthProfileDescriptor profile,
        MetadataCatalogSnapshot snapshot)
    {
        foreach (var binding in profile.Bindings)
        {
            var service = snapshot.Services.SingleOrDefault(candidate =>
                candidate.Id == binding.ServiceId && candidate.IsCurrent);
            if (service?.EnvironmentConfigs.Any(config =>
                    config.EnvironmentId == binding.EnvironmentId
                    && config.AuthProfileId == profile.Id) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";
}
