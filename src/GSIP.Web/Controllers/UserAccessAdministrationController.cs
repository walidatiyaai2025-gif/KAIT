using System.ComponentModel.DataAnnotations;
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.RolesManage)]
[Route("permissions/users")]
public sealed class UserAccessAdministrationController(
    GsipDbContext dbContext,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost("create")]
    [Authorize(Policy = GsipPermissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        string displayName,
        string username,
        string email,
        string? departmentCode,
        string initialPassword,
        bool isPrivileged,
        Guid? roleId,
        bool allEntities,
        string[]? entityCodes,
        bool allServices,
        string[]? serviceKeys,
        string? culture,
        CancellationToken cancellationToken)
    {
        var normalizedDisplayName = displayName?.Trim() ?? string.Empty;
        var normalizedUsername = username?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim() ?? string.Empty;
        var normalizedDepartment = string.IsNullOrWhiteSpace(departmentCode) ? null : departmentCode.Trim();

        if (normalizedDisplayName.Length is < 1 or > 160
            || normalizedDisplayName.Any(char.IsControl)
            || normalizedUsername.Length is < 1 or > 120
            || normalizedUsername.Any(char.IsControl)
            || normalizedEmail.Length is < 3 or > 254
            || !new EmailAddressAttribute().IsValid(normalizedEmail)
            || normalizedDepartment?.Length > 100
            || string.IsNullOrWhiteSpace(initialPassword)
            || initialPassword.Length > 128)
        {
            return RedirectWithError("UserCreateInvalid", culture);
        }

        string? roleName = null;
        if (roleId is Guid exactRoleId)
        {
            roleName = await dbContext.Roles
                .AsNoTracking()
                .Where(role => role.Id == exactRoleId)
                .Select(role => role.Name)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return RedirectWithError("UserCreateInvalidRole", culture);
            }
        }

        var validatedScope = await ValidateScopeAsync(
            allEntities,
            entityCodes,
            allServices,
            serviceKeys,
            cancellationToken);
        if (validatedScope is null)
        {
            return RedirectWithError("UserAccessInvalid", culture);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = normalizedDisplayName,
            UserName = normalizedUsername,
            Email = normalizedEmail,
            EmailConfirmed = true,
            DepartmentCode = normalizedDepartment,
            IsEnabled = true,
            IsPrivileged = isPrivileged,
            MustChangePassword = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, initialPassword);
        if (!createResult.Succeeded)
        {
            TempData["PermissionsAdminError"] = "UserCreateFailed";
            TempData["PermissionsAdminErrorDetail"] = SafeIdentityError(createResult);
            return RedirectToPermissions(null, culture);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                var roleResult = await userManager.AddToRoleAsync(user, roleName);
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException("Initial role assignment failed.");
                }
            }

            await EntityAccessScopeStore.SetForUserAsync(
                dbContext,
                user.Id,
                allEntities,
                validatedScope.EntityCodes,
                allServices,
                validatedScope.Services,
                cancellationToken);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            TempData["PermissionsAdminError"] = "UserCreateFailed";
            return RedirectToPermissions(null, culture);
        }

        TempData["PermissionsAdminSuccess"] = "UserCreated";
        return RedirectToPermissions(user.Id, culture);
    }

    [HttpPost("{userId:guid}/access-scope")]
    [Authorize(Policy = GsipPermissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAccessScope(
        Guid userId,
        bool allEntities,
        string[]? entityCodes,
        bool allServices,
        string[]? serviceKeys,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound();
        }

        var validatedScope = await ValidateScopeAsync(
            allEntities,
            entityCodes,
            allServices,
            serviceKeys,
            cancellationToken);
        if (validatedScope is null)
        {
            return RedirectWithError("UserAccessInvalid", culture, userId);
        }

        await EntityAccessScopeStore.SetForUserAsync(
            dbContext,
            userId,
            allEntities,
            validatedScope.EntityCodes,
            allServices,
            validatedScope.Services,
            cancellationToken);

        TempData["PermissionsAdminSuccess"] = "UserAccessSaved";
        return RedirectToPermissions(userId, culture);
    }

    private async Task<ValidatedScope?> ValidateScopeAsync(
        bool allEntities,
        IEnumerable<string>? entityCodes,
        bool allServices,
        IEnumerable<string>? serviceKeys,
        CancellationToken cancellationToken)
    {
        var activeEntities = await dbContext.CatalogEntities
            .AsNoTracking()
            .Where(entity => entity.Active)
            .Select(entity => entity.Code)
            .ToListAsync(cancellationToken);
        var knownEntities = activeEntities
            .Select(NormalizeCode)
            .ToHashSet(StringComparer.Ordinal);

        var requestedEntities = allEntities
            ? []
            : (entityCodes ?? [])
                .Select(NormalizeCode)
                .Where(code => code.Length is > 0 and <= 40)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
        if (!requestedEntities.All(knownEntities.Contains))
        {
            return null;
        }

        if (allServices)
        {
            return new ValidatedScope(requestedEntities, []);
        }

        var activeServices = await (
            from service in dbContext.CatalogServices.AsNoTracking()
            join entity in dbContext.CatalogEntities.AsNoTracking() on service.EntityId equals entity.Id
            where service.Active && service.IsCurrent && entity.Active
            select new { EntityCode = entity.Code, ServiceCode = service.Code })
            .ToListAsync(cancellationToken);

        var knownServices = activeServices
            .Select(item => UserAccessScope.ServiceKey(item.EntityCode, item.ServiceCode))
            .ToDictionary(
                key => key,
                key =>
                {
                    var separator = key.IndexOf("::", StringComparison.Ordinal);
                    return (key[..separator], key[(separator + 2)..]);
                },
                StringComparer.Ordinal);

        var requestedServiceKeys = (serviceKeys ?? [])
            .Select(NormalizeServiceKey)
            .Where(key => key.Length is > 0 and <= 96)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!requestedServiceKeys.All(knownServices.ContainsKey))
        {
            return null;
        }

        if (!allEntities)
        {
            var selectedEntitySet = requestedEntities.ToHashSet(StringComparer.Ordinal);
            if (requestedServiceKeys.Any(key => !selectedEntitySet.Contains(knownServices[key].Item1)))
            {
                return null;
            }
        }

        var services = requestedServiceKeys
            .Select(key => knownServices[key])
            .Select(item => (EntityCode: item.Item1, ServiceCode: item.Item2))
            .ToArray();
        return new ValidatedScope(requestedEntities, services);
    }

    private IActionResult RedirectWithError(string resourceKey, string? culture, Guid? userId = null)
    {
        TempData["PermissionsAdminError"] = resourceKey;
        return RedirectToPermissions(userId, culture);
    }

    private RedirectToActionResult RedirectToPermissions(Guid? userId, string? culture) =>
        RedirectToAction(
            "Index",
            "Permissions",
            new
            {
                userId,
                culture = NormalizeCulture(culture)
            });

    private static string SafeIdentityError(IdentityResult result) =>
        string.Join(" ", result.Errors
            .Take(3)
            .Select(error => error.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description)));

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";

    private static string NormalizeCode(string? code) =>
        code?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string NormalizeServiceKey(string? key)
    {
        var value = key?.Trim().ToUpperInvariant() ?? string.Empty;
        if (value.Length is 0 or > 96 || value.Contains('|'))
        {
            return string.Empty;
        }

        var parts = value.Split("::", StringSplitOptions.None);
        return parts.Length == 2
            && parts[0].Length is > 0 and <= 40
            && parts[1].Length is > 0 and <= 54
                ? $"{parts[0]}::{parts[1]}"
                : string.Empty;
    }

    private sealed record ValidatedScope(
        IReadOnlyList<string> EntityCodes,
        IReadOnlyList<(string EntityCode, string ServiceCode)> Services);
}
