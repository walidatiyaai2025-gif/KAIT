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

        var validatedEntityCodes = await ValidateEntityCodesAsync(allEntities, entityCodes, cancellationToken);
        if (validatedEntityCodes is null)
        {
            return RedirectWithError("EntityAccessInvalid", culture);
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
                validatedEntityCodes,
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

    [HttpPost("{userId:guid}/entity-scope")]
    [Authorize(Policy = GsipPermissions.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEntityScope(
        Guid userId,
        bool allEntities,
        string[]? entityCodes,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound();
        }

        var validatedEntityCodes = await ValidateEntityCodesAsync(allEntities, entityCodes, cancellationToken);
        if (validatedEntityCodes is null)
        {
            return RedirectWithError("EntityAccessInvalid", culture, userId);
        }

        await EntityAccessScopeStore.SetForUserAsync(
            dbContext,
            userId,
            allEntities,
            validatedEntityCodes,
            cancellationToken);

        TempData["PermissionsAdminSuccess"] = "EntityAccessSaved";
        return RedirectToPermissions(userId, culture);
    }

    private async Task<IReadOnlyList<string>?> ValidateEntityCodesAsync(
        bool allEntities,
        IEnumerable<string>? entityCodes,
        CancellationToken cancellationToken)
    {
        if (allEntities)
        {
            return [];
        }

        var requested = (entityCodes ?? [])
            .Select(code => code?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(code => code.Length is > 0 and <= 40)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        if (requested.Length == 0)
        {
            return requested;
        }

        var known = await dbContext.CatalogEntities
            .AsNoTracking()
            .Where(entity => entity.Active)
            .Select(entity => entity.Code)
            .ToListAsync(cancellationToken);
        var knownSet = known
            .Select(code => code.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return requested.All(knownSet.Contains) ? requested : null;
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
}
