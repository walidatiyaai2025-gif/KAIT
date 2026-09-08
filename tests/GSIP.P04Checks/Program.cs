using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var connectionString = Environment.GetEnvironmentVariable("GSIP_P04_SQL");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("GSIP_P04_SQL is required for the P04 SQL authorization checks.");
}

Assert(GsipPermissions.All.Count == 20, "The canonical permission catalog must contain exactly 20 P04 permissions.");
Assert(GsipPermissions.All.Distinct(StringComparer.Ordinal).Count() == GsipPermissions.All.Count, "Permission keys must be unique.");
Assert(GsipRoles.SeedRoles.Count == 5, "P04 must define exactly five canonical seed roles.");
Assert(GsipRoles.SeedRoles.Select(role => role.Id).Distinct().Count() == 5, "Seed role identifiers must be stable and unique.");

var authorizationOptions = new AuthorizationOptions();
GsipAuthorizationPolicyRegistration.AddPolicies(authorizationOptions);
foreach (var permission in GsipPermissions.All)
{
    var policy = authorizationOptions.GetPolicy(permission);
    Assert(policy is not null, $"Missing server-side authorization policy for {permission}.");
    Assert(policy!.Requirements.OfType<GsipPermissionRequirement>().Any(requirement => requirement.Permission == permission),
        $"Policy {permission} must carry the GSIP permission requirement.");
}
Assert(authorizationOptions.GetPolicy("Unknown.Permission") is null, "Unknown permissions must not be registered implicitly.");

var options = new DbContextOptionsBuilder<GsipDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();

    var seededRoles = await db.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync();
    Assert(seededRoles.Count == 5, "A fresh P04 database must seed the five canonical roles.");
    foreach (var roleDefinition in GsipRoles.SeedRoles)
    {
        var role = seededRoles.SingleOrDefault(candidate => candidate.Id == roleDefinition.Id);
        Assert(role is not null, $"Seed role {roleDefinition.Name} is missing.");
        Assert(string.Equals(role!.Name, roleDefinition.Name, StringComparison.Ordinal),
            $"Seed role {roleDefinition.Name} must start with its canonical editable name.");
    }

    var systemAdministratorId = Guid.Parse(GsipRoles.SystemAdministratorId);
    var systemPermissionCount = await db.RolePermissions.AsNoTracking()
        .CountAsync(row => row.RoleId == systemAdministratorId && row.IsAllowed);
    Assert(systemPermissionCount == GsipPermissions.All.Count,
        "System Administrator must receive the complete P04 permission catalog at initial seed.");

    var operatorId = Guid.NewGuid();
    var readOnlyId = Guid.NewGuid();
    db.Users.AddRange(
        CreateUser(operatorId, "operator.synthetic"),
        CreateUser(readOnlyId, "readonly.synthetic"));
    db.UserRoles.AddRange(
        new IdentityUserRole<Guid>
        {
            UserId = operatorId,
            RoleId = Guid.Parse(GsipRoles.ServiceOperatorId)
        },
        new IdentityUserRole<Guid>
        {
            UserId = readOnlyId,
            RoleId = Guid.Parse(GsipRoles.ReadOnlyId)
        });
    await db.SaveChangesAsync();

    var evaluator = new GsipPermissionEvaluator(db);
    var operatorPrincipal = CreatePrincipal(operatorId);
    var readOnlyPrincipal = CreatePrincipal(readOnlyId);

    Assert(await evaluator.HasPermissionAsync(operatorPrincipal, GsipPermissions.ServicesExecute),
        "Service Operator must receive its seeded base Services.Execute permission.");
    Assert(!await evaluator.HasPermissionAsync(operatorPrincipal, GsipPermissions.RolesManage),
        "Service Operator must be denied Roles.Manage by default.");
    Assert(!await evaluator.HasPermissionAsync(readOnlyPrincipal, GsipPermissions.ServicesExecute),
        "Read Only must not inherit service execution permission.");
    Assert(!await evaluator.HasPermissionAsync(operatorPrincipal, "Unknown.Permission"),
        "Unknown permissions must be Default Deny.");

    Assert(!await evaluator.HasServicePermissionAsync(
            operatorPrincipal,
            "SVC-ALPHA",
            GsipPermissions.ServicesExecute),
        "A service operation must be denied when no role/service matrix allowance exists.");

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
            operatorPrincipal,
            "svc-alpha",
            GsipPermissions.ServicesExecute),
        "An explicit per-role service allowance must permit the service operation.");
    Assert(!await evaluator.HasServicePermissionAsync(
            operatorPrincipal,
            "SVC-BETA",
            GsipPermissions.ServicesExecute),
        "An explicit per-role service deny must remain denied.");
    Assert(!await evaluator.HasServicePermissionAsync(
            operatorPrincipal,
            "SVC-ALPHA",
            GsipPermissions.RolesManage),
        "Non-service permissions must never be satisfied through the service matrix.");

    Console.WriteLine("P04 RBAC SQL/policy checks passed.");
    Console.WriteLine($"roles={seededRoles.Count}; permissions={GsipPermissions.All.Count}; default_deny=PASS; service_matrix=PASS; server_policies=PASS");
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

static ClaimsPrincipal CreatePrincipal(Guid userId) => new(
    new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
        authenticationType: "P04Synthetic"));

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
