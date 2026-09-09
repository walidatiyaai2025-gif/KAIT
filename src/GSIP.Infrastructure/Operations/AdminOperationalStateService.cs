using System.Security.Claims;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Application.Operations;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Operations;

/// <summary>
/// P12 operational state mutations are intentionally separate from metadata definition
/// versioning. Toggling one ServiceEnvironmentConfig must preserve the canonical ServiceId,
/// AuthProfile/SecretRef scope and sibling UAT/Production bindings.
/// </summary>
public sealed class AdminOperationalStateService(
    GsipDbContext db,
    IGsipPermissionEvaluator permissions,
    IAuditTrailWriter auditTrail,
    ISystemClock clock) : IAdminOperationalStateService
{
    public async Task<AdminEnvironmentStateResult> SetEnvironmentStateAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (serviceId == Guid.Empty || environmentId == Guid.Empty)
            throw new AdminOperationsTargetRejectedException();

        var service = await db.CatalogServices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsCurrent, cancellationToken);
        var environment = await db.CatalogEnvironments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == environmentId, cancellationToken);
        if (service is null || environment is null)
            throw new AdminOperationsTargetRejectedException();

        if (!await permissions.HasServicePermissionAsync(
                principal,
                service.Code,
                GsipPermissions.ServicesManage,
                cancellationToken))
            throw new AdminOperationsAccessDeniedException();

        if (isActive && (!service.Active || !environment.Active))
            throw new AdminOperationsStateConflictException();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var config = await db.ServiceEnvironmentConfigs
                .SingleOrDefaultAsync(
                    item => item.ServiceId == serviceId && item.EnvironmentId == environmentId,
                    cancellationToken);
            if (config is null)
                throw new AdminOperationsTargetRejectedException();

            var previousState = config.Active;
            config.Active = isActive;
            await db.SaveChangesAsync(cancellationToken);

            var changedAt = clock.UtcNow;
            await auditTrail.WriteAsync(new AuditTrailEvent(
                ReadActorId(principal),
                isActive ? "Admin.Environment.Activate" : "Admin.Environment.Disable",
                "ServiceEnvironmentConfig",
                $"{service.Code}:{environment.Code}",
                true,
                previousState == isActive ? "NoChange" : "Changed",
                ServiceCode: service.Code,
                Metadata: new Dictionary<string, object?>
                {
                    ["environmentCode"] = environment.Code,
                    ["previousActive"] = previousState,
                    ["active"] = isActive,
                    ["serviceIdPreserved"] = true
                }), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new AdminEnvironmentStateResult(
                service.Id,
                service.Code,
                environment.Id,
                environment.Code,
                isActive,
                changedAt);
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { }
            throw;
        }
    }

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
