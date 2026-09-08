using GSIP.Application.Authorization;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Controllers;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var connectionString = Environment.GetEnvironmentVariable("GSIP_P04_SQL");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("GSIP_P04_SQL is required for P04 admin checks.");
}

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<GsipDbContext>(options => options.UseSqlServer(connectionString));
services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<GsipDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<GsipDbContext>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
await db.Database.EnsureDeletedAsync();

try
{
    await db.Database.MigrateAsync();

    var systemAdminRoleId = Guid.Parse(GsipRoles.SystemAdministratorId);
    var auditorRoleId = Guid.Parse(GsipRoles.AuditorId);
    Assert(await db.Roles.AnyAsync(role => role.Id == systemAdminRoleId), "Seed System Administrator role is missing.");
    Assert(await db.Roles.AnyAsync(role => role.Id == auditorRoleId), "Seed Auditor role is missing.");

    var adminOne = CreateUser("p04.admin.one", enabled: true);
    var adminTwo = CreateUser("p04.admin.two", enabled: true);
    var regular = CreateUser("p04.regular", enabled: true);
    db.Users.AddRange(adminOne, adminTwo, regular);
    db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = adminOne.Id, RoleId = systemAdminRoleId });
    await db.SaveChangesAsync();

    var controller = new PermissionsController(db, roleManager);

    var createResult = await controller.CreateRole("  Synthetic Analyst  ", regular.Id, "en", CancellationToken.None);
    Assert(createResult is RedirectToActionResult, "Creating a valid role must redirect back to the dashboard.");
    var customRole = await db.Roles.SingleAsync(role => role.Name == "Synthetic Analyst");
    Assert(!await db.RolePermissions.AnyAsync(row => row.RoleId == customRole.Id && row.IsAllowed),
        "New roles must start with zero global grants under Default Deny.");
    Assert(!await db.RoleServicePermissions.AnyAsync(row => row.RoleId == customRole.Id && row.IsAllowed),
        "New roles must start with zero service grants under Default Deny.");

    var duplicateResult = await controller.CreateRole("Synthetic Analyst", regular.Id, "en", CancellationToken.None);
    Assert(duplicateResult is ConflictObjectResult, "Duplicate role names must be rejected.");
    Assert(await controller.CreateRole("   ", regular.Id, "en", CancellationToken.None) is BadRequestObjectResult,
        "Blank role names must be rejected.");

    var renameResult = await controller.RenameRole(customRole.Id, "Synthetic Reviewer", regular.Id, "en", CancellationToken.None);
    Assert(renameResult is RedirectToActionResult, "Custom roles must be renameable.");
    Assert(await db.Roles.AnyAsync(role => role.Id == customRole.Id && role.Name == "Synthetic Reviewer"),
        "Renamed custom role was not persisted.");
    Assert(await controller.RenameRole(Guid.NewGuid(), "Ghost Role", regular.Id, "en", CancellationToken.None) is NotFoundResult,
        "Unknown role IDs must be rejected during rename.");

    var seedRename = await controller.RenameRole(auditorRoleId, "Auditor Renamed", regular.Id, "en", CancellationToken.None);
    Assert(seedRename is RedirectToActionResult, "Seed roles must remain editable.");
    Assert(await db.Roles.AnyAsync(role => role.Id == auditorRoleId && role.Name == "Auditor Renamed"),
        "Seed role rename was not persisted.");
    Assert(await controller.RenameRole(auditorRoleId, GsipRoles.Auditor, regular.Id, "en", CancellationToken.None) is RedirectToActionResult,
        "Seed role restore must succeed after rename validation.");

    Assert(await controller.SetUserRole(regular.Id, customRole.Id, true, "en", CancellationToken.None) is RedirectToActionResult,
        "Assigning an existing role to an existing user must succeed.");
    Assert(await db.UserRoles.AnyAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id),
        "UserRole assignment was not persisted.");
    Assert(await controller.SetUserRole(regular.Id, customRole.Id, true, "en", CancellationToken.None) is RedirectToActionResult,
        "Assigning an already-assigned role must be idempotent.");
    Assert(await db.UserRoles.CountAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id) == 1,
        "Idempotent role assignment created a duplicate UserRole row.");

    Assert(await controller.SetUserRole(Guid.NewGuid(), customRole.Id, true, "en", CancellationToken.None) is NotFoundResult,
        "Forged/unknown user IDs must be rejected.");
    Assert(await controller.SetUserRole(regular.Id, Guid.NewGuid(), true, "en", CancellationToken.None) is NotFoundResult,
        "Forged/unknown role IDs must be rejected.");

    Assert(await controller.SetUserRole(regular.Id, customRole.Id, false, "en", CancellationToken.None) is RedirectToActionResult,
        "Removing an assigned non-admin role must succeed.");
    Assert(!await db.UserRoles.AnyAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id),
        "UserRole removal was not persisted.");

    var lastAdminRemoval = await controller.SetUserRole(adminOne.Id, systemAdminRoleId, false, "en", CancellationToken.None);
    Assert(lastAdminRemoval is BadRequestObjectResult,
        "Removing the last enabled System Administrator assignment must be rejected.");
    Assert(await db.UserRoles.AnyAsync(row => row.UserId == adminOne.Id && row.RoleId == systemAdminRoleId),
        "Last-admin protection removed the protected assignment.");

    Assert(await controller.SetUserRole(adminTwo.Id, systemAdminRoleId, true, "en", CancellationToken.None) is RedirectToActionResult,
        "Assigning a second enabled System Administrator must succeed.");
    Assert(await controller.SetUserRole(adminOne.Id, systemAdminRoleId, false, "en", CancellationToken.None) is RedirectToActionResult,
        "Removing one System Administrator must succeed when another enabled administrator remains.");
    Assert(!await db.UserRoles.AnyAsync(row => row.UserId == adminOne.Id && row.RoleId == systemAdminRoleId),
        "System Administrator removal with a safe remaining administrator was not persisted.");
    Assert(await db.UserRoles.AnyAsync(row => row.UserId == adminTwo.Id && row.RoleId == systemAdminRoleId),
        "Safe remaining System Administrator assignment was lost.");

    var dashboard = await controller.Index(regular.Id, CancellationToken.None) as ViewResult;
    Assert(dashboard?.Model is PermissionsDashboardViewModel model, "Permissions dashboard model was not produced.");
    Assert(model.PendingApprovals == 0, "Pending approvals must render truthful zero until a governed approval source exists.");
    Assert(model.SelectedUser?.Id == regular.Id, "Selected user identity was not preserved by the dashboard.");

    Console.WriteLine("P04 admin and IDOR gate PASS.");
    Console.WriteLine("role_create_default_deny=PASS; seed_role_edit=PASS; user_role_assign_remove=PASS; forged_ids=DENY; last_admin_lockout=DENY; pending_approvals_truthful_zero=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static ApplicationUser CreateUser(string username, bool enabled) => new()
{
    Id = Guid.NewGuid(),
    DisplayName = username,
    UserName = username,
    NormalizedUserName = username.ToUpperInvariant(),
    Email = $"{username}@example.invalid",
    NormalizedEmail = $"{username}@example.invalid".ToUpperInvariant(),
    EmailConfirmed = true,
    IsEnabled = enabled,
    IsPrivileged = false,
    MustChangePassword = false,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    LockoutEnabled = true,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
