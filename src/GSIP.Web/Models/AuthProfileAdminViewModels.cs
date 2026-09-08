namespace GSIP.Web.Models;

public sealed record AuthProfileAdminViewModel
{
    public IReadOnlyList<AuthProfileAdminEntityViewModel> Entities { get; init; } = [];
    public IReadOnlyList<AuthProfileSummaryViewModel> Profiles { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
}

public sealed record AuthProfileAdminEntityViewModel(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    IReadOnlyList<AuthProfileAdminServiceViewModel> Services);

public sealed record AuthProfileAdminServiceViewModel(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    IReadOnlyList<AuthProfileEnvironmentViewModel> Environments);

public sealed record AuthProfileEnvironmentViewModel(
    Guid Id,
    string Code,
    bool IsActive,
    Guid? AuthProfileId,
    string? AuthProfileName,
    bool IsSharedProfile);

public sealed record AuthProfileSummaryViewModel(
    Guid Id,
    string Name,
    string AuthenticationType,
    bool IsShared,
    bool IsActive,
    int BindingCount,
    IReadOnlyList<SecretRefSummaryViewModel> Secrets,
    DateTimeOffset? LastRotatedAtUtc);

/// <summary>
/// Deliberately contains metadata only. Plaintext secret material must never be projected to a view model.
/// </summary>
public sealed record SecretRefSummaryViewModel(
    Guid Id,
    string Label,
    string MaskedState,
    long Generation,
    bool IsActive,
    DateTimeOffset? UpdatedAtUtc);
