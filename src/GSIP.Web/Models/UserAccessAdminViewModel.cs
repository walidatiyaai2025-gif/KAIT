namespace GSIP.Web.Models;

public sealed class UserAccessAdminViewModel
{
    public required IReadOnlyList<UserAccessEntityOptionViewModel> Entities { get; init; }
    public required IReadOnlyList<UserAccessRoleOptionViewModel> Roles { get; init; }
    public Guid? SelectedUserId { get; init; }
    public string SelectedUserDisplayName { get; init; } = string.Empty;
    public string SelectedUsername { get; init; } = string.Empty;
    public bool ScopeIsUnrestricted { get; init; } = true;
    public required IReadOnlySet<string> SelectedEntityCodes { get; init; }

    public bool SelectedUserHasEntity(string entityCode) =>
        ScopeIsUnrestricted
        || SelectedEntityCodes.Contains(entityCode.Trim().ToUpperInvariant());
}

public sealed record UserAccessEntityOptionViewModel(
    string Code,
    string NameAr,
    string NameEn);

public sealed record UserAccessRoleOptionViewModel(Guid Id, string Name);
