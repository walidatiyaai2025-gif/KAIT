using System.Reflection;
using GSIP.Application.Abstractions;
using GSIP.Application.Identity;
using GSIP.Application.Metadata;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GSIP.Web.Controllers;

public sealed class ShellController(
    ISystemClock clock,
    IAccountAuthenticationService authentication,
    IMetadataCatalogService metadataCatalog) : Controller
{
    [Authorize]
    [HttpGet("/")]
    public async Task<IActionResult> Index(
        [FromQuery] string? entityCode,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var restrictionRedirect = await RedirectForRestrictionAsync(cancellationToken);
        if (restrictionRedirect is not null)
        {
            return restrictionRedirect;
        }

        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        return View(CreateModel(snapshot, entityCode, search));
    }

    [AllowAnonymous]
    [HttpGet("/login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var restrictionRedirect = await RedirectForRestrictionAsync(cancellationToken);
            return restrictionRedirect ?? Redirect("/");
        }

        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            return LoginFailure(model, "AuthRequiredFields");
        }

        var result = await authentication.PasswordSignInAsync(
            model.Username,
            model.Password,
            model.RememberMe,
            cancellationToken);

        return result.Status switch
        {
            AccountSignInStatus.Succeeded => Redirect("/"),
            AccountSignInStatus.MfaEnrollmentRequired => Redirect("/mfa/enroll"),
            AccountSignInStatus.MfaVerificationRequired => Redirect("/mfa/verify"),
            AccountSignInStatus.PasswordChangeRequired => Redirect("/password/change"),
            AccountSignInStatus.LockedOut => LoginFailure(model, "AuthLocked"),
            AccountSignInStatus.Disabled => LoginFailure(model, "AuthDisabled"),
            _ => LoginFailure(model, "AuthInvalid")
        };
    }

    [Authorize]
    [HttpGet("/mfa/enroll")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MfaEnroll(CancellationToken cancellationToken)
    {
        var restriction = await authentication.GetSessionRestrictionAsync(User, cancellationToken);
        if (restriction != SessionRestriction.MfaEnrollmentRequired)
        {
            return await RedirectFromUnexpectedRestrictionAsync(restriction, cancellationToken);
        }

        var details = await authentication.GetMfaEnrollmentAsync(User, cancellationToken);
        if (details is null)
        {
            await authentication.SignOutAsync(User, cancellationToken);
            return Redirect("/login");
        }

        return View(new MfaEnrollmentViewModel { SharedKey = details.SharedKey });
    }

    [Authorize]
    [HttpPost("/mfa/enroll")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MfaEnroll(MfaEnrollmentViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            return await MfaEnrollmentFailureAsync(model, "MfaInvalid", cancellationToken);
        }

        var result = await authentication.ConfirmMfaEnrollmentAsync(User, model.Code, cancellationToken);
        if (!result.Succeeded)
        {
            return await MfaEnrollmentFailureAsync(model, "MfaInvalid", cancellationToken);
        }

        return Redirect("/");
    }

    [Authorize]
    [HttpGet("/mfa/verify")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MfaVerify(CancellationToken cancellationToken)
    {
        var restriction = await authentication.GetSessionRestrictionAsync(User, cancellationToken);
        if (restriction != SessionRestriction.MfaVerificationRequired)
        {
            return await RedirectFromUnexpectedRestrictionAsync(restriction, cancellationToken);
        }

        return View(new MfaVerificationViewModel());
    }

    [Authorize]
    [HttpPost("/mfa/verify")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MfaVerify(MfaVerificationViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            model.ErrorCode = "MfaInvalid";
            return View(model);
        }

        var result = await authentication.VerifyMfaAsync(User, model.Code, cancellationToken);
        if (!result.Succeeded)
        {
            model.Code = string.Empty;
            ModelState.Remove(nameof(MfaVerificationViewModel.Code));
            model.ErrorCode = "MfaInvalid";
            return View(model);
        }

        return Redirect("/");
    }

    [Authorize]
    [HttpGet("/password/change")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PasswordChange(CancellationToken cancellationToken)
    {
        var restriction = await authentication.GetSessionRestrictionAsync(User, cancellationToken);
        if (restriction != SessionRestriction.PasswordChangeRequired)
        {
            return await RedirectFromUnexpectedRestrictionAsync(restriction, cancellationToken);
        }

        return View(new PasswordChangeViewModel());
    }

    [Authorize]
    [HttpPost("/password/change")]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PasswordChange(PasswordChangeViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.CurrentPassword)
            || string.IsNullOrWhiteSpace(model.NewPassword)
            || string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            return PasswordChangeFailure(model, "PasswordRequiredFields");
        }

        if (!string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
        {
            return PasswordChangeFailure(model, "PasswordMismatch");
        }

        var result = await authentication.ChangePasswordAsync(
            User,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);
        if (!result.Succeeded)
        {
            return PasswordChangeFailure(model, "PasswordChangeRejected");
        }

        return Redirect("/");
    }

    [Authorize]
    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authentication.SignOutAsync(User, cancellationToken);
        return Redirect("/login");
    }

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => StatusCode(403);

    private IActionResult LoginFailure(LoginViewModel model, string errorCode)
    {
        model.Password = string.Empty;
        ModelState.Remove(nameof(LoginViewModel.Password));
        model.ErrorCode = errorCode;
        return View("Login", model);
    }

    private async Task<IActionResult> MfaEnrollmentFailureAsync(
        MfaEnrollmentViewModel model,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var details = await authentication.GetMfaEnrollmentAsync(User, cancellationToken);
        if (details is null)
        {
            await authentication.SignOutAsync(User, cancellationToken);
            return Redirect("/login");
        }

        model.Code = string.Empty;
        ModelState.Remove(nameof(MfaEnrollmentViewModel.Code));
        model.SharedKey = details.SharedKey;
        model.ErrorCode = errorCode;
        return View("MfaEnroll", model);
    }

    private IActionResult PasswordChangeFailure(PasswordChangeViewModel model, string errorCode)
    {
        model.CurrentPassword = string.Empty;
        model.NewPassword = string.Empty;
        model.ConfirmPassword = string.Empty;
        ModelState.Clear();
        model.ErrorCode = errorCode;
        return View("PasswordChange", model);
    }

    private async Task<IActionResult?> RedirectForRestrictionAsync(CancellationToken cancellationToken)
    {
        var restriction = await authentication.GetSessionRestrictionAsync(User, cancellationToken);
        if (restriction == SessionRestriction.None)
        {
            return null;
        }

        return await RedirectFromUnexpectedRestrictionAsync(restriction, cancellationToken);
    }

    private async Task<IActionResult> RedirectFromUnexpectedRestrictionAsync(
        SessionRestriction restriction,
        CancellationToken cancellationToken)
    {
        switch (restriction)
        {
            case SessionRestriction.MfaEnrollmentRequired:
                return Redirect("/mfa/enroll");
            case SessionRestriction.MfaVerificationRequired:
                return Redirect("/mfa/verify");
            case SessionRestriction.PasswordChangeRequired:
                return Redirect("/password/change");
            case SessionRestriction.None:
                return Redirect("/");
            default:
                await authentication.SignOutAsync(User, cancellationToken);
                return Redirect("/login");
        }
    }

    private ShellViewModel CreateModel(
        MetadataCatalogSnapshot snapshot,
        string? entityCode,
        string? search)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        var normalizedEntityCode = entityCode?.Trim() ?? string.Empty;
        var normalizedSearch = search?.Trim() ?? string.Empty;

        var activeEntities = snapshot.Entities
            .Where(entity => entity.Active)
            .OrderBy(entity => entity.DisplayOrder)
            .ThenBy(entity => entity.Code, StringComparer.Ordinal)
            .ToArray();
        var activeEntityById = activeEntities.ToDictionary(entity => entity.Id);

        var activeServices = snapshot.Services
            .Where(service => service.Active && service.IsCurrent && activeEntityById.ContainsKey(service.EntityId))
            .OrderBy(service => service.Code, StringComparer.Ordinal)
            .ToArray();

        var visibleServices = activeServices.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedEntityCode))
        {
            visibleServices = visibleServices.Where(service =>
                string.Equals(activeEntityById[service.EntityId].Code, normalizedEntityCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            visibleServices = visibleServices.Where(service =>
            {
                var entity = activeEntityById[service.EntityId];
                return service.Code.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || service.NameAr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || service.NameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entity.Code.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entity.NameAr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entity.NameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase);
            });
        }

        var entityOptions = activeEntities
            .Select(entity => new ShellEntityOptionViewModel(entity.Id, entity.Code, entity.NameAr, entity.NameEn))
            .ToArray();
        var serviceCards = visibleServices
            .Take(50)
            .Select(service =>
            {
                var entity = activeEntityById[service.EntityId];
                return new ShellServiceCardViewModel(
                    service.Id,
                    service.EntityId,
                    entity.Code,
                    entity.NameAr,
                    entity.NameEn,
                    service.Code,
                    service.NameAr,
                    service.NameEn,
                    service.DescriptionAr,
                    service.DescriptionEn,
                    service.Version,
                    service.EnvironmentConfigs.Count(configuration => configuration.Active));
            })
            .ToArray();

        return new ShellViewModel(
            version,
            "P13",
            clock.UtcNow,
            activeEntities.Length,
            activeServices.Length,
            snapshot.Environments.Count(environment => environment.Active),
            normalizedEntityCode,
            normalizedSearch,
            entityOptions,
            serviceCards);
    }
}
