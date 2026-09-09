using System.Security.Claims;

namespace GSIP.Application.Execution;

public sealed record ServiceExecutionCommand(
    ClaimsPrincipal Principal,
    Guid ServiceId,
    Guid EnvironmentId,
    IReadOnlyDictionary<string, string?> Inputs);

public sealed record StructuredServiceResultItem(
    string SourcePath,
    string LabelAr,
    string LabelEn,
    string ResultType,
    string Value,
    bool Sensitive,
    int DisplayOrder);

public enum ServiceExecutionOutcome
{
    Success = 0,
    BadRequest = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5,
    RateLimited = 6,
    ServerError = 7,
    Timeout = 8,
    TlsFailure = 9,
    NetworkFailure = 10,
    AuthenticationUnavailable = 11,
    ResponseTooLarge = 12,
    InvalidResponse = 13
}

public sealed record ServiceExecutionResult(
    string RequestId,
    string CorrelationId,
    string ServiceCode,
    string EnvironmentCode,
    string EndpointAlias,
    int? StatusCode,
    long DurationMilliseconds,
    int Attempts,
    ServiceExecutionOutcome Outcome,
    string MessageCode,
    IReadOnlyList<StructuredServiceResultItem> StructuredResult,
    string RawResponse);

public sealed class ServiceExecutionValidationException : InvalidOperationException
{
    public const string SafeMessage = "Service execution input validation failed.";

    public ServiceExecutionValidationException(IReadOnlyDictionary<string, string> errors)
        : base(SafeMessage)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string> Errors { get; }
}

public interface IServiceExecutionEngine
{
    Task<ServiceExecutionResult> ExecuteAsync(
        ServiceExecutionCommand command,
        CancellationToken cancellationToken = default);
}

public interface IRequestHistoryStore
{
    Task<string> StartAsync(
        ServiceExecutionCommand command,
        AuthorizedServiceExecutionBinding binding,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(string trackingRequestId, ServiceExecutionResult result, CancellationToken cancellationToken = default);

    Task TerminateAsync(
        string trackingRequestId,
        RequestLifecycleStatus status,
        string outcomeCode,
        CancellationToken cancellationToken = default);
}

public enum RequestLifecycleStatus
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

public sealed class ServiceExecutionRuntimeOptions
{
    public const string SafeToRetryMetadataHeader = "X-GSIP-SafeToRetry";
    public int MaxAttempts { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 50;
    public int MaxRawResponseBytes { get; set; } = 1_048_576;
}
