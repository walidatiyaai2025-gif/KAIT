using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Application.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Authorization;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class RoleServicePermission
{
    public Guid RoleId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class GsipPermissionEvaluator(GsipDbContext dbContext) : IGsipPermissionEvaluator
{
    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (GsipSessionRestrictions.IsRestricted(principal)
            || !GsipPermissions.IsKnown(permission)
            || !TryGetUserId(principal, out var userId))
        {
            return false;
        }

        var roleIds = await GetRoleIdsAsync(userId, cancellationToken);
        if (roleIds.Count == 0)
        {
            return false;
        }

        return await dbContext.RolePermissions
            .AsNoTracking()
            .AnyAsync(
                row => roleIds.Contains(row.RoleId)
                    && row.PermissionKey == permission
                    && row.IsAllowed,
                cancellationToken);
    }

    public async Task<bool> HasServicePermissionAsync(
        ClaimsPrincipal principal,
        string serviceCode,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (GsipSessionRestrictions.IsRestricted(principal)
            || !GsipPermissions.ServiceScoped.Contains(permission)
            || string.IsNullOrWhiteSpace(serviceCode)
            || !TryGetUserId(principal, out var userId))
        {
            return false;
        }

        var roleIds = await GetRoleIdsAsync(userId, cancellationToken);
        if (roleIds.Count == 0)
        {
            return false;
        }

        var entitledRoleIds = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(row => roleIds.Contains(row.RoleId)
                && row.PermissionKey == permission
                && row.IsAllowed)
            .Select(row => row.RoleId)
            .ToListAsync(cancellationToken);
        if (entitledRoleIds.Count == 0)
        {
            return false;
        }

        var normalizedServiceCode = serviceCode.Trim().ToUpperInvariant();
        var hasServiceGrant = await dbContext.RoleServicePermissions
            .AsNoTracking()
            .AnyAsync(
                row => entitledRoleIds.Contains(row.RoleId)
                    && row.ServiceCode == normalizedServiceCode
                    && row.PermissionKey == permission
                    && row.IsAllowed,
                cancellationToken);
        if (!hasServiceGrant)
        {
            return false;
        }

        var scope = await EntityAccessScopeStore.GetForUserAsync(dbContext, userId, cancellationToken);
        if (scope.AllEntities && scope.AllServices)
        {
            // No explicit user-level restriction exists. Preserve the pre-scope
            // RBAC/service-grant behavior without requiring catalog metadata.
            return true;
        }

        // An explicit user scope needs an exact active current service/entity
        // binding so forged, stale or ambiguous service codes fail closed.
        var serviceScopes = await (
            from service in dbContext.CatalogServices.AsNoTracking()
            join entity in dbContext.CatalogEntities.AsNoTracking() on service.EntityId equals entity.Id
            where service.IsCurrent
                && service.Active
                && entity.Active
                && service.Code == normalizedServiceCode
            select new { EntityCode = entity.Code, ServiceCode = service.Code })
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);
        if (serviceScopes.Count != 1)
        {
            return false;
        }

        return scope.AllowsService(serviceScopes[0].EntityCode, serviceScopes[0].ServiceCode);
    }

    private async Task<List<Guid>> GetRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => row.RoleId)
            .ToListAsync(cancellationToken);

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public sealed class GsipPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = GsipPermissions.IsKnown(permission)
        ? permission
        : throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown GSIP permission.");
}

public sealed class GsipPermissionAuthorizationHandler(IGsipPermissionEvaluator evaluator)
    : AuthorizationHandler<GsipPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GsipPermissionRequirement requirement)
    {
        if (await evaluator.HasPermissionAsync(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

public static class GsipAuthorizationPolicyRegistration
{
    public static void AddPolicies(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var permission in GsipPermissions.All)
        {
            options.AddPolicy(permission, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new GsipPermissionRequirement(permission));
            });
        }
    }
}
