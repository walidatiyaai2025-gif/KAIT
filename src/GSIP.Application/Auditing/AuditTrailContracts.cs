using System.Security.Claims;

namespace GSIP.Application.Auditing;

public sealed record AuditTrailEvent(
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string? TargetId,
    bool Succeeded,
    string Outcome,
    string? CorrelationId = null,
    string? RequestId = null,
    string? EntityCode = null,
    string? ServiceCode = null,
    string? Source = null,
    string? Device = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record AuditTrailFilter(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? EntityCode = null,
    string? ServiceCode = null,
    Guid? ActorUserId = null,
    bool? Succeeded = null,
    string? Action = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditTrailEntry(
    Guid Id,
    long SequenceNumber,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string? TargetId,
    DateTimeOffset OccurredAtUtc,
    bool Succeeded,
    string Outcome,
    string CorrelationId,
    string? RequestId,
    string? EntityCode,
    string? ServiceCode,
    string? Source,
    string? Device,
    string MetadataJson,
    string PreviousHash,
    string RecordHash);

public sealed record AuditTrailPage(
    IReadOnlyList<AuditTrailEntry> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AuditIntegrityReport(
    bool IsHealthy,
    string Status,
    long CheckpointSequence,
    long LastSequence,
    int VerifiedRecordCount,
    int LegacyRecordCount,
    long? FirstBrokenSequence,
    DateTimeOffset VerifiedAtUtc);

public sealed record AuditMonitoringSnapshot(
    string IntegrityStatus,
    DateTimeOffset? LastVerifiedAtUtc,
    DateTimeOffset? LastAuditAtUtc,
    long LastSequence,
    long CheckpointSequence,
    int RetainedCanonicalRecords,
    int LegacyRecords,
    int FailedEventsLast24Hours);

public sealed record AuditExport(
    string FileName,
    string ContentType,
    byte[] Content,
    int RecordCount);

public interface IAuditTrailWriter
{
    Task<AuditTrailEntry> WriteAsync(AuditTrailEvent auditEvent, CancellationToken cancellationToken = default);
}

public interface IAuditTrailService
{
    Task<AuditTrailPage> QueryAsync(ClaimsPrincipal principal, AuditTrailFilter filter, CancellationToken cancellationToken = default);
    Task<AuditTrailEntry?> GetAsync(ClaimsPrincipal principal, Guid id, CancellationToken cancellationToken = default);
    Task<AuditExport> ExportAsync(ClaimsPrincipal principal, AuditTrailFilter filter, CancellationToken cancellationToken = default);
    Task<AuditIntegrityReport> VerifyIntegrityAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<AuditMonitoringSnapshot> GetMonitoringSnapshotAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<int> PurgeExpiredPrefixAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditTrailOptions
{
    public const string SectionName = "AuditTrail";
    public int RetentionDays { get; init; } = 90;
    public int MaxMetadataBytes { get; init; } = 16 * 1024;
    public int MaxQueryPageSize { get; init; } = 100;
    public int MaxExportRows { get; init; } = 10_000;
    public int PurgeBatchSize { get; init; } = 500;
}

public sealed class AuditTrailAccessDeniedException : Exception
{
    public AuditTrailAccessDeniedException() : base("The requested audit operation is not permitted.") { }
}
