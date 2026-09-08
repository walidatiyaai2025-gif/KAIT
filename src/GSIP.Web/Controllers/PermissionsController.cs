using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.RolesManage)]
[Route("permissions")]
public sealed class PermissionsController(
    GsipDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Select(role => new { role.Id, Name = role.Name ?? string.Empty })
            .ToListAsync(cancellationToken);
        var seedOrder = GsipRoles.SeedRoles
            .Select((role, index) => new { role.Id, index })
            .ToDictionary(item => item.Id, item => item.index);
        var roleModels = roles
            .OrderBy(role => seedOrder.TryGetValue(role.Id, out var order) ? order : int.MaxValue)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .Select(role => new PermissionRoleViewModel(role.Id, role.Name, seedOrder.ContainsKey(role.Id)))
            .ToList();

        var permissionRows = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(row => row.IsAllowed)
            .Select(row => new { row.RoleId, row.PermissionKey })
            .ToListAsync(cancellationToken);
        var rolePermissionGrants = roleModels.ToDictionary(
            role => role.Id,
            role => (IReadOnlySet<string>)permissionRows
                .Where(row => row.RoleId == role.Id)
                .Select(row => row.PermissionKey)
                .ToHashSet(StringComparer.Ordinal));

        var permissions = GsipPermissions.All
            .Select(permission => new PermissionDefinitionViewModel(permission, GetCategory(permission)))
            .ToList();

        var serviceRecords = await dbContext.ServiceEnvironmentPlaceholders
            .AsNoTracking()
            .Where(row => row.IsActive)
            .Select(row => new { row.EntityCode, row.ServiceCode })
            .ToListAsync(cancellationToken);
        var services = serviceRecords
            .Where(row => !string.IsNullOrWhiteSpace(row.ServiceCode))
            .GroupBy(row => new { row.EntityCode, row.ServiceCode })
            .Select(group => new ServicePermissionViewModel(group.Key.EntityCode, group.Key.ServiceCode))
            .OrderBy(service => service.EntityCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(service => service.ServiceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var serviceGrantRows = await dbContext.RoleServicePermissions
            .AsNoTracking()
            .Where(row => row.PermissionKey == GsipPermissions.ServicesExecute && row.IsAllowed)
            .Select(row => new { row.RoleId, row.ServiceCode })
            .ToListAsync(cancellationToken);
        var serviceExecuteGrants = services.ToDictionary(
            service => service.ServiceCode,
            service => (IReadOnlySet<Guid>)serviceGrantRows
                .Where(row => string.Equals(row.ServiceCode, NormalizeServiceCode(service.ServiceCode), StringComparison.Ordinal))
                .Select(row => row.RoleId)
                .ToHashSet(),
            StringComparer.Ordinal);

        var users = await dbContext.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                Username = user.UserName ?? string.Empty,
                user.IsEnabled,
                user.IsPrivileged
            })
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
        var userRoleRows = await dbContext.UserRoles.AsNoTracking().ToListAsync(cancellationToken);
        var roleNames = roleModels.ToDictionary(role => role.Id, role => role.Name);
        var userModels = users.Select(user =>
        {
            var assignedRoleIds = userRoleRows
                .Where(row => row.UserId == user.Id && roleNames.ContainsKey(row.RoleId))
                .Select(row => row.RoleId)
                .ToHashSet();
            var assignedRoleNames = assignedRoleIds
                .Select(roleId => roleNames[roleId])
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new UserRoleSummaryViewModel(
                user.Id,
                user.DisplayName,
                user.Username,
                user.IsEnabled,
                user.IsPrivileged,
                assignedRoleIds,
                assignedRoleNames);
        }).ToList();

        var selectedUser = userId.HasValue
            ? userModels.FirstOrDefault(user => user.Id == userId.Value)
            : userModels.FirstOrDefault();

        return View(new PermissionsDashboardViewModel
        {
            Roles = roleModels,
            Permissions = permissions,
            Services = services,
            Users = userModels,
            RolePermissionGrants = rolePermissionGrants,
            ServiceExecuteGrants = serviceExecuteGrants,
            SelectedUser = selectedUser,
            PendingApprovals = 0,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        });
    }

    [HttpPost("roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(
        string roleName,
        Guid? userId,
        string? culture,
        CancellationToken cancellationToken)
    {
        var normalizedName = ValidateRoleName(roleName);
        if (normalizedName is null)
        {
            return BadRequest("Role name must contain 1 to 100 printable characters.");
        }

        if (await roleManager.FindByNameAsync(normalizedName) is not null)
        {
            return Conflict("A role with this name already exists.");
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = normalizedName
        });
        if (!result.Succeeded)
        {
            return BadRequest("The role could not be created.");
        }

        return RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture), userId });
    }

    [HttpPost("roles/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameRole(
        Guid roleId,
        string roleName,
        Guid? userId,
        string? culture,
        CancellationToken cancellationToken)
    {
        var validatedName = ValidateRoleName(roleName);
        if (validatedName is null)
        {
            return BadRequest("Role name must contain 1 to 100 printable characters.");
        }

        var role = await dbContext.Roles.SingleOrDefaultAsync(candidate => candidate.Id == roleId, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        var normalizedLookupName = roleManager.NormalizeKey(validatedName);
        var duplicate = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id != roleId && candidate.NormalizedName == normalizedLookupName, cancellationToken);
        if (duplicate)
        {
            return Conflict("A role with this name already exists.");
        }

        role.Name = validatedName;
        role.NormalizedName = normalizedLookupName;
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture), userId });
    }

    [HttpPost("user-role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserRole(
        Guid userId,
        Guid roleId,
        bool isAssigned,
        string? culture,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (!await dbContext.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            return NotFound();
        }

        var existing = await dbContext.UserRoles
            .SingleOrDefaultAsync(
                row => row.UserId == userId && row.RoleId == roleId,
                cancellationToken);

        if (isAssigned)
        {
            if (existing is null)
            {
                dbContext.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = userId,
                    RoleId = roleId
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (existing is not null)
        {
            if (roleId == Guid.Parse(GsipRoles.SystemAdministratorId) && user.IsEnabled)
            {
                var remainingEnabledAdministrators = await (
                    from userRole in dbContext.UserRoles.AsNoTracking()
                    join candidate in dbContext.Users.AsNoTracking() on userRole.UserId equals candidate.Id
                    where userRole.RoleId == roleId
                        && candidate.IsEnabled
                        && candidate.Id != userId
                    select candidate.Id)
                    .Distinct()
                    .CountAsync(cancellationToken);
                if (remainingEnabledAdministrators == 0)
                {
                    return BadRequest("The last enabled System Administrator role assignment cannot be removed.");
                }
            }

            dbContext.UserRoles.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture), userId });
    }

    [HttpPost("role-permission")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRolePermission(
        Guid roleId,
        string permissionKey,
        bool isAllowed,
        Guid? userId,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!GsipPermissions.IsKnown(permissionKey))
        {
            return BadRequest("Unknown permission.");
        }
        if (!await dbContext.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            return NotFound();
        }

        var row = await dbContext.RolePermissions
            .SingleOrDefaultAsync(
                permission => permission.RoleId == roleId && permission.PermissionKey == permissionKey,
                cancellationToken);
        if (row is null)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionKey = permissionKey,
                IsAllowed = isAllowed,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.IsAllowed = isAllowed;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture), userId });
    }

    [HttpPost("service-permission")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetServicePermission(
        Guid roleId,
        string serviceCode,
        string permissionKey,
        bool isAllowed,
        Guid? userId,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (!GsipPermissions.ServiceScoped.Contains(permissionKey)
            || string.IsNullOrWhiteSpace(serviceCode)
            || serviceCode.Length > 120)
        {
            return BadRequest("Invalid service permission request.");
        }
        if (!await dbContext.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            return NotFound();
        }
        if (!await dbContext.ServiceEnvironmentPlaceholders
            .AsNoTracking()
            .AnyAsync(service => service.IsActive && service.ServiceCode == serviceCode, cancellationToken))
        {
            return BadRequest("Unknown service.");
        }

        var normalizedServiceCode = NormalizeServiceCode(serviceCode);
        var row = await dbContext.RoleServicePermissions
            .SingleOrDefaultAsync(
                permission => permission.RoleId == roleId
                    && permission.ServiceCode == normalizedServiceCode
                    && permission.PermissionKey == permissionKey,
                cancellationToken);
        if (row is null)
        {
            dbContext.RoleServicePermissions.Add(new RoleServicePermission
            {
                RoleId = roleId,
                ServiceCode = normalizedServiceCode,
                PermissionKey = permissionKey,
                IsAllowed = isAllowed,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.IsAllowed = isAllowed;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture), userId });
    }

    private static string GetCategory(string permission) => permission switch
    {
        var value when value.StartsWith("Entities.", StringComparison.Ordinal) => "Entities",
        var value when value.StartsWith("Services.", StringComparison.Ordinal) || value.StartsWith("ServiceSecrets.", StringComparison.Ordinal) => "Services",
        var value when value.StartsWith("Users.", StringComparison.Ordinal) || value.StartsWith("Roles.", StringComparison.Ordinal) => "Identity",
        var value when value.StartsWith("Requests.", StringComparison.Ordinal) => "Requests",
        var value when value.StartsWith("Audit.", StringComparison.Ordinal) => "Audit",
        _ => "Operations"
    };

    private static string? ValidateRoleName(string? roleName)
    {
        var value = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 100
            || value.Any(char.IsControl))
        {
            return null;
        }
        return value;
    }

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";

    private static string NormalizeServiceCode(string serviceCode) => serviceCode.Trim().ToUpperInvariant();
}
