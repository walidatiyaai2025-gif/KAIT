namespace GSIP.Web.Models;

public sealed class ServiceExecutionViewModel
{
    public required IReadOnlyList<ExecutionOptionViewModel> Entities { get; init; }
    public required IReadOnlyList<ExecutionServiceOptionViewModel> Services { get; init; }
    public required IReadOnlyList<ExecutionOptionViewModel> Environments { get; init; }
    public Guid? SelectedEntityId { get; init; }
    public Guid? SelectedServiceId { get; init; }
    public Guid? SelectedEnvironmentId { get; init; }
    public ExecutionServiceDetailsViewModel? SelectedService { get; init; }
}

public sealed record ExecutionOptionViewModel(Guid Id, string Code, string Label);

public sealed record ExecutionServiceOptionViewModel(Guid Id, Guid EntityId, string Code, string Label);

public sealed record ExecutionFieldViewModel(
    string Key,
    string Label,
    string FieldType,
    bool Required,
    int? MinLength,
    int? MaxLength,
    decimal? Minimum,
    decimal? Maximum,
    string Regex,
    bool Sensitive);

public sealed record ExecutionServiceDetailsViewModel(
    string EntityName,
    string ServiceName,
    string Description,
    string EnvironmentCode,
    string Method,
    string RelativePath,
    string ContentType,
    int TimeoutSeconds,
    bool HasAuthProfile,
    IReadOnlyList<ExecutionFieldViewModel> Fields);
