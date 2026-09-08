using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
[Route("auth-profiles")]
public sealed class AuthProfilesController(
    IMetadataCatalogService catalog,
    IAuthProfileService authProfiles,
    ISecretVault secretVault) : Controller
{
    private const int MinimumSecretCharacters = 8;
    private const int MaximumSecretCharacters = 4096;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        var model = await BuildViewModelAsync(snapshot, cancellationToken);
        return View(model with
        {
            ErrorMessage = TempData["AuthProfileError"] as string,
            SuccessMessage = TempData["AuthProfileSuccess"] as string
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string name,
        string authenticationType,
        string ownerBinding,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!TryParseBindingTarget(ownerBinding, out var target)
            || !TryParseAuthType(authenticationType, out var authType))
        {
            return BadRequest("Invalid AuthProfile request.");
        }

        var actor = GetActor();
        if (actor is null)
        {
            return Forbid();
        }

        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        if (!TryResolveExactTarget(snapshot, target, out var service, out var config)
            || !service.Active
            || !config.Active)
        {
            return NotFound();
        }
        if (config.AuthProfileId.HasValue)
        {
            return Conflict("The exact Service + Environment already has an AuthProfile binding.");
        }

        try
        {
            await authProfiles.CreateAsync(
                new CreateAuthProfileCommand(service.Id, config.EnvironmentId, name, authType, actor),
                cancellationToken);
            SetSuccess();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            SetRejected();
        }

        return RedirectToIndex(culture);
    }

    [HttpPost("{authProfileId:guid}/share")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(
        Guid authProfileId,
        string targetBinding,
        string reason,
        bool confirmShare,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!confirmShare || !TryParseBindingTarget(targetBinding, out var target))
        {
            return BadRequest("Explicit sharing confirmation and an exact target are required.");
        }

        var actor = GetActor();
        if (actor is null)
        {
            return Forbid();
        }

        var profile = await FindProfileAsync(authProfileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        if (!profile.IsEnabled)
        {
            return Conflict("A disabled AuthProfile cannot be shared.");
        }

        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        if (!ProfileBelongsToCatalog(profile, snapshot)
            || !TryResolveExactTarget(snapshot, target, out var targetService, out var targetConfig)
            || !targetService.Active
            || !targetConfig.Active)
        {
            return NotFound();
        }
        if (targetConfig.AuthProfileId.HasValue)
        {
            return Conflict("The exact target already has an AuthProfile binding.");
        }
        if (profile.OwnerServiceId == targetService.Id && profile.OwnerEnvironmentId == targetConfig.EnvironmentId)
        {
            return Conflict("The owner binding cannot be converted into a shared binding.");
        }

        try
        {
            await authProfiles.ShareAsync(
                new ShareAuthProfileCommand(
                    profile.Id,
                    targetService.Id,
                    targetConfig.EnvironmentId,
                    actor,
                    NormalizeReason(reason)),
                cancellationToken);
            SetSuccess();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            SetRejected();
        }

        return RedirectToIndex(culture);
    }

    [HttpPost("{authProfileId:guid}/secrets")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSecret(
        Guid authProfileId,
        string label,
        string secretValue,
        string? culture,
        CancellationToken cancellationToken)
    {
        // Never carry a submitted secret value into validation/UI state.
        ModelState.Remove(nameof(secretValue));

        var profile = await FindProfileAsync(authProfileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        if (!profile.IsEnabled)
        {
            return Conflict("A disabled AuthProfile cannot accept a new secret.");
        }

        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        if (!ProfileBelongsToCatalog(profile, snapshot))
        {
            return NotFound();
        }

        var normalizedLabel = NormalizeSecretLabel(label);
        if (normalizedLabel is null || !IsAcceptableSecretValue(secretValue))
        {
            return BadRequest("Invalid secret request.");
        }
        if (profile.Secrets.Any(secret => string.Equals(secret.Name, normalizedLabel, StringComparison.Ordinal)))
        {
            // Replacement must use the atomic rotation boundary. Do not emulate it
            // with unconditional SetSecretReferenceAsync.
            return Conflict("An existing secret slot must be changed through atomic rotation.");
        }

        var clearBytes = Encoding.UTF8.GetBytes(secretValue);
        SecretRef? createdReference = null;
        try
        {
            var created = await secretVault.CreateActiveAsync(clearBytes, cancellationToken);
            createdReference = created.Reference;
            await authProfiles.SetSecretReferenceAsync(profile.Id, normalizedLabel, created.Reference, cancellationToken);
            createdReference = null;
            SetSuccess();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            if (createdReference.HasValue)
            {
                await RevokeBestEffortAsync(createdReference.Value);
            }
            SetRejected();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }

        return RedirectToIndex(culture);
    }

    [HttpPost("{authProfileId:guid}/secrets/{secretId:guid}/rotate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateSecret(
        Guid authProfileId,
        Guid secretId,
        string secretValue,
        bool confirmRotation,
        string? culture,
        CancellationToken cancellationToken)
    {
        // The current canonical foundation is explicitly merge-blocked until it
        // exposes atomic expected-current-reference/generation CAS. Accepting a
        // plaintext candidate here and doing an unconditional replacement would
        // violate the integrated P06 rotation contract, so fail closed.
        ModelState.Remove(nameof(secretValue));

        if (!confirmRotation || !IsAcceptableSecretValue(secretValue))
        {
            return BadRequest("Invalid secret rotation request.");
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

        var secret = profile.Secrets.SingleOrDefault(candidate =>
            CreateSecretSlotId(profile.Id, candidate.Name) == secretId);
        if (secret is null)
        {
            return NotFound();
        }

        return Conflict("Atomic rotation is unavailable until the canonical foundation CAS/generation boundary is integrated.");
    }

    [HttpPost("{authProfileId:guid}/state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetState(
        Guid authProfileId,
        bool isActive,
        string? culture,
        CancellationToken cancellationToken)
    {
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

        try
        {
            await authProfiles.SetEnabledAsync(profile.Id, isActive, cancellationToken);
            SetSuccess();
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            SetRejected();
        }

        return RedirectToIndex(culture);
    }

    [HttpPost("{authProfileId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMetadata(
        Guid authProfileId,
        string name,
        string authenticationType,
        CancellationToken cancellationToken)
    {
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

        if (string.IsNullOrWhiteSpace(name) || !TryParseAuthType(authenticationType, out _))
        {
            return BadRequest("Invalid AuthProfile metadata request.");
        }

        // Name/AuthType mutation is intentionally not implemented through a
        // second persistence path. The canonical IAuthProfileService must own it.
        return Conflict("AuthProfile metadata mutation requires a canonical foundation operation.");
    }

    [HttpPost("{authProfileId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid authProfileId, CancellationToken cancellationToken)
    {
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

        // Canonical AuthProfiles always own at least one exact binding. Deleting
        // an in-use profile without a foundation transaction would be unsafe.
        if (profile.Bindings.Count > 0)
        {
            return Conflict("An in-use AuthProfile cannot be deleted.");
        }

        return Conflict("AuthProfile deletion requires a canonical foundation operation.");
    }

    private async Task<AuthProfileAdminViewModel> BuildViewModelAsync(
        MetadataCatalogSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var currentServices = snapshot.Services
            .Where(service => service.IsCurrent)
            .ToList();
        var referencedProfileIds = currentServices
            .SelectMany(service => service.EnvironmentConfigs)
            .Where(config => config.AuthProfileId.HasValue)
            .Select(config => config.AuthProfileId!.Value)
            .Distinct()
            .ToList();

        var descriptors = new Dictionary<Guid, AuthProfileDescriptor>();
        foreach (var profileId in referencedProfileIds)
        {
            var profile = await FindProfileAsync(profileId, cancellationToken);
            if (profile is not null)
            {
                descriptors[profile.Id] = profile;
            }
        }

        var environmentById = snapshot.Environments.ToDictionary(environment => environment.Id);
        var entities = snapshot.Entities
            .Where(entity => entity.Active)
            .OrderBy(entity => entity.DisplayOrder)
            .ThenBy(entity => entity.Code, StringComparer.OrdinalIgnoreCase)
            .Select(entity => new AuthProfileAdminEntityViewModel(
                entity.Id,
                entity.Code,
                entity.NameAr,
                entity.NameEn,
                currentServices
                    .Where(service => service.EntityId == entity.Id && service.Active)
                    .OrderBy(service => service.Code, StringComparer.OrdinalIgnoreCase)
                    .Select(service => new AuthProfileAdminServiceViewModel(
                        service.Id,
                        service.Code,
                        service.NameAr,
                        service.NameEn,
                        service.EnvironmentConfigs
                            .Where(config => environmentById.ContainsKey(config.EnvironmentId))
                            .OrderBy(config => environmentById[config.EnvironmentId].DisplayOrder)
                            .Select(config =>
                            {
                                descriptors.TryGetValue(config.AuthProfileId ?? Guid.Empty, out var profile);
                                var binding = profile?.Bindings.SingleOrDefault(candidate =>
                                    candidate.ServiceId == service.Id && candidate.EnvironmentId == config.EnvironmentId);
                                return new AuthProfileEnvironmentViewModel(
                                    config.EnvironmentId,
                                    environmentById[config.EnvironmentId].Code,
                                    config.Active && environmentById[config.EnvironmentId].Active,
                                    config.AuthProfileId,
                                    profile?.Name,
                                    binding?.IsShared == true);
                            })
                            .ToList()))
                    .ToList()))
            .ToList();

        var profileModels = new List<AuthProfileSummaryViewModel>();
        foreach (var profile in descriptors.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            var secretModels = new List<SecretRefSummaryViewModel>();
            foreach (var secret in profile.Secrets.OrderBy(secret => secret.Name, StringComparer.Ordinal))
            {
                try
                {
                    var descriptor = await secretVault.GetDescriptorAsync(secret.Reference, cancellationToken);
                    secretModels.Add(new SecretRefSummaryViewModel(
                        CreateSecretSlotId(profile.Id, secret.Name),
                        secret.Name,
                        "••••••••",
                        descriptor.Generation,
                        descriptor.State == SecretLifecycleState.Active,
                        descriptor.CreatedAtUtc));
                }
                catch (SecretReferenceRejectedException)
                {
                    secretModels.Add(new SecretRefSummaryViewModel(
                        CreateSecretSlotId(profile.Id, secret.Name),
                        secret.Name,
                        "••••••••",
                        0,
                        false,
                        null));
                }
            }

            profileModels.Add(new AuthProfileSummaryViewModel(
                profile.Id,
                profile.Name,
                profile.AuthType.ToString(),
                profile.Bindings.Any(binding => binding.IsShared),
                profile.IsEnabled,
                profile.Bindings.Count,
                secretModels,
                secretModels.Where(secret => secret.UpdatedAtUtc.HasValue)
                    .Select(secret => secret.UpdatedAtUtc)
                    .Max()));
        }

        return new AuthProfileAdminViewModel
        {
            Entities = entities,
            Profiles = profileModels
        };
    }

    private static bool TryResolveExactTarget(
        MetadataCatalogSnapshot snapshot,
        BindingTarget target,
        out CatalogService service,
        out ServiceEnvironmentConfig config)
    {
        service = snapshot.Services.SingleOrDefault(candidate =>
            candidate.Id == target.ServiceId
            && candidate.EntityId == target.EntityId
            && candidate.IsCurrent)!;
        if (service is null)
        {
            config = null!;
            return false;
        }

        config = service.EnvironmentConfigs.SingleOrDefault(candidate => candidate.EnvironmentId == target.EnvironmentId)!;
        if (config is null)
        {
            return false;
        }

        return snapshot.Environments.Any(environment => environment.Id == target.EnvironmentId);
    }

    private static bool ProfileBelongsToCatalog(AuthProfileDescriptor profile, MetadataCatalogSnapshot snapshot)
    {
        foreach (var binding in profile.Bindings)
        {
            var service = snapshot.Services.SingleOrDefault(candidate => candidate.Id == binding.ServiceId && candidate.IsCurrent);
            if (service is null)
            {
                continue;
            }
            if (service.EnvironmentConfigs.Any(config =>
                    config.EnvironmentId == binding.EnvironmentId
                    && config.AuthProfileId == profile.Id))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<AuthProfileDescriptor?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        if (profileId == Guid.Empty)
        {
            return null;
        }
        try
        {
            return await authProfiles.GetAsync(profileId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static bool TryParseBindingTarget(string? raw, out BindingTarget target)
    {
        var parts = raw?.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 3 }
            || !Guid.TryParse(parts[0], out var entityId)
            || !Guid.TryParse(parts[1], out var serviceId)
            || !Guid.TryParse(parts[2], out var environmentId)
            || entityId == Guid.Empty
            || serviceId == Guid.Empty
            || environmentId == Guid.Empty)
        {
            target = default;
            return false;
        }

        target = new BindingTarget(entityId, serviceId, environmentId);
        return true;
    }

    private static bool TryParseAuthType(string? raw, out AuthProfileType authType)
    {
        if (Enum.TryParse(raw, ignoreCase: false, out authType)
            && Enum.IsDefined(authType)
            && authType != AuthProfileType.None)
        {
            return true;
        }
        authType = AuthProfileType.None;
        return false;
    }

    private static string NormalizeReason(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 500 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Sharing reason is invalid.", nameof(value));
        }
        return normalized;
    }

    private static string? NormalizeSecretLabel(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 80
            || normalized.Any(char.IsControl))
        {
            return null;
        }
        return normalized;
    }

    private static bool IsAcceptableSecretValue(string? value) =>
        value is not null && value.Length is >= MinimumSecretCharacters and <= MaximumSecretCharacters;

    internal static Guid CreateSecretSlotId(Guid profileId, string secretName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(secretName.Trim().ToLowerInvariant());
        var payload = new byte[16 + nameBytes.Length];
        profileId.TryWriteBytes(payload.AsSpan(0, 16));
        nameBytes.CopyTo(payload.AsSpan(16));
        var hash = SHA256.HashData(payload);
        return new Guid(hash.AsSpan(0, 16));
    }

    private async Task RevokeBestEffortAsync(SecretRef secretRef)
    {
        try
        {
            await secretVault.RevokeAsync(secretRef, CancellationToken.None);
        }
        catch (Exception)
        {
            // The primary operation has already failed. Never surface cleanup
            // exceptions because a provider exception may contain sensitive data.
        }
    }

    private string? GetActor()
    {
        var actor = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        actor = actor?.Trim();
        return string.IsNullOrWhiteSpace(actor) || actor.Length > 160 ? null : actor;
    }

    private void SetSuccess() => TempData["AuthProfileSuccess"] = "success";
    private void SetRejected() => TempData["AuthProfileError"] = "rejected";

    private IActionResult RedirectToIndex(string? culture) =>
        RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture) });

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";

    private readonly record struct BindingTarget(Guid EntityId, Guid ServiceId, Guid EnvironmentId);
}
