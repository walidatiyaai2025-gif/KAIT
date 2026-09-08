namespace GSIP.Domain.Metadata;

public static class CatalogEnvironmentCodes
{
    public const string Uat = "UAT";
    public const string Production = "Production";

    public static readonly Guid UatId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000501");
    public static readonly Guid ProductionId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000502");
}

public sealed class CatalogEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<CatalogService> Services { get; set; } = new List<CatalogService>();
}

public sealed class CatalogEnvironment
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int DisplayOrder { get; set; }
    public ICollection<ServiceEnvironmentConfig> ServiceConfigurations { get; set; } = new List<ServiceEnvironmentConfig>();
}

public sealed class CatalogService
{
    public Guid Id { get; set; }
    public Guid DefinitionKey { get; set; }
    public Guid EntityId { get; set; }
    public CatalogEntity? Entity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public DateTimeOffset? FirstUsedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<ServiceEnvironmentConfig> EnvironmentConfigs { get; set; } = new List<ServiceEnvironmentConfig>();
    public ICollection<ServiceFieldDefinition> Fields { get; set; } = new List<ServiceFieldDefinition>();
    public ICollection<ResultMappingDefinition> ResultMappings { get; set; } = new List<ResultMappingDefinition>();
}

public sealed class ServiceEnvironmentConfig
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public CatalogService? Service { get; set; }
    public Guid EnvironmentId { get; set; }
    public CatalogEnvironment? Environment { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string ContentType { get; set; } = "application/json";
    public string NonSecretHeadersJson { get; set; } = "{}";
    public int TimeoutSeconds { get; set; } = 30;
    public string TlsPolicy { get; set; } = "SystemDefault";
    public bool ValidateServerCertificate { get; set; } = true;
    public string ProxyUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = string.Empty;
    public string HealthMethod { get; set; } = "HEAD";
    public bool Active { get; set; } = true;
    public DateTimeOffset? LastTestedAtUtc { get; set; }
    public string LastTestStatus { get; set; } = string.Empty;
    public Guid? AuthProfileId { get; set; }
}

public sealed class ServiceFieldDefinition
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public CatalogService? Service { get; set; }
    public string Key { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
    public string Regex { get; set; } = string.Empty;
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public int DisplayOrder { get; set; }
    public bool Sensitive { get; set; }
    public string Masking { get; set; } = "None";
}

public sealed class ResultMappingDefinition
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public CatalogService? Service { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string ResultType { get; set; } = "text";
    public string Formatter { get; set; } = string.Empty;
    public bool Sensitive { get; set; }
    public int DisplayOrder { get; set; }
}
