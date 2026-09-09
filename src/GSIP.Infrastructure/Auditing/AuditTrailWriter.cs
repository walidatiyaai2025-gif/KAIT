using System.Data.Common;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Auditing;

public sealed class AuditTrailWriter(
    GsipDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ISystemClock clock,
    IOptions<AuditTrailOptions> options) : IAuditTrailWriter
{
    private const string GenesisHash = "GENESIS";
    private readonly AuditTrailOptions _options = options.Value;

    public async Task<AuditTrailEntry> WriteAsync(AuditTrailEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ValidateRequired(auditEvent.Action, nameof(auditEvent.Action));
        ValidateRequired(auditEvent.TargetType, nameof(auditEvent.TargetType));
        ValidateRequired(auditEvent.Outcome, nameof(auditEvent.Outcome));

        var ownsTransaction = db.Database.CurrentTransaction is null;
        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (ownsTransaction)
                ownedTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var transaction = db.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Audit append requires a database transaction.");

            await AcquireChainLockAsync(cancellationToken);
            var state = await ReadChainStateAsync(transaction, forUpdate: true, cancellationToken);
            var sequence = checked(state.LastSequence + 1);
            var previousHash = state.LastSequence == 0 ? GenesisHash : state.LastHash;
            var context = httpContextAccessor.HttpContext;
            var occurredAt = clock.UtcNow;
            var retentionDays = Math.Clamp(_options.RetentionDays, 1, 3650);
            var metadataJson = AuditTrailSanitizer.SanitizeMetadata(auditEvent.Metadata, _options.MaxMetadataBytes);

            var entity = new AuthenticationAuditEvent
            {
                Id = Guid.NewGuid(),
                UserId = auditEvent.ActorUserId,
                EventType = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.Action), 64),
                Succeeded = auditEvent.Succeeded,
                ResultCode = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.Outcome), 64),
                CorrelationId = Truncate(AuditTrailSanitizer.SanitizeText(
                    auditEvent.CorrelationId ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N")), 100),
                OccurredAtUtc = occurredAt,
                SequenceNumber = sequence,
                TargetType = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.TargetType), 80),
                TargetId = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.TargetId), 160),
                RequestId = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.RequestId), 100),
                EntityCode = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.EntityCode), 40),
                ServiceCode = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.ServiceCode), 120),
                Source = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.Source ?? BuildSource(context)), 120),
                Device = Truncate(AuditTrailSanitizer.SanitizeText(auditEvent.Device ?? BuildDevice(context)), 160),
                MetadataJson = metadataJson,
                PreviousHash = previousHash,
                RetainUntilUtc = occurredAt.AddDays(retentionDays)
            };
            entity.RecordHash = AuditIntegrityHash.Compute(entity);

            db.AuthenticationAuditEvents.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            await UpdateChainTailAsync(transaction, sequence, entity.RecordHash, cancellationToken);

            if (ownsTransaction && ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);

            return Map(entity);
        }
        catch
        {
            if (ownsTransaction && ownedTransaction is not null)
            {
                try { await ownedTransaction.RollbackAsync(cancellationToken); } catch { }
            }
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    internal static AuditTrailEntry Map(AuthenticationAuditEvent entity) => new(
        entity.Id,
        entity.SequenceNumber ?? 0,
        entity.UserId,
        entity.EventType,
        entity.TargetType ?? string.Empty,
        entity.TargetId,
        entity.OccurredAtUtc,
        entity.Succeeded,
        entity.ResultCode,
        entity.CorrelationId,
        entity.RequestId,
        entity.EntityCode,
        entity.ServiceCode,
        entity.Source,
        entity.Device,
        entity.MetadataJson ?? "{}",
        entity.PreviousHash ?? string.Empty,
        entity.RecordHash ?? string.Empty);

    internal async Task<ChainStateSnapshot> ReadChainStateAsync(
        IDbContextTransaction transaction,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = forUpdate
            ? "SELECT LastSequence, LastHash, CheckpointSequence, CheckpointHash, LastVerifiedAtUtc, LastIntegrityStatus FROM AuditChainState WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1;"
            : "SELECT LastSequence, LastHash, CheckpointSequence, CheckpointHash, LastVerifiedAtUtc, LastIntegrityStatus FROM AuditChainState WHERE Id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Audit chain state is missing.");

        return new ChainStateSnapshot(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5));
    }

    internal async Task UpdateChainTailAsync(
        IDbContextTransaction transaction,
        long sequence,
        string recordHash,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "UPDATE AuditChainState SET LastSequence = @sequence, LastHash = @hash, LastIntegrityStatus = N'Unverified' WHERE Id = 1;";
        AddParameter(command, "@sequence", sequence);
        AddParameter(command, "@hash", recordHash);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Audit chain tail update failed.");
    }

    internal async Task AcquireChainLockAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = N'GSIP.P11.AuditChain',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 15000;
            IF @lockResult < 0 THROW 51012, 'Unable to acquire audit-chain lock.', 1;
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    internal static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void ValidateRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Audit fact is required.", name);
    }

    private static string BuildSource(HttpContext? context) => context is null
        ? "System"
        : $"{context.Request.Method} {context.Request.Path}";

    private static string BuildDevice(HttpContext? context) =>
        context?.Request.Headers["User-Agent"].ToString() ?? string.Empty;

    private static string Truncate(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maximumLength ? value : value[..maximumLength];
}

public sealed record ChainStateSnapshot(
    long LastSequence,
    string LastHash,
    long CheckpointSequence,
    string CheckpointHash,
    DateTimeOffset? LastVerifiedAtUtc,
    string LastIntegrityStatus);
