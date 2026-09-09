using System.Security.Claims;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Infrastructure.Auditing;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var connectionString = Environment.GetEnvironmentVariable("GSIP_P11_SQL");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("GSIP_P11_SQL is required for the P11 LocalDB integration gate.");

var dbOptions = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString).Options;
var clock = new MutableClock(new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero));
var auditOptions = Options.Create(new AuditTrailOptions
{
    RetentionDays = 1,
    MaxMetadataBytes = 4096,
    MaxQueryPageSize = 100,
    MaxExportRows = 1000,
    PurgeBatchSize = 100
});
var actorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
var principal = Principal(actorId);
var permissions = new FakePermissionEvaluator(actorId,
    GsipPermissions.AuditView,
    GsipPermissions.AuditViewSensitive,
    GsipPermissions.AuditExport,
    GsipPermissions.DiagnosticsRun);

await ResetDatabaseAsync(dbOptions, actorId);

await using (var db = new GsipDbContext(dbOptions))
{
    var applied = await db.Database.GetAppliedMigrationsAsync();
    Assert(applied.Contains("20260909213000_AuditTrailTamperEvidence", StringComparer.Ordinal),
        "P11 tamper-evidence migration was not discovered/applied.");

    var writer = Writer(db, clock, auditOptions);
    await writer.WriteAsync(Event(actorId, "Identity.Login", "Account", "A", true, "Succeeded"));
    await writer.WriteAsync(Event(actorId, "AuthProfile.RotateSecret", "AuthProfile", "B", true, "Rotated",
        new Dictionary<string, object?>
        {
            ["password"] = "SYNTH-PASSWORD-ONLY",
            ["civilId"] = "SYNTH-CIVIL-ONLY",
            ["serviceCode"] = "SVC-SYNTHETIC"
        }));

    var service = new AuditTrailService(db, permissions, writer, clock, auditOptions);
    var integrity = await service.VerifyIntegrityAsync(principal);
    Assert(integrity.IsHealthy && integrity.LastSequence == 2 && integrity.VerifiedRecordCount == 2,
        "Fresh canonical audit chain failed integrity verification.");

    var page = await service.QueryAsync(principal, new AuditTrailFilter(PageSize: 20));
    Assert(page.TotalCount == 2 && page.Items.All(item => item.RecordHash.Length == 64),
        "Canonical audit query did not return the verified records.");
    Assert(page.Items.All(item => !item.MetadataJson.Contains("SYNTH-PASSWORD-ONLY", StringComparison.Ordinal)
                                  && !item.MetadataJson.Contains("SYNTH-CIVIL-ONLY", StringComparison.Ordinal)),
        "Sensitive synthetic marker leaked through canonical audit query.");

    await AssertDatabaseMutationRejectedAsync(db,
        "UPDATE AuthenticationAuditEvents SET ResultCode=N'TAMPER' WHERE SequenceNumber=1;",
        "DB append-only trigger allowed UPDATE of canonical audit record.");
    await AssertDatabaseMutationRejectedAsync(db,
        "DELETE FROM AuthenticationAuditEvents WHERE SequenceNumber=2;",
        "DB append-only trigger allowed DELETE of canonical audit record.");

    var monitoring = await service.GetMonitoringSnapshotAsync(principal);
    Assert(monitoring.IntegrityStatus == "Healthy" && monitoring.RetainedCanonicalRecords == 2,
        "Monitoring snapshot did not expose verified healthy state.");
}

await ResetDatabaseAsync(dbOptions, actorId);

var concurrentWrites = Enumerable.Range(0, 4).Select(async index =>
{
    await using var db = new GsipDbContext(dbOptions);
    var writer = Writer(db, clock, auditOptions);
    await writer.WriteAsync(Event(actorId, $"Concurrent.{index}", "Synthetic", index.ToString(), true, "Succeeded"));
}).ToArray();
await Task.WhenAll(concurrentWrites);

await using (var db = new GsipDbContext(dbOptions))
{
    var rows = await db.AuthenticationAuditEvents.Where(item => item.SequenceNumber != null)
        .OrderBy(item => item.SequenceNumber).AsNoTracking().ToListAsync();
    Assert(rows.Select(item => item.SequenceNumber!.Value).SequenceEqual(new long[] { 1, 2, 3, 4 }),
        "Concurrent canonical audit writers created a sequence gap or duplicate.");
    for (var index = 1; index < rows.Count; index++)
        Assert(rows[index].PreviousHash == rows[index - 1].RecordHash, "Concurrent append broke hash-chain linkage.");

    var writer = Writer(db, clock, auditOptions);
    var service = new AuditTrailService(db, permissions, writer, clock, auditOptions);
    Assert((await service.VerifyIntegrityAsync(principal)).IsHealthy, "Concurrent canonical chain did not verify healthy.");

    clock.UtcNow = clock.UtcNow.AddDays(2);
    var purged = await service.PurgeExpiredPrefixAsync();
    Assert(purged == 4, "Chain-safe retention did not purge the complete expired prefix.");
    var afterPurge = await service.VerifyIntegrityAsync(principal);
    Assert(afterPurge.IsHealthy && afterPurge.CheckpointSequence == 4 && afterPurge.LastSequence == 4 && afterPurge.VerifiedRecordCount == 0,
        "Retention checkpoint did not preserve verifiable chain state after prefix purge.");
}

await ResetDatabaseAsync(dbOptions, actorId);
clock.UtcNow = new DateTimeOffset(2026, 9, 9, 19, 0, 0, TimeSpan.Zero);

await using (var db = new GsipDbContext(dbOptions))
{
    var writer = Writer(db, clock, auditOptions);
    await writer.WriteAsync(Event(actorId, "Tail.One", "Synthetic", "1", true, "Succeeded"));
    await writer.WriteAsync(Event(actorId, "Tail.Two", "Synthetic", "2", true, "Succeeded"));

    await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER [dbo].[TR_AuthenticationAuditEvents_AppendOnly] ON [dbo].[AuthenticationAuditEvents];");
    try
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AuthenticationAuditEvents WHERE SequenceNumber=2;");
    }
    finally
    {
        await db.Database.ExecuteSqlRawAsync("ENABLE TRIGGER [dbo].[TR_AuthenticationAuditEvents_AppendOnly] ON [dbo].[AuthenticationAuditEvents];");
    }

    var service = new AuditTrailService(db, permissions, writer, clock, auditOptions);
    var tampered = await service.VerifyIntegrityAsync(principal);
    Assert(!tampered.IsHealthy && tampered.Status == "Tampered" && tampered.FirstBrokenSequence is not null,
        "Tail deletion was not detected against AuditChainState tail evidence.");
}

await ResetDatabaseAsync(dbOptions, actorId);
clock.UtcNow = new DateTimeOffset(2026, 9, 9, 20, 0, 0, TimeSpan.Zero);

await using (var db = new GsipDbContext(dbOptions))
{
    var writer = Writer(db, clock, auditOptions);
    await writer.WriteAsync(Event(actorId, "Mutation.One", "Synthetic", "1", true, "Succeeded"));
    await writer.WriteAsync(Event(actorId, "Mutation.Two", "Synthetic", "2", true, "Succeeded"));

    await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER [dbo].[TR_AuthenticationAuditEvents_AppendOnly] ON [dbo].[AuthenticationAuditEvents];");
    try
    {
        await db.Database.ExecuteSqlRawAsync("UPDATE AuthenticationAuditEvents SET ResultCode=N'TamperedOutcome' WHERE SequenceNumber=1;");
    }
    finally
    {
        await db.Database.ExecuteSqlRawAsync("ENABLE TRIGGER [dbo].[TR_AuthenticationAuditEvents_AppendOnly] ON [dbo].[AuthenticationAuditEvents];");
    }

    var service = new AuditTrailService(db, permissions, writer, clock, auditOptions);
    var tampered = await service.VerifyIntegrityAsync(principal);
    Assert(!tampered.IsHealthy && tampered.FirstBrokenSequence == 1,
        "Full-record field mutation was not detected by integrity verification.");
}

await ResetDatabaseAsync(dbOptions, actorId);
Console.WriteLine("P11_LOCALDB_MIGRATION_DISCOVERY=PASS");
Console.WriteLine("P11_APPEND_ONLY_DB_TRIGGER=PASS");
Console.WriteLine("P11_CONCURRENT_HASH_CHAIN_SERIALIZATION=PASS");
Console.WriteLine("P11_CHAIN_SAFE_RETENTION_CHECKPOINT=PASS");
Console.WriteLine("P11_TAIL_DELETION_TAMPER_DETECTION=PASS");
Console.WriteLine("P11_FIELD_MUTATION_TAMPER_DETECTION=PASS");
Console.WriteLine("P11_MONITORING_HEALTH_EVIDENCE=PASS");

static AuditTrailWriter Writer(GsipDbContext db, ISystemClock clock, IOptions<AuditTrailOptions> options) =>
    new(db, new HttpContextAccessor(), clock, options);

static AuditTrailEvent Event(
    Guid actorId,
    string action,
    string targetType,
    string targetId,
    bool succeeded,
    string outcome,
    IReadOnlyDictionary<string, object?>? metadata = null) => new(
        actorId,
        action,
        targetType,
        targetId,
        succeeded,
        outcome,
        CorrelationId: "CORR-SYNTHETIC",
        RequestId: "REQ-SYNTHETIC",
        EntityCode: "MOJ-SYNTHETIC",
        ServiceCode: "SVC-SYNTHETIC",
        Source: "P11Acceptance",
        Device: "SyntheticDevice",
        Metadata: metadata);

static ClaimsPrincipal Principal(Guid actorId) => new(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")) }, "P11Synthetic"));

static async Task ResetDatabaseAsync(DbContextOptions<GsipDbContext> options, Guid actorId)
{
    await using var db = new GsipDbContext(options);
    await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();
    db.Users.Add(new ApplicationUser
    {
        Id = actorId,
        UserName = "p11.synthetic",
        NormalizedUserName = "P11.SYNTHETIC",
        Email = "p11.synthetic@example.invalid",
        NormalizedEmail = "P11.SYNTHETIC@EXAMPLE.INVALID",
        EmailConfirmed = true,
        DisplayName = "P11 Synthetic Actor",
        IsEnabled = true,
        MustChangePassword = false,
        CreatedAtUtc = new DateTimeOffset(2026, 9, 9, 17, 0, 0, TimeSpan.Zero),
        SecurityStamp = "P11-SYNTHETIC-SECURITY-STAMP",
        ConcurrencyStamp = "P11-SYNTHETIC-CONCURRENCY-STAMP"
    });
    await db.SaveChangesAsync();
}

static async Task AssertDatabaseMutationRejectedAsync(GsipDbContext db, string sql, string message)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync(sql);
        throw new InvalidOperationException(message);
    }
    catch (InvalidOperationException exception) when (exception.Message == message)
    {
        throw;
    }
    catch
    {
        // Expected database-side append-only rejection.
    }
}

sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

sealed class FakePermissionEvaluator(Guid actorId, params string[] permissions) : IGsipPermissionEvaluator
{
    private readonly HashSet<string> _permissions = new(permissions, StringComparer.Ordinal);

    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default)
    {
        var allowed = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            && id == actorId
            && _permissions.Contains(permission);
        return Task.FromResult(allowed);
    }

    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
