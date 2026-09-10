namespace GSIP.Web.Models;

public sealed record ShellEntityOptionViewModel(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn);

public sealed record ShellServiceCardViewModel(
    Guid ServiceId,
    Guid EntityId,
    string EntityCode,
    string EntityNameAr,
    string EntityNameEn,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    string DescriptionAr,
    string DescriptionEn,
    int Version,
    int ActiveEnvironmentCount);

public sealed record ShellViewModel(
    string ProductVersion,
    string Phase,
    DateTimeOffset GeneratedAtUtc,
    int ActiveEntityCount,
    int ActiveServiceCount,
    int ActiveEnvironmentCount,
    string SelectedEntityCode,
    string Search,
    IReadOnlyList<ShellEntityOptionViewModel> Entities,
    IReadOnlyList<ShellServiceCardViewModel> Services);
