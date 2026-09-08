namespace GSIP.Web.Models;

public sealed class MetadataDashboardViewModel
{
    public IReadOnlyList<MetadataEntityViewModel> Entities { get; init; } = [];
    public IReadOnlyList<MetadataServiceViewModel> Services { get; init; } = [];
    public string ServiceTemplateJson { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
}

public sealed record MetadataEntityViewModel(Guid Id, string Code, string NameAr, string NameEn, string Logo, bool Active, int DisplayOrder);

public sealed record MetadataServiceViewModel(
    Guid Id,
    Guid EntityId,
    string Code,
    string NameAr,
    string NameEn,
    bool Active,
    int Version,
    bool WasUsed,
    int EnvironmentCount,
    int FieldCount,
    int MappingCount,
    string DefinitionJson);
