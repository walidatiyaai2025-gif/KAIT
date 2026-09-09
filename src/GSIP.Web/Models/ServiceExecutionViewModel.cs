using GSIP.Application.Execution;

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
    public IReadOnlyDictionary<string, string?> SubmittedValues { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, string> ValidationErrors { get; init; } = new Dictionary<string, string>();
    public ServiceExecutionResult? ExecutionResult { get; init; }
}

public sealed class ServiceExecutionRunRequest
{
    public Guid EntityId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Dictionary<string, string?> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
