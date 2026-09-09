using System.Security.Claims;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var connectionString = Environment.GetEnvironmentVariable("GSIP_P10_SQL");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("GSIP_P10_SQL is required for the P10 LocalDB integration gate.");

var now = new DateTimeOffset(2026, 9, 9, 18, 30, 0, TimeSpan.Zero);
var clock = new FixedClock(now);
var viewerId = Guid.NewGuid();
var peerId = Guid.NewGuid();
var otherDepartmentId = Guid.NewGuid();
var ownOnlyId = Guid.NewGuid();
var emptyDepartmentId = Guid.NewGuid();
var entityId = Guid.NewGuid();
var serviceAId = Guid.NewGuid();
var serviceBId = Guid.NewGuid();
var environmentId = Guid.NewGuid();

var metadata = new FakeMetadataCatalog(entityId, serviceAId, serviceBId);
var permissions = new FakePermissionEvaluator();
permissions.GrantGlobal(viewerId, GsipPermissions.RequestsViewOwn, GsipPermissions.RequestsViewDepartment, GsipPermissions.RequestsViewAll, GsipPermissions.RequestsExport);
permissions.GrantGlobal(ownOnlyId, GsipPermissions.RequestsViewOwn);
permissions.GrantGlobal(emptyDepartmentId, GsipPermissions.RequestsViewDepartment);
permissions.GrantService(viewerId, "SVC-A", GsipPermissions.ServicesView);
permissions.GrantService(ownOnlyId, "SVC-A", GsipPermissions.ServicesView);
permissions.GrantService(emptyDepartmentId, "SVC-A", GsipPermissions.ServicesView);

var optionsBuilder = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString);
await using var db = new GsipDbContext(optionsBuilder.Options);
await db.Database.EnsureDeletedAsync();

var dataProtectionDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "gsip-p10-dp-" + Guid.NewGuid().ToString("N")));
try
{
    await db.Database.MigrateAsync();
    var applied = await db.Database.GetAppliedMigrationsAsync();
    Assert(applied.Contains("20260909183000_RequestHistoryFoundation", StringComparer.Ordinal),
        "P10 RequestHistoryFoundation migration was not discovered/applied by EF Core.");

    db.Users.AddRange(
        CreateUser(viewerId, "viewer.synthetic", "FIN"),
        CreateUser(peerId, "peer.synthetic", "FIN"),
        CreateUser(otherDepartmentId, "other.synthetic", "HR"),
        CreateUser(ownOnlyId, "own.synthetic", "FIN"),
        CreateUser(emptyDepartmentId, "empty.synthetic", null));
    await db.SaveChangesAsync();

    var viewerPrincipal = Principal(viewerId);
    var ownOnlyPrincipal = Principal(ownOnlyId);
    var emptyDepartmentPrincipal = Principal(emptyDepartmentId);

    db.Set<RequestExecutionRecord>().AddRange(
        Record("REQ-OWN-A", viewerId, "FIN", serviceAId, "SVC-A", entityId, environmentId, now.AddMinutes(-4), "Success"),
        Record("REQ-PEER-A", peerId, "FIN", serviceAId, "SVC-A", entityId, environmentId, now.AddMinutes(-3), "=SYNTHETIC_FORMULA"),
        Record("REQ-HR-A", otherDepartmentId, "HR", serviceAId, "SVC-A", entityId, environmentId, now.AddMinutes(-2), "Success"),
        Record("REQ-OWN-B", viewerId, "FIN", serviceBId, "SVC-B", entityId, environmentId, now.AddMinutes(-1), "Success"));
    await db.SaveChangesAsync();

    var history = new RequestHistoryService(
        db,
        permissions,
        metadata,
        DataProtectionProvider.Create(dataProtectionDirectory),
        clock,
        Options.Create(new RequestHistoryOptions
        {
            StoreStructuredResult = true,
            StoreRawResponse = false,
            RetentionDays = 30,
            MaxStoredPayloadBytes = 64 * 1024,
            MaxExportRows = 100,
            MaxPdfRows = 50
        }));

    var own = await history.QueryAsync(viewerPrincipal, new RequestHistoryFilter(RequestHistoryScope.Own));
    Assert(own.TotalCount == 1 && own.Items.Single().RequestId == "REQ-OWN-A",
        "Own scope leaked another actor or a service without Services.View.");

    var department = await history.QueryAsync(viewerPrincipal, new RequestHistoryFilter(RequestHistoryScope.Department));
    Assert(department.TotalCount == 2
           && department.Items.All(item => item.DepartmentCode == "FIN" && item.ServiceCode == "SVC-A"),
        "Department scope failed same-department/service isolation.");

    var all = await history.QueryAsync(viewerPrincipal, new RequestHistoryFilter(RequestHistoryScope.All));
    Assert(all.TotalCount == 3 && all.Items.All(item => item.ServiceCode == "SVC-A"),
        "All scope bypassed current Services.View filtering.");

    Assert(await history.GetAsync(viewerPrincipal, "REQ-HR-A", RequestHistoryScope.Own) is null,
        "Forged request identifier bypassed Own scope.");

    await AssertDeniedAsync(() => history.QueryAsync(ownOnlyPrincipal, new RequestHistoryFilter(RequestHistoryScope.Department)),
        "Own-only user obtained Department scope.");
    await AssertDeniedAsync(() => history.QueryAsync(emptyDepartmentPrincipal, new RequestHistoryFilter(RequestHistoryScope.Department)),
        "Empty-department user did not fail closed.");
    await AssertDeniedAsync(() => history.ExportAsync(ownOnlyPrincipal, new RequestHistoryFilter(RequestHistoryScope.Own), RequestHistoryExportFormat.Csv),
        "User without Requests.Export exported request history.");

    var export = await history.ExportAsync(viewerPrincipal, new RequestHistoryFilter(RequestHistoryScope.All), RequestHistoryExportFormat.Csv);
    var csv = Encoding.UTF8.GetString(export.Content);
    Assert(export.RecordCount == 3, "Export row count escaped authorized query scope.");
    Assert(csv.Contains("'=SYNTHETIC_FORMULA", StringComparison.Ordinal), "CSV formula-injection neutralization was not preserved end-to-end.");
    Assert(!csv.Contains("SYNTH-CIVIL-VALUE", StringComparison.Ordinal), "Sensitive synthetic input leaked into export.");
    Assert(await db.AuthenticationAuditEvents.CountAsync(item => item.EventType == "RequestHistory.Export" && item.UserId == viewerId) == 1,
        "Request-history export did not emit its audit event.");

    var concurrencyId = Guid.NewGuid();
    db.Set<RequestExecutionRecord>().Add(Record("REQ-CONCURRENCY", viewerId, "FIN", serviceAId, "SVC-A", entityId, environmentId, now, "Started", concurrencyId));
    await db.SaveChangesAsync();

    await using (var first = new GsipDbContext(optionsBuilder.Options))
    await using (var second = new GsipDbContext(optionsBuilder.Options))
    {
        var left = await first.Set<RequestExecutionRecord>().SingleAsync(item => item.Id == concurrencyId);
        var right = await second.Set<RequestExecutionRecord>().SingleAsync(item => item.Id == concurrencyId);
        left.OutcomeCode = "FirstTerminalWriter";
        right.OutcomeCode = "SecondTerminalWriter";
        await first.SaveChangesAsync();
        try
        {
            await second.SaveChangesAsync();
            throw new InvalidOperationException("RowVersion concurrency protection did not reject stale writer.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // Required stale-writer rejection.
        }
    }

    db.Set<RequestExecutionRecord>().AddRange(
        Record("REQ-EXPIRED", viewerId, "FIN", serviceAId, "SVC-A", entityId, environmentId, now.AddDays(-60), "Expired", retainUntil: now.AddDays(-1)),
        Record("REQ-STARTED-EXPIRED", viewerId, "FIN", serviceAId, "SVC-A", entityId, environmentId, now.AddDays(-60), "Started", lifecycle: RequestLifecycleStatus.Started, retainUntil: now.AddDays(-1)));
    await db.SaveChangesAsync();
    var purged = await history.PurgeExpiredAsync();
    Assert(purged == 1, "Retention purge must remove only bounded terminal expired rows.");
    Assert(await db.Set<RequestExecutionRecord>().AnyAsync(item => item.RequestId == "REQ-STARTED-EXPIRED"),
        "Retention purge removed an in-flight request.");

    Console.WriteLine("P10_LOCALDB_MIGRATION_DISCOVERY=PASS");
    Console.WriteLine("P10_SCOPE_ISOLATION_OWN_DEPARTMENT_ALL=PASS");
    Console.WriteLine("P10_SERVICE_VIEW_AND_IDOR=PASS");
    Console.WriteLine("P10_EXPORT_PERMISSION_MASKING_INJECTION_AUDIT=PASS");
    Console.WriteLine("P10_ROWVERSION_CONCURRENCY=PASS");
    Console.WriteLine("P10_RETENTION_SAFETY=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
    try { dataProtectionDirectory.Delete(true); } catch { }
}

static ApplicationUser CreateUser(Guid id, string name, string? department) => new()
{
    Id = id,
    DisplayName = name,
    DepartmentCode = department,
    UserName = name,
    NormalizedUserName = name.ToUpperInvariant(),
    Email = $"{name}@example.invalid",
    NormalizedEmail = $"{name}@example.invalid".ToUpperInvariant(),
    EmailConfirmed = true,
    IsEnabled = true,
    CreatedAtUtc = DateTimeOffset.UtcNow,
    SecurityStamp = Guid.NewGuid().ToString("N"),
    ConcurrencyStamp = Guid.NewGuid().ToString("N")
};

static RequestExecutionRecord Record(
    string requestId,
    Guid actorId,
    string? department,
    Guid serviceId,
    string serviceCode,
    Guid entityId,
    Guid environmentId,
    DateTimeOffset started,
    string outcome,
    Guid? id = null,
    RequestLifecycleStatus lifecycle = RequestLifecycleStatus.Succeeded,
    DateTimeOffset? retainUntil = null) => new()
{
    Id = id ?? Guid.NewGuid(),
    RequestId = requestId,
    CorrelationId = "CORR-" + requestId,
    ActorUserId = actorId,
    UserReferenceId = actorId,
    DepartmentCode = department,
    EntityId = entityId,
    EntityCode = "MOJ-SYNTHETIC",
    ServiceId = serviceId,
    ServiceCode = serviceCode,
    ServiceVersion = 1,
    EnvironmentId = environmentId,
    EnvironmentCode = "UAT-SYNTHETIC",
    MaskedInputJson = "{\"civilId\":\"[MASKED]\",\"note\":\"synthetic\"}",
    LifecycleStatus = lifecycle,
    OutcomeCode = outcome,
    HttpStatusCode = lifecycle == RequestLifecycleStatus.Succeeded ? 200 : null,
    DurationMilliseconds = 12,
    Attempts = 1,
    StartedAtUtc = started,
    CompletedAtUtc = lifecycle == RequestLifecycleStatus.Started ? null : started.AddMilliseconds(12),
    RetainUntilUtc = retainUntil ?? started.AddDays(30)
};

static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString("D")) }, "P10Synthetic"));

static async Task AssertDeniedAsync(Func<Task> action, string message)
{
    try
    {
        await action();
        throw new InvalidOperationException(message);
    }
    catch (RequestHistoryAccessDeniedException)
    {
        // Expected fail-closed authorization.
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

sealed class FakePermissionEvaluator : IGsipPermissionEvaluator
{
    private readonly Dictionary<Guid, HashSet<string>> _global = [];
    private readonly Dictionary<(Guid UserId, string ServiceCode), HashSet<string>> _service = [];

    public void GrantGlobal(Guid userId, params string[] permissions) =>
        _global[userId] = new HashSet<string>(permissions, StringComparer.Ordinal);

    public void GrantService(Guid userId, string serviceCode, params string[] permissions) =>
        _service[(userId, serviceCode.ToUpperInvariant())] = new HashSet<string>(permissions, StringComparer.Ordinal);

    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default)
    {
        var id = ReadId(principal);
        return Task.FromResult(id is not null && _global.TryGetValue(id.Value, out var set) && set.Contains(permission));
    }

    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default)
    {
        var id = ReadId(principal);
        return Task.FromResult(id is not null
            && _service.TryGetValue((id.Value, serviceCode.Trim().ToUpperInvariant()), out var set)
            && set.Contains(permission));
    }

    private static Guid? ReadId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

sealed class FakeMetadataCatalog : IMetadataCatalogService
{
    private readonly MetadataCatalogSnapshot _snapshot;

    public FakeMetadataCatalog(Guid entityId, Guid serviceAId, Guid serviceBId)
    {
        var entity = new CatalogEntity { Id = entityId, Code = "MOJ-SYNTHETIC", NameAr = "Synthetic", NameEn = "Synthetic", Active = true };
        _snapshot = new MetadataCatalogSnapshot(
            new[] { entity },
            new[]
            {
                new CatalogService { Id = serviceAId, EntityId = entityId, Code = "SVC-A", NameAr = "A", NameEn = "A", Active = true, IsCurrent = true, Version = 1 },
                new CatalogService { Id = serviceBId, EntityId = entityId, Code = "SVC-B", NameAr = "B", NameEn = "B", Active = true, IsCurrent = true, Version = 1 }
            },
            Array.Empty<CatalogEnvironment>());
    }

    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
    public Task<CatalogEntity> CreateEntityAsync(EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogEntity> UpdateEntityAsync(Guid entityId, EntityInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeactivateEntityAsync(Guid entityId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> CreateServiceAsync(Guid entityId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> UpdateServiceAsync(Guid serviceId, ServiceInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CatalogService> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task MarkServiceUsedAsync(Guid serviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string> ExportJsonAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<MetadataImportResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
