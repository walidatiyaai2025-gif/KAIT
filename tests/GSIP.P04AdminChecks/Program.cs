using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Controllers;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
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

    var lastManagerGrantRemoval = await controller.SetRolePermission(
        systemAdminRoleId,
        GsipPermissions.RolesManage,
        false,
        adminOne.Id,
        "en",
        CancellationToken.None);
    Assert(lastManagerGrantRemoval is BadRequestObjectResult,
        "Removing the final effective Roles.Manage grant must be rejected.");
    Assert(await db.RolePermissions.AnyAsync(row => row.RoleId == systemAdminRoleId
            && row.PermissionKey == GsipPermissions.RolesManage
            && row.IsAllowed),
        "Last-manager grant protection modified the protected Roles.Manage grant.");

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
    db.ChangeTracker.Clear();
    Assert(await db.Roles.AsNoTracking().AnyAsync(role => role.Id == customRole.Id && role.Name == "Synthetic Reviewer"),
        "Renamed custom role was not persisted to SQL.");
    Assert(await controller.RenameRole(Guid.NewGuid(), "Ghost Role", regular.Id, "en", CancellationToken.None) is NotFoundResult,
        "Unknown role IDs must be rejected during rename.");

    var seedRename = await controller.RenameRole(auditorRoleId, "Auditor Renamed", regular.Id, "en", CancellationToken.None);
    Assert(seedRename is RedirectToActionResult, "Seed roles must remain editable.");
    db.ChangeTracker.Clear();
    Assert(await db.Roles.AsNoTracking().AnyAsync(role => role.Id == auditorRoleId && role.Name == "Auditor Renamed"),
        "Seed role rename was not persisted to SQL.");
    Assert(await controller.RenameRole(auditorRoleId, GsipRoles.Auditor, regular.Id, "en", CancellationToken.None) is RedirectToActionResult,
        "Seed role restore must succeed after rename validation.");
    db.ChangeTracker.Clear();

    Assert(await controller.SetUserRole(regular.Id, customRole.Id, true, "en", CancellationToken.None) is RedirectToActionResult,
        "Assigning an existing role to an existing user must succeed.");
    Assert(await db.UserRoles.AnyAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id),
        "UserRole assignment was not persisted.");
    Assert(await controller.SetUserRole(regular.Id, customRole.Id, true, "en", CancellationToken.None) is RedirectToActionResult,
        "Assigning an already-assigned role must be idempotent.");
    Assert(await db.UserRoles.CountAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id) == 1,
        "Idempotent role assignment created a duplicate UserRole row.");

    Assert(await controller.SetRolePermission(customRole.Id, GsipPermissions.RolesManage, true, regular.Id, "en", CancellationToken.None)
        is RedirectToActionResult,
        "Granting Roles.Manage to a second enabled manager role must succeed.");
    Assert(await controller.SetRolePermission(systemAdminRoleId, GsipPermissions.RolesManage, false, adminOne.Id, "en", CancellationToken.None)
        is RedirectToActionResult,
        "Removing Roles.Manage from one role must succeed when another enabled manager remains.");
    var lastManagerAssignmentRemoval = await controller.SetUserRole(regular.Id, customRole.Id, false, "en", CancellationToken.None);
    Assert(lastManagerAssignmentRemoval is BadRequestObjectResult,
        "Removing the final effective Roles.Manage role assignment must be rejected.");
    Assert(await db.UserRoles.AnyAsync(row => row.UserId == regular.Id && row.RoleId == customRole.Id),
        "Last-manager assignment protection removed the protected role assignment.");
    Assert(await controller.SetRolePermission(systemAdminRoleId, GsipPermissions.RolesManage, true, adminOne.Id, "en", CancellationToken.None)
        is RedirectToActionResult,
        "Restoring the System Administrator Roles.Manage grant must succeed.");
    Assert(await controller.SetRolePermission(customRole.Id, GsipPermissions.RolesManage, false, regular.Id, "en", CancellationToken.None)
        is RedirectToActionResult,
        "Removing a secondary Roles.Manage grant must succeed when another enabled manager remains.");

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

    var now = DateTimeOffset.UtcNow;
    var mojEntity = new CatalogEntity
    {
        Id = Guid.NewGuid(),
        Code = "MOJ",
        NameAr = "وزارة العدل",
        NameEn = "Ministry of Justice",
        Logo = string.Empty,
        Active = true,
        DisplayOrder = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
    var mohEntity = new CatalogEntity
    {
        Id = Guid.NewGuid(),
        Code = "MOH",
        NameAr = "وزارة الصحة",
        NameEn = "Ministry of Health",
        Logo = string.Empty,
        Active = true,
        DisplayOrder = 2,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
    var mojServiceA = CreateService(mojEntity.Id, "P04_MOJ_A", "MOJ Service A", now);
    var mojServiceB = CreateService(mojEntity.Id, "P04_MOJ_B", "MOJ Service B", now);
    var mohServiceA = CreateService(mohEntity.Id, "P04_MOH_A", "MOH Service A", now);
    db.CatalogEntities.AddRange(mojEntity, mohEntity);
    db.CatalogServices.AddRange(mojServiceA, mojServiceB, mohServiceA);

    var executeGrant = await db.RolePermissions.SingleOrDefaultAsync(row =>
        row.RoleId == systemAdminRoleId && row.PermissionKey == GsipPermissions.ServicesExecute);
    if (executeGrant is null)
    {
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = systemAdminRoleId,
            PermissionKey = GsipPermissions.ServicesExecute,
            IsAllowed = true,
            UpdatedAtUtc = now
        });
    }
    else
    {
        executeGrant.IsAllowed = true;
        executeGrant.UpdatedAtUtc = now;
    }

    foreach (var serviceCode in new[] { mojServiceA.Code, mojServiceB.Code, mohServiceA.Code })
    {
        db.RoleServicePermissions.Add(new RoleServicePermission
        {
            RoleId = systemAdminRoleId,
            ServiceCode = serviceCode,
            PermissionKey = GsipPermissions.ServicesExecute,
            IsAllowed = true,
            UpdatedAtUtc = now
        });
    }
    await db.SaveChangesAsync();

    var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, adminTwo.Id.ToString())],
        "P04-Test"));
    var evaluator = new GsipPermissionEvaluator(db);

    Assert(await evaluator.HasServicePermissionAsync(principal, mojServiceA.Code, GsipPermissions.ServicesExecute),
        "Existing users without an explicit access scope must remain backward-compatible and unrestricted.");
    Assert(await evaluator.HasServicePermissionAsync(principal, mohServiceA.Code, GsipPermissions.ServicesExecute),
        "Existing unrestricted users lost a role-granted service after user-scope introduction.");

    await EntityAccessScopeStore.SetForUserAsync(
        db,
        adminTwo.Id,
        allEntities: false,
        entityCodes: ["MOJ"],
        allServices: false,
        services: [("MOJ", mojServiceA.Code)],
        CancellationToken.None);
    Assert(await evaluator.HasServicePermissionAsync(principal, mojServiceA.Code, GsipPermissions.ServicesExecute),
        "Selected service inside the selected entity must remain executable.");
    Assert(!await evaluator.HasServicePermissionAsync(principal, mojServiceB.Code, GsipPermissions.ServicesExecute),
        "Unselected service inside an allowed entity must be denied.");
    Assert(!await evaluator.HasServicePermissionAsync(principal, mohServiceA.Code, GsipPermissions.ServicesExecute),
        "Service in an unselected entity must be denied.");

    await EntityAccessScopeStore.SetForUserAsync(
        db,
        adminTwo.Id,
        allEntities: false,
        entityCodes: ["MOJ"],
        allServices: true,
        services: [],
        CancellationToken.None);
    Assert(await evaluator.HasServicePermissionAsync(principal, mojServiceB.Code, GsipPermissions.ServicesExecute),
        "All-services scope must allow every role-granted service inside the selected entity.");
    Assert(!await evaluator.HasServicePermissionAsync(principal, mohServiceA.Code, GsipPermissions.ServicesExecute),
        "All-services scope must not bypass the selected entity boundary.");

    var userAdminHttpContext = new DefaultHttpContext();
    var userAdminController = new UserAccessAdministrationController(db, userManager)
    {
        ControllerContext = new ControllerContext { HttpContext = userAdminHttpContext },
        TempData = new TempDataDictionary(userAdminHttpContext, new TestTempDataProvider())
    };
    var createScopedUser = await userAdminController.CreateUser(
        "Scoped Operator",
        "p04.scoped.operator",
        "p04.scoped.operator@example.invalid",
        "OPS",
        "ScopedPass@2026",
        false,
        auditorRoleId,
        false,
        ["MOJ"],
        false,
        [$"MOJ::{mojServiceA.Code}"],
        "en",
        CancellationToken.None);
    Assert(createScopedUser is RedirectToActionResult,
        "Governed user provisioning must create a valid local account and redirect to permissions.");

    var scopedUser = await userManager.FindByNameAsync("p04.scoped.operator");
    Assert(scopedUser is not null, "Governed user provisioning did not persist the new account.");
    Assert(scopedUser!.MustChangePassword, "Newly provisioned users must be forced to change the initial password.");
    Assert(await userManager.IsInRoleAsync(scopedUser, GsipRoles.Auditor),
        "The selected initial role was not assigned to the newly provisioned user.");
    var scopedUserAccess = await EntityAccessScopeStore.GetForUserAsync(db, scopedUser.Id);
    Assert(!scopedUserAccess.AllEntities && !scopedUserAccess.AllServices,
        "New user's explicit entity/service restrictions were not persisted.");
    Assert(scopedUserAccess.AllowsService("MOJ", mojServiceA.Code),
        "New user's selected service scope is missing.");
    Assert(!scopedUserAccess.AllowsService("MOJ", mojServiceB.Code),
        "New user's unselected service must remain denied by the user scope.");
    Assert(!scopedUserAccess.AllowsService("MOH", mohServiceA.Code),
        "New user's unselected entity must remain denied by the user scope.");

    var invalidScopedUser = await userAdminController.CreateUser(
        "Invalid Scoped Operator",
        "p04.invalid.scope",
        "p04.invalid.scope@example.invalid",
        null,
        "ScopedPass@2026",
        false,
        auditorRoleId,
        false,
        ["MOJ"],
        false,
        [$"MOH::{mohServiceA.Code}"],
        "en",
        CancellationToken.None);
    Assert(invalidScopedUser is RedirectToActionResult,
        "Invalid cross-entity service scope must return safely to permissions.");
    Assert(await userManager.FindByNameAsync("p04.invalid.scope") is null,
        "Invalid cross-entity service scope must be rejected before user creation.");
    Assert(string.Equals(userAdminController.TempData["PermissionsAdminError"] as string, "UserAccessInvalid", StringComparison.Ordinal),
        "Invalid user access scope did not surface the expected safe validation code.");

    var dashboard = await controller.Index(regular.Id, CancellationToken.None) as ViewResult;
    if (dashboard?.Model is not PermissionsDashboardViewModel model)
    {
        throw new InvalidOperationException("Permissions dashboard model was not produced.");
    }
    Assert(model.PendingApprovals == 0, "Pending approvals must render truthful zero until a governed approval source exists.");
    Assert(model.SelectedUser?.Id == regular.Id, "Selected user identity was not preserved by the dashboard.");

    Console.WriteLine("P04 admin, user provisioning and entity-service scope gate PASS.");
    Console.WriteLine("role_create_default_deny=PASS; seed_role_edit=PASS; user_role_assign_remove=PASS; user_provisioning=PASS; entity_scope=PASS; service_scope=PASS; forged_ids=DENY; last_admin_lockout=DENY; last_roles_manager_lockout=DENY; pending_approvals_truthful_zero=PASS");
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

static CatalogService CreateService(Guid entityId, string code, string name, DateTimeOffset now) => new()
{
    Id = Guid.NewGuid(),
    DefinitionKey = Guid.NewGuid(),
    EntityId = entityId,
    Code = code,
    NameAr = name,
    NameEn = name,
    DescriptionAr = name,
    DescriptionEn = name,
    Active = true,
    Version = 1,
    IsCurrent = true,
    CreatedAtUtc = now,
    UpdatedAtUtc = now
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
