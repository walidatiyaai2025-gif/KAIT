using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var connectionString = Environment.GetEnvironmentVariable("GSIP_P04_AUTHZ_SQL");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("GSIP_P04_AUTHZ_SQL is required for the P04 authorization policy negative gate.");
}

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<GsipDbContext>(options => options.UseSqlServer(connectionString));
services.AddScoped<IGsipPermissionEvaluator, GsipPermissionEvaluator>();
services.AddScoped<IAuthorizationHandler, GsipPermissionAuthorizationHandler>();
services.AddAuthorization(options => GsipAuthorizationPolicyRegistration.AddPolicies(options));

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
await db.Database.EnsureDeletedAsync();

try
{
    await db.Database.MigrateAsync();

    var adminId = Guid.NewGuid();
    var operatorId = Guid.NewGuid();
    var noRoleId = Guid.NewGuid();

    db.Users.AddRange(
        CreateUser(adminId, "authz.admin.synthetic"),
        CreateUser(operatorId, "authz.operator.synthetic"),
        CreateUser(noRoleId, "authz.norole.synthetic"));

    db.UserRoles.AddRange(
        new IdentityUserRole<Guid>
        {
            UserId = adminId,
            RoleId = Guid.Parse(GsipRoles.SystemAdministratorId)
        },
        new IdentityUserRole<Guid>
        {
            UserId = operatorId,
            RoleId = Guid.Parse(GsipRoles.ServiceOperatorId)
        });
    await db.SaveChangesAsync();

    var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
    var evaluator = scope.ServiceProvider.GetRequiredService<IGsipPermissionEvaluator>();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
    var admin = CreatePrincipal(adminId);
    var serviceOperator = CreatePrincipal(operatorId);
    var noRole = CreatePrincipal(noRoleId);
    var forgedAdministrator = CreatePrincipal(Guid.NewGuid(), GsipRoles.SystemAdministrator);

    Assert(!(await authorization.AuthorizeAsync(unauthenticated, null, GsipPermissions.EntitiesView)).Succeeded,
        "Unauthenticated principals must be denied by every P04 permission policy.");
    Assert(!(await authorization.AuthorizeAsync(noRole, null, GsipPermissions.EntitiesView)).Succeeded,
        "Authenticated users without persisted role assignments must be denied.");
    Assert((await authorization.AuthorizeAsync(serviceOperator, null, GsipPermissions.ServicesExecute)).Succeeded,
        "Service Operator must receive its persisted base Services.Execute permission.");
    Assert(!(await authorization.AuthorizeAsync(serviceOperator, null, GsipPermissions.RolesManage)).Succeeded,
        "Service Operator must not gain Roles.Manage through authentication alone.");
    Assert((await authorization.AuthorizeAsync(admin, null, GsipPermissions.RolesManage)).Succeeded,
        "A persisted System Administrator assignment must satisfy Roles.Manage.");
    Assert(!(await authorization.AuthorizeAsync(forgedAdministrator, null, GsipPermissions.RolesManage)).Succeeded,
        "A forged role claim without a persisted UserRole assignment must not elevate privileges.");
    Assert(options.GetPolicy("Unknown.Permission") is null,
        "Unknown permission names must not resolve to an implicit allow policy.");

    Assert(!await evaluator.HasServicePermissionAsync(
            serviceOperator,
            "SVC-ALPHA",
            GsipPermissions.ServicesExecute),
        "Global Services.Execute must not bypass the per-service Default Deny matrix.");

    db.RoleServicePermissions.AddRange(
        new RoleServicePermission
        {
            RoleId = Guid.Parse(GsipRoles.ServiceOperatorId),
            ServiceCode = "SVC-ALPHA",
            PermissionKey = GsipPermissions.ServicesExecute,
            IsAllowed = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        new RoleServicePermission
        {
            RoleId = Guid.Parse(GsipRoles.ServiceOperatorId),
            ServiceCode = "SVC-BETA",
            PermissionKey = GsipPermissions.ServicesExecute,
            IsAllowed = false,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    await db.SaveChangesAsync();

    Assert(await evaluator.HasServicePermissionAsync(
            serviceOperator,
            "svc-alpha",
            GsipPermissions.ServicesExecute),
        "Explicit service allowance must authorize only the assigned service code.");
    Assert(!await evaluator.HasServicePermissionAsync(
            serviceOperator,
            "SVC-BETA",
            GsipPermissions.ServicesExecute),
        "Explicit service denial must resist cross-service authorization bypass attempts.");
    Assert(!await evaluator.HasServicePermissionAsync(
            serviceOperator,
            "SVC-GAMMA",
            GsipPermissions.ServicesExecute),
        "Unknown/unassigned service identifiers must remain Default Deny.");
    Assert(!await evaluator.HasServicePermissionAsync(
            noRole,
            "SVC-ALPHA",
            GsipPermissions.ServicesExecute),
        "A user without the persisted role must not reuse another role's service entitlement.");
    Assert(!await evaluator.HasServicePermissionAsync(
            forgedAdministrator,
            "SVC-ALPHA",
            GsipPermissions.ServicesExecute),
        "Forged role claims must not bypass the database-backed service permission matrix.");

    Console.WriteLine("P04 authorization policy negative gate PASS.");
    Console.WriteLine("unauthenticated=DENY; no_role=DENY; forged_role=DENY; role_policy=PASS; service_default_deny=PASS; cross_service_bypass=DENY");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static ApplicationUser CreateUser(Guid id, string username) => new()
{
    Id = id,
    DisplayName = username,
    UserName = username,
    NormalizedUserName = username.ToUpperInvariant(),
    Email = $"{username}@example.invalid",
    NormalizedEmail = $"{username}@example.invalid".ToUpperInvariant(),
    EmailConfirmed = true,
    IsEnabled = true,
    IsPrivileged = false,
    MustChangePassword = false,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    LockoutEnabled = true,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};

static ClaimsPrincipal CreatePrincipal(Guid userId, string? forgedRole = null)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.ToString())
    };
    if (!string.IsNullOrWhiteSpace(forgedRole))
    {
        claims.Add(new Claim(ClaimTypes.Role, forgedRole));
    }

    return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "P04Synthetic"));
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
