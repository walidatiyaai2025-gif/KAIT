using System.Security.Claims;

namespace GSIP.Application.Operations;

public enum OperationalHealthState
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2,
    NotConfigured = 3
}

public sealed record OperationalDiagnostic(
    string Category,
    string Code,
    OperationalHealthState State,
    string CorrelationId,
    string MessageCode,
    DateTimeOffset CheckedAtUtc,
    long DurationMilliseconds,
    Guid? ServiceId = null,
    Guid? EnvironmentId = null,
    int? HttpStatusCode = null,
    long? ClockSkewSeconds = null);

public sealed record AdminHealthComponent(
    string Key,
    OperationalHealthState State,
    string Code,
    string MessageCode,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyDictionary<string, string> Facts);

public sealed record AdminServiceEnvironmentStatus(
    Guid ServiceId,
    string EntityCode,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    bool ServiceActive,
    Guid EnvironmentId,
    string EnvironmentCode,
    bool EnvironmentActive,
    bool ConfigurationActive,
    bool HealthProbeConfigured,
    bool AuthenticationConfigured,
    string LastTestStatus);

public sealed record AdminOperationalAlert(
    string Code,
    OperationalHealthState State,
    string MessageCode,
    string? ServiceCode = null,
    string? EnvironmentCode = null);

public sealed record AdminHealthSnapshot(
    string CorrelationId,
    DateTimeOffset CheckedAtUtc,
    OperationalHealthState OverallState,
    IReadOnlyList<AdminHealthComponent> Components,
    IReadOnlyList<AdminServiceEnvironmentStatus> Integrations,
    IReadOnlyList<AdminOperationalAlert> Alerts);

public sealed class AdminOperationsAccessDeniedException : Exception
{
    public AdminOperationsAccessDeniedException() : base("The requested administrative diagnostic is not permitted.") { }
}

public sealed class AdminOperationsTargetRejectedException : Exception
{
    public AdminOperationsTargetRejectedException() : base("The requested service/environment diagnostic target is invalid or unavailable.") { }
}

public interface IAdminOperationsService
{
    Task<AdminHealthSnapshot> GetHealthAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<OperationalDiagnostic> RunDatabaseDiagnosticAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<OperationalDiagnostic> RunIntegrationDiagnosticAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        CancellationToken cancellationToken = default);

    Task<OperationalDiagnostic> RunAuthenticationDiagnosticAsync(
        ClaimsPrincipal principal,
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        CancellationToken cancellationToken = default);
}

public sealed class AdminOperationsOptions
{
    public const string SectionName = "AdminOperations";
    public int IntegrationProbeTimeoutSeconds { get; init; } = 15;
    public int ClockSkewWarningSeconds { get; init; } = 120;
    public int DiskFreeWarningPercent { get; init; } = 15;
    public int RepeatedFailureAlertThreshold { get; init; } = 5;
}
