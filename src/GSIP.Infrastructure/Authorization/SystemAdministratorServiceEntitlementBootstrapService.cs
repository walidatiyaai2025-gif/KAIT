using GSIP.Application.Authorization;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Authorization;

/// <summary>
/// Ensures the canonical System Administrator role has explicit service-scoped
/// permissions for every current service. P04 intentionally defaults service
/// permissions to deny when the role/service matrix row is absent, so the
/// bootstrap administrator otherwise receives the global permission catalog
/// but cannot see or execute any service.
///
/// Existing explicit rows are never overwritten. In particular, an explicit
/// IsAllowed=false decision remains authoritative.
/// </summary>
internal sealed class SystemAdministratorServiceEntitlementBootstrapService(
    IServiceScopeFactory scopeFactory) : IHostedService
{
    private static readonly string[] ServiceScopedAdministratorPermissions =
    [
        GsipPermissions.ServicesView,
        GsipPermissions.ServicesExecute,
        GsipPermissions.ServicesManage
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        if (!(await setup.GetStatusAsync(cancellationToken)).IsCompleted)
            return;

        var db = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
        var roleId = Guid.Parse(GsipRoles.SystemAdministratorId);
        if (!await db.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
            return;

        var globallyAllowedPermissions = await db.RolePermissions
            .AsNoTracking()
            .Where(row => row.RoleId == roleId
                && row.IsAllowed
                && ServiceScopedAdministratorPermissions.Contains(row.PermissionKey))
            .Select(row => row.PermissionKey)
            .ToListAsync(cancellationToken);
        if (globallyAllowedPermissions.Count == 0)
            return;

        var serviceCodes = await db.CatalogServices
            .AsNoTracking()
            .Where(service => service.IsCurrent)
            .Select(service => service.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (serviceCodes.Count == 0)
            return;

        var normalizedServiceCodes = serviceCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingRows = await db.RoleServicePermissions
            .AsNoTracking()
            .Where(row => row.RoleId == roleId
                && normalizedServiceCodes.Contains(row.ServiceCode)
                && globallyAllowedPermissions.Contains(row.PermissionKey))
            .Select(row => new { row.ServiceCode, row.PermissionKey })
            .ToListAsync(cancellationToken);

        var existing = existingRows
            .Select(row => $"{row.ServiceCode}\u001f{row.PermissionKey}")
            .ToHashSet(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;

        foreach (var serviceCode in normalizedServiceCodes)
        {
            foreach (var permission in globallyAllowedPermissions)
            {
                if (existing.Contains($"{serviceCode}\u001f{permission}"))
                    continue;

                db.RoleServicePermissions.Add(new RoleServicePermission
                {
                    RoleId = roleId,
                    ServiceCode = serviceCode,
                    PermissionKey = permission,
                    IsAllowed = true,
                    UpdatedAtUtc = now
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
