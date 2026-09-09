using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Auditing;

public sealed class AuditTrailService(
    GsipDbContext db,
    IGsipPermissionEvaluator permissions,
    AuditTrailWriter writer,
    ISystemClock clock,
    IOptions<AuditTrailOptions> options) : IAuditTrailService
{
    private const string GenesisHash = "GENESIS";
    private readonly AuditTrailOptions _options = options.Value;

    public async Task<AuditTrailPage> QueryAsync(
        ClaimsPrincipal principal,
        AuditTrailFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(principal, GsipPermissions.AuditView, cancellationToken);
        var canViewSensitive = await permissions.HasPermissionAsync(principal, GsipPermissions.AuditViewSensitive, cancellationToken);
        var query = ApplyFilter(CanonicalQuery(), filter);
        var page = Math.Clamp(filter.Page, 1, 100_000);
        var pageSize = Math.Clamp(filter.PageSize, 1, Math.Clamp(_options.MaxQueryPageSize, 1, 500));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new AuditTrailPage(rows.Select(row => Map(row, canViewSensitive)).ToArray(), page, pageSize, total);
    }

    public async Task<AuditTrailEntry?> GetAsync(
        ClaimsPrincipal principal,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(principal, GsipPermissions.AuditView, cancellationToken);
        var row = await CanonicalQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
            return null;

        var canViewSensitive = await permissions.HasPermissionAsync(principal, GsipPermissions.AuditViewSensitive, cancellationToken);
        if (canViewSensitive)
        {
            await writer.WriteAsync(new AuditTrailEvent(
                ReadActorId(principal),
                "Audit.ViewSensitive",
                "AuditEvent",
                id.ToString("D"),
                true,
                "Viewed",
                Metadata: new Dictionary<string, object?> { ["sequence"] = row.SequenceNumber }), cancellationToken);
        }
        return Map(row, canViewSensitive);
    }

    public async Task<AuditExport> ExportAsync(
        ClaimsPrincipal principal,
        AuditTrailFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(principal, GsipPermissions.AuditView, cancellationToken);
        await RequireAsync(principal, GsipPermissions.AuditExport, cancellationToken);
        var canViewSensitive = await permissions.HasPermissionAsync(principal, GsipPermissions.AuditViewSensitive, cancellationToken);
        var maxRows = Math.Clamp(_options.MaxExportRows, 1, 25_000);
        var rows = await ApplyFilter(CanonicalQuery(), filter)
            .OrderByDescending(item => item.SequenceNumber)
            .Take(maxRows + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (rows.Count > maxRows)
            throw new InvalidOperationException("The audit export exceeds the configured row bound; narrow the filters.");

        var builder = new StringBuilder();
        builder.AppendLine("Sequence,TimestampUtc,ActorUserId,Action,TargetType,TargetId,Outcome,CorrelationId,RequestId,EntityCode,ServiceCode,Source,Device,Metadata");
        foreach (var row in rows)
        {
            var mapped = Map(row, canViewSensitive);
            builder.Append(mapped.SequenceNumber.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(mapped.OccurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(mapped.ActorUserId?.ToString("D") ?? string.Empty)).Append(',')
                .Append(Csv(mapped.Action)).Append(',')
                .Append(Csv(mapped.TargetType)).Append(',')
                .Append(Csv(mapped.TargetId ?? string.Empty)).Append(',')
                .Append(Csv(mapped.Outcome)).Append(',')
                .Append(Csv(mapped.CorrelationId)).Append(',')
                .Append(Csv(mapped.RequestId ?? string.Empty)).Append(',')
                .Append(Csv(mapped.EntityCode ?? string.Empty)).Append(',')
                .Append(Csv(mapped.ServiceCode ?? string.Empty)).Append(',')
                .Append(Csv(mapped.Source ?? string.Empty)).Append(',')
                .Append(Csv(mapped.Device ?? string.Empty)).Append(',')
                .Append(Csv(mapped.MetadataJson)).AppendLine();
        }

        await writer.WriteAsync(new AuditTrailEvent(
            ReadActorId(principal),
            "Audit.Export",
            "AuditTrail",
            null,
            true,
            "Exported",
            Metadata: new Dictionary<string, object?>
            {
                ["recordCount"] = rows.Count,
                ["fromUtc"] = filter.FromUtc,
                ["toUtc"] = filter.ToUtc,
                ["entityCode"] = filter.EntityCode,
                ["serviceCode"] = filter.ServiceCode
            }), cancellationToken);

        return new AuditExport(
            $"gsip-audit-{clock.UtcNow:yyyyMMdd-HHmmss}.csv",
            "text/csv; charset=utf-8",
            new UTF8Encoding(true).GetBytes(builder.ToString()),
            rows.Count);
    }

    public async Task<AuditIntegrityReport> VerifyIntegrityAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(principal, GsipPermissions.DiagnosticsRun, cancellationToken);
        return await VerifyIntegrityCoreAsync(updateState: true, cancellationToken);
    }

    public async Task<AuditMonitoringSnapshot> GetMonitoringSnapshotAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(principal, GsipPermissions.AuditView, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var state = await writer.ReadChainStateAsync(transaction, forUpdate: false, cancellationToken);
        var canonical = CanonicalQuery();
        var retained = await canonical.CountAsync(cancellationToken);
        var legacy = await db.AuthenticationAuditEvents.CountAsync(item => item.SequenceNumber == null, cancellationToken);
        var failed = await canonical.CountAsync(item => !item.Succeeded && item.OccurredAtUtc >= clock.UtcNow.AddHours(-24), cancellationToken);
        var lastAudit = await canonical.MaxAsync(item => (DateTimeOffset?)item.OccurredAtUtc, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AuditMonitoringSnapshot(
            state.LastIntegrityStatus,
            state.LastVerifiedAtUtc,
            lastAudit,
            state.LastSequence,
            state.CheckpointSequence,
            retained,
            legacy,
            failed);
    }

    public async Task<int> PurgeExpiredPrefixAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(_options.PurgeBatchSize, 1, 5000);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await writer.AcquireChainLockAsync(cancellationToken);
        var state = await writer.ReadChainStateAsync(transaction, forUpdate: true, cancellationToken);
        var candidates = await CanonicalQuery()
            .Where(item => item.SequenceNumber > state.CheckpointSequence)
            .OrderBy(item => item.SequenceNumber)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var expectedSequence = state.CheckpointSequence + 1;
        var expectedPreviousHash = state.CheckpointSequence == 0 ? GenesisHash : state.CheckpointHash;
        AuthenticationAuditEvent? lastPurgeable = null;
        foreach (var candidate in candidates)
        {
            if (candidate.SequenceNumber != expectedSequence
                || !string.Equals(candidate.PreviousHash, expectedPreviousHash, StringComparison.Ordinal)
                || !string.Equals(candidate.RecordHash, AuditIntegrityHash.Compute(candidate), StringComparison.Ordinal))
                throw new InvalidOperationException("Audit retention refused to operate on a broken integrity chain.");

            if (candidate.RetainUntilUtc is null || candidate.RetainUntilUtc > clock.UtcNow)
                break;

            lastPurgeable = candidate;
            expectedPreviousHash = candidate.RecordHash!;
            expectedSequence++;
        }

        if (lastPurgeable is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var deleted = await DeleteCanonicalPrefixAsync(transaction, lastPurgeable.SequenceNumber!.Value, cancellationToken);
        await UpdateCheckpointAsync(transaction, lastPurgeable.SequenceNumber.Value, lastPurgeable.RecordHash!, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    internal async Task<AuditIntegrityReport> VerifyIntegrityCoreAsync(bool updateState, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await writer.AcquireChainLockAsync(cancellationToken);
        var state = await writer.ReadChainStateAsync(transaction, forUpdate: updateState, cancellationToken);
        var rows = await CanonicalQuery().OrderBy(item => item.SequenceNumber).AsNoTracking().ToListAsync(cancellationToken);
        var legacyCount = await db.AuthenticationAuditEvents.CountAsync(item => item.SequenceNumber == null, cancellationToken);

        var expectedSequence = state.CheckpointSequence + 1;
        var expectedPreviousHash = state.CheckpointSequence == 0 ? GenesisHash : state.CheckpointHash;
        long? brokenAt = null;
        foreach (var row in rows)
        {
            if (row.SequenceNumber != expectedSequence
                || !string.Equals(row.PreviousHash, expectedPreviousHash, StringComparison.Ordinal)
                || !string.Equals(row.RecordHash, AuditIntegrityHash.Compute(row), StringComparison.Ordinal))
            {
                brokenAt = row.SequenceNumber ?? expectedSequence;
                break;
            }
            expectedPreviousHash = row.RecordHash!;
            expectedSequence++;
        }

        var observedLastSequence = rows.Count == 0 ? state.CheckpointSequence : rows[^1].SequenceNumber!.Value;
        var observedLastHash = rows.Count == 0 ? state.CheckpointHash : rows[^1].RecordHash!;
        if (brokenAt is null && (observedLastSequence != state.LastSequence || !string.Equals(observedLastHash, state.LastHash, StringComparison.Ordinal)))
            brokenAt = state.LastSequence;

        var healthy = brokenAt is null;
        var status = healthy ? "Healthy" : "Tampered";
        var verifiedAt = clock.UtcNow;
        if (updateState)
            await UpdateVerificationStateAsync(transaction, verifiedAt, status, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AuditIntegrityReport(
            healthy,
            status,
            state.CheckpointSequence,
            state.LastSequence,
            rows.Count,
            legacyCount,
            brokenAt,
            verifiedAt);
    }

    private IQueryable<AuthenticationAuditEvent> CanonicalQuery() =>
        db.AuthenticationAuditEvents.Where(item => item.SequenceNumber != null);

    private static IQueryable<AuthenticationAuditEvent> ApplyFilter(
        IQueryable<AuthenticationAuditEvent> query,
        AuditTrailFilter filter)
    {
        if (filter.FromUtc is not null) query = query.Where(item => item.OccurredAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc is not null) query = query.Where(item => item.OccurredAtUtc <= filter.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(filter.EntityCode)) query = query.Where(item => item.EntityCode == filter.EntityCode);
        if (!string.IsNullOrWhiteSpace(filter.ServiceCode)) query = query.Where(item => item.ServiceCode == filter.ServiceCode);
        if (filter.ActorUserId is not null) query = query.Where(item => item.UserId == filter.ActorUserId);
        if (filter.Succeeded is not null) query = query.Where(item => item.Succeeded == filter.Succeeded.Value);
        if (!string.IsNullOrWhiteSpace(filter.Action)) query = query.Where(item => item.EventType == filter.Action);
        return query;
    }

    private static AuditTrailEntry Map(AuthenticationAuditEvent row, bool includeMetadata)
    {
        var mapped = AuditTrailWriter.Map(row);
        return includeMetadata ? mapped : mapped with { MetadataJson = "{}" };
    }

    private async Task RequireAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken)
    {
        if (!await permissions.HasPermissionAsync(principal, permission, cancellationToken))
            throw new AuditTrailAccessDeniedException();
    }

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private async Task<int> DeleteCanonicalPrefixAsync(
        IDbContextTransaction transaction,
        long throughSequence,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            EXEC sys.sp_set_session_context @key=N'GSIP_AUDIT_RETENTION', @value=1;
            BEGIN TRY
                DELETE FROM AuthenticationAuditEvents WHERE SequenceNumber IS NOT NULL AND SequenceNumber <= @throughSequence;
                DECLARE @deleted int = @@ROWCOUNT;
                EXEC sys.sp_set_session_context @key=N'GSIP_AUDIT_RETENTION', @value=NULL;
                SELECT @deleted;
            END TRY
            BEGIN CATCH
                EXEC sys.sp_set_session_context @key=N'GSIP_AUDIT_RETENTION', @value=NULL;
                THROW;
            END CATCH
            """;
        AuditTrailWriter.AddParameter(command, "@throughSequence", throughSequence);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private async Task UpdateCheckpointAsync(
        IDbContextTransaction transaction,
        long sequence,
        string hash,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "UPDATE AuditChainState SET CheckpointSequence=@sequence, CheckpointHash=@hash, LastIntegrityStatus=N'Unverified' WHERE Id=1;";
        AuditTrailWriter.AddParameter(command, "@sequence", sequence);
        AuditTrailWriter.AddParameter(command, "@hash", hash);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Audit checkpoint update failed.");
    }

    private async Task UpdateVerificationStateAsync(
        IDbContextTransaction transaction,
        DateTimeOffset verifiedAt,
        string status,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "UPDATE AuditChainState SET LastVerifiedAtUtc=@verifiedAt, LastIntegrityStatus=@status WHERE Id=1;";
        AuditTrailWriter.AddParameter(command, "@verifiedAt", verifiedAt);
        AuditTrailWriter.AddParameter(command, "@status", status);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Audit verification state update failed.");
    }

    private static string Csv(string value)
    {
        var safe = PreventSpreadsheetFormula(value);
        return '"' + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private static string PreventSpreadsheetFormula(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }
}
