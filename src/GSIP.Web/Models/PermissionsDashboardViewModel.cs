namespace GSIP.Web.Models;

public sealed class PermissionsDashboardViewModel
{
    public required IReadOnlyList<PermissionRoleViewModel> Roles { get; init; }
    public required IReadOnlyList<PermissionDefinitionViewModel> Permissions { get; init; }
    public required IReadOnlyList<ServicePermissionViewModel> Services { get; init; }
    public required IReadOnlyList<UserRoleSummaryViewModel> Users { get; init; }
    public required IReadOnlyDictionary<Guid, IReadOnlySet<string>> RolePermissionGrants { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlySet<Guid>> ServiceExecuteGrants { get; init; }
    public UserRoleSummaryViewModel? SelectedUser { get; init; }
    public required int PendingApprovals { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public int TotalUsers => Users.Count;
    public int ActiveRoles => Roles.Count;
    public int PrivilegedUsers => Users.Count(user => user.IsPrivileged);
    public int ExplicitServiceGrants => ServiceExecuteGrants.Values.Sum(roleIds => roleIds.Count);

    public bool HasRolePermission(Guid roleId, string permissionKey) =>
        RolePermissionGrants.TryGetValue(roleId, out var permissions)
        && permissions.Contains(permissionKey);

    public bool HasServiceExecute(Guid roleId, string serviceCode) =>
        ServiceExecuteGrants.TryGetValue(serviceCode, out var roles)
        && roles.Contains(roleId);

    public bool SelectedUserHasRole(Guid roleId) =>
        SelectedUser?.RoleIds.Contains(roleId) == true;
}

public sealed record PermissionRoleViewModel(Guid Id, string Name, bool IsSeedRole);

public sealed record PermissionDefinitionViewModel(string Key, string Category);

public sealed record ServicePermissionViewModel(string EntityCode, string ServiceCode);

public sealed record UserRoleSummaryViewModel(
    Guid Id,
    string DisplayName,
    string Username,
    bool IsEnabled,
    bool IsPrivileged,
    IReadOnlySet<Guid> RoleIds,
    IReadOnlyList<string> Roles);
