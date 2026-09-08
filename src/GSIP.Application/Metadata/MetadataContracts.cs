using GSIP.Domain.Metadata;

namespace GSIP.Application.Metadata;

public sealed record EntityInput(
    string Code,
    string NameAr,
    string NameEn,
    string Logo,
    bool Active,
    int DisplayOrder);

public sealed record ServiceInput(
    string Code,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    bool Active,
    List<ServiceEnvironmentInput> EnvironmentConfigs,
    List<ServiceFieldInput> Fields,
    List<ResultMappingInput> ResultMappings);

public sealed record ServiceEnvironmentInput(
    string EnvironmentCode,
    string BaseUrl,
    string RelativePath,
    string HttpMethod,
    string ContentType,
    string NonSecretHeadersJson,
    int TimeoutSeconds,
    string TlsPolicy,
    bool ValidateServerCertificate,
    string ProxyUrl,
    string HealthPath,
    string HealthMethod,
    bool Active,
    Guid? AuthProfileId);

public sealed record ServiceFieldInput(
    string Key,
    string LabelAr,
    string LabelEn,
    string FieldType,
    bool Required,
    string Regex,
    decimal? Minimum,
    decimal? Maximum,
    int? MinLength,
    int? MaxLength,
    string OptionsJson,
    int DisplayOrder,
    bool Sensitive,
    string Masking);

public sealed record ResultMappingInput(
    string SourcePath,
    string LabelAr,
    string LabelEn,
    string ResultType,
    string Formatter,
    bool Sensitive,
    int DisplayOrder);

public sealed record MetadataCatalogSnapshot(
    IReadOnlyList<CatalogEntity> Entities,
    IReadOnlyList<CatalogService> Services,
    IReadOnlyList<CatalogEnvironment> Environments);

public sealed record MetadataImportResult(int EntitiesProcessed, int ServicesProcessed, int RevisionsCreated);

public sealed class MetadataPackage
{
    public int SchemaVersion { get; set; } = 1;
    public List<MetadataEntityPackage> Entities { get; set; } = [];
}

public sealed class MetadataEntityPackage
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int DisplayOrder { get; set; }
    public List<MetadataServicePackage> Services { get; set; } = [];
}

public sealed class MetadataServicePackage
{
    private List<ServiceEnvironmentInput> _environmentConfigs = [];

    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public bool Active { get; set; } = true;

    public List<ServiceEnvironmentInput> EnvironmentConfigs
    {
        get => _environmentConfigs;
        set => _environmentConfigs = (value ?? [])
            .GroupBy(config => config.EnvironmentCode?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public List<ServiceFieldInput> Fields { get; set; } = [];
    public List<ResultMappingInput> ResultMappings { get; set; } = [];
}

public interface IMetadataCatalogService
{
    Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default);
    Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default);
    Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default);
    Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default);
    Task<CatalogService> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);
    Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default);
    Task<string> ExportJsonAsync(CancellationToken cancellationToken = default);
    Task<MetadataImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default);
}
