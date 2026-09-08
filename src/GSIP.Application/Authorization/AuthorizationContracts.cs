using System.Security.Claims;

namespace GSIP.Application.Authorization;

public static class GsipPermissions
{
    public const string EntitiesView = "Entities.View";
    public const string EntitiesManage = "Entities.Manage";
    public const string ServicesView = "Services.View";
    public const string ServicesExecute = "Services.Execute";
    public const string ServicesManage = "Services.Manage";
    public const string ServiceSecretsManage = "ServiceSecrets.Manage";
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string RequestsViewOwn = "Requests.ViewOwn";
    public const string RequestsViewDepartment = "Requests.ViewDepartment";
    public const string RequestsViewAll = "Requests.ViewAll";
    public const string RequestsExport = "Requests.Export";
    public const string AuditView = "Audit.View";
    public const string AuditExport = "Audit.Export";
    public const string AuditViewSensitive = "Audit.ViewSensitive";
    public const string SettingsManage = "Settings.Manage";
    public const string DiagnosticsRun = "Diagnostics.Run";
    public const string SystemBackup = "System.Backup";
    public const string SystemRestore = "System.Restore";

    public static IReadOnlyList<string> All { get; } =
    [
        EntitiesView,
        EntitiesManage,
        ServicesView,
        ServicesExecute,
        ServicesManage,
        ServiceSecretsManage,
        UsersView,
        UsersManage,
        RolesManage,
        RequestsViewOwn,
        RequestsViewDepartment,
        RequestsViewAll,
        RequestsExport,
        AuditView,
        AuditExport,
        AuditViewSensitive,
        SettingsManage,
        DiagnosticsRun,
        SystemBackup,
        SystemRestore
    ];

    public static IReadOnlySet<string> ServiceScoped { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ServicesView,
        ServicesExecute,
        ServicesManage
    };

    public static bool IsKnown(string permission) =>
        All.Contains(permission, StringComparer.Ordinal);
}

public static class GsipRoles
{
    public const string SystemAdministrator = "System Administrator";
    public const string IntegrationManager = "Integration Manager";
    public const string ServiceOperator = "Service Operator";
    public const string Auditor = "Auditor";
    public const string ReadOnly = "Read Only";

    public const string SystemAdministratorId = "8d3408a0-74bb-44f7-a472-966baf8dde01";
    public const string IntegrationManagerId = "b1e6172b-4e62-4c75-9766-824e99b33d02";
    public const string ServiceOperatorId = "ca73c8af-71dc-4d16-b210-259573c3b403";
    public const string AuditorId = "da918663-e15f-4f19-a535-5f941eab5b04";
    public const string ReadOnlyId = "f2ec1b2d-c91a-48d8-a132-f4c2325c6c05";

    public static IReadOnlyList<SeedRoleDefinition> SeedRoles { get; } =
    [
        new(Guid.Parse(SystemAdministratorId), SystemAdministrator),
        new(Guid.Parse(IntegrationManagerId), IntegrationManager),
        new(Guid.Parse(ServiceOperatorId), ServiceOperator),
        new(Guid.Parse(AuditorId), Auditor),
        new(Guid.Parse(ReadOnlyId), ReadOnly)
    ];
}

public sealed record SeedRoleDefinition(Guid Id, string Name);

public interface IGsipPermissionEvaluator
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permission,
        CancellationToken cancellationToken = default);

    Task<bool> HasServicePermissionAsync(
        ClaimsPrincipal principal,
        string serviceCode,
        string permission,
        CancellationToken cancellationToken = default);
}
