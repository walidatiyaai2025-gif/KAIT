using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web.Controllers;

[Authorize(Policy = GsipPermissions.RolesManage)]
[Route("permissions")]
public sealed class PermissionsController(GsipDbContext dbContext) : Controller
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
        var userModels = users.Select(user => new UserRoleSummaryViewModel(
                user.Id,
                user.DisplayName,
                user.Username,
                user.IsEnabled,
                user.IsPrivileged,
                userRoleRows
                    .Where(row => row.UserId == user.Id && roleNames.ContainsKey(row.RoleId))
                    .Select(row => roleNames[row.RoleId])
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

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
            GeneratedAtUtc = DateTimeOffset.UtcNow
        });
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

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";

    private static string NormalizeServiceCode(string serviceCode) => serviceCode.Trim().ToUpperInvariant();
}
