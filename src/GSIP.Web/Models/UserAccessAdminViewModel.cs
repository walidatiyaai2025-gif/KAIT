namespace GSIP.Web.Models;

public sealed class UserAccessAdminViewModel
{
    public required IReadOnlyList<UserAccessEntityOptionViewModel> Entities { get; init; }
    public required IReadOnlyList<UserAccessServiceOptionViewModel> Services { get; init; }
    public required IReadOnlyList<UserAccessRoleOptionViewModel> Roles { get; init; }
    public Guid? SelectedUserId { get; init; }
    public string SelectedUserDisplayName { get; init; } = string.Empty;
    public string SelectedUsername { get; init; } = string.Empty;
    public bool AllEntities { get; init; } = true;
    public required IReadOnlySet<string> SelectedEntityCodes { get; init; }
    public bool AllServices { get; init; } = true;
    public required IReadOnlySet<string> SelectedServiceKeys { get; init; }

    public bool SelectedUserHasEntity(string entityCode) =>
        AllEntities
        || SelectedEntityCodes.Contains(entityCode.Trim().ToUpperInvariant());

    public bool SelectedUserHasService(string entityCode, string serviceCode) =>
        AllServices
        || SelectedServiceKeys.Contains(ServiceKey(entityCode, serviceCode));

    public static string ServiceKey(string entityCode, string serviceCode) =>
        $"{entityCode.Trim().ToUpperInvariant()}::{serviceCode.Trim().ToUpperInvariant()}";
}

public sealed record UserAccessEntityOptionViewModel(
    string Code,
    string NameAr,
    string NameEn);

public sealed record UserAccessServiceOptionViewModel(
    string EntityCode,
    string ServiceCode,
    string NameAr,
    string NameEn);

public sealed record UserAccessRoleOptionViewModel(Guid Id, string Name);
