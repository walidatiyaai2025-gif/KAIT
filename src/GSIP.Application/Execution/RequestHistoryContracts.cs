using System.Security.Claims;

namespace GSIP.Application.Execution;

public enum RequestHistoryScope
{
    Own = 0,
    Department = 1,
    All = 2
}

public enum RequestHistoryExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2,
    Print = 3
}

public sealed record RequestHistoryFilter(
    RequestHistoryScope Scope = RequestHistoryScope.Own,
    string? Search = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    RequestLifecycleStatus? Status = null,
    string? EntityCode = null,
    string? ServiceCode = null,
    int Page = 1,
    int PageSize = 25);

public sealed record RequestHistoryItem(
    string RequestId,
    string CorrelationId,
    Guid ActorUserId,
    string? DepartmentCode,
    string EntityCode,
    string ServiceCode,
    int ServiceVersion,
    string EnvironmentCode,
    string MaskedInputJson,
    RequestLifecycleStatus Status,
    string OutcomeCode,
    int? HttpStatusCode,
    long DurationMilliseconds,
    int Attempts,
    bool ResultSuppressedByBound,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record RequestHistoryDetail(
    RequestHistoryItem Summary,
    string? StructuredResultJson,
    string? RawResponse);

public sealed record RequestHistoryPage(
    IReadOnlyList<RequestHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RequestHistoryExport(
    byte[] Content,
    string ContentType,
    string FileName,
    int RecordCount);

public interface IRequestHistoryService
{
    Task<RequestHistoryPage> QueryAsync(
        ClaimsPrincipal principal,
        RequestHistoryFilter filter,
        CancellationToken cancellationToken = default);

    Task<RequestHistoryDetail?> GetAsync(
        ClaimsPrincipal principal,
        string requestId,
        RequestHistoryScope scope = RequestHistoryScope.Own,
        CancellationToken cancellationToken = default);

    Task<RequestHistoryExport> ExportAsync(
        ClaimsPrincipal principal,
        RequestHistoryFilter filter,
        RequestHistoryExportFormat format,
        CancellationToken cancellationToken = default);

    Task<int> PurgeExpiredAsync(int batchSize = 500, CancellationToken cancellationToken = default);
}

public sealed class RequestHistoryAccessDeniedException : InvalidOperationException
{
    public RequestHistoryAccessDeniedException() : base("Request history access was denied.") { }
}

public sealed class RequestHistoryExportLimitException : InvalidOperationException
{
    public RequestHistoryExportLimitException() : base("Request history export exceeds the configured safe bound.") { }
}
