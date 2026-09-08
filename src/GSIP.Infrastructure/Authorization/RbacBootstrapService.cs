using GSIP.Application.Authorization;
using GSIP.Application.Identity;
using GSIP.Application.Setup;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Authorization;

internal sealed class RbacBootstrapService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        if (!(await setup.GetStatusAsync(cancellationToken)).IsCompleted)
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
        var systemAdministratorRoleId = Guid.Parse(GsipRoles.SystemAdministratorId);
        if (!await dbContext.Roles.AnyAsync(role => role.Id == systemAdministratorRoleId, cancellationToken))
        {
            throw new InvalidOperationException("P04 RBAC migration did not seed the System Administrator role.");
        }

        var authentication = scope.ServiceProvider.GetRequiredService<IAccountAuthenticationService>();
        await authentication.EnsureBootstrapAdministratorAsync(cancellationToken);

        var bootstrap = await dbContext.BootstrapAdministrators
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (bootstrap is null || !await dbContext.Users.AnyAsync(user => user.Id == bootstrap.Id, cancellationToken))
        {
            return;
        }

        var alreadyAssigned = await dbContext.UserRoles.AnyAsync(
            row => row.UserId == bootstrap.Id && row.RoleId == systemAdministratorRoleId,
            cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        dbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = bootstrap.Id,
            RoleId = systemAdministratorRoleId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
