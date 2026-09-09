using System.Security.Claims;
using GSIP.Application.Abstractions;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Application.Operations;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Operations;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
    throw new InvalidOperationException("P12 operational-state acceptance requires one LocalDB connection string argument.");

var connectionString = args[0];
var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connectionString).Options;
await using var db = new GsipDbContext(options);
await db.Database.MigrateAsync();

var now = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
var entityId = Guid.Parse("12121212-1212-4121-8121-121212121201");
var serviceId = Guid.Parse("12121212-1212-4121-8121-121212121202");
var definitionKey = Guid.Parse("12121212-1212-4121-8121-121212121203");
var uatConfigId = Guid.Parse("12121212-1212-4121-8121-121212121204");
var productionConfigId = Guid.Parse("12121212-1212-4121-8121-121212121205");
var actorId = Guid.Parse("12121212-1212-4121-8121-121212121206");

try
{
    if (!await db.CatalogEnvironments.AnyAsync(x => x.Id == CatalogEnvironmentCodes.UatId))
    {
        db.CatalogEnvironments.Add(new CatalogEnvironment
        {
            Id = CatalogEnvironmentCodes.UatId,
            Code = CatalogEnvironmentCodes.Uat,
            NameAr = "اختبار",
            NameEn = "UAT",
            Active = true,
            DisplayOrder = 10
        });
    }
    if (!await db.CatalogEnvironments.AnyAsync(x => x.Id == CatalogEnvironmentCodes.ProductionId))
    {
        db.CatalogEnvironments.Add(new CatalogEnvironment
        {
            Id = CatalogEnvironmentCodes.ProductionId,
            Code = CatalogEnvironmentCodes.Production,
            NameAr = "إنتاج",
            NameEn = "Production",
            Active = true,
            DisplayOrder = 20
        });
    }

    db.CatalogEntities.Add(new CatalogEntity
    {
        Id = entityId,
        Code = "P12-STATE-ENTITY",
        NameAr = "جهة اختبار P12",
        NameEn = "P12 State Test Entity",
        Logo = string.Empty,
        Active = true,
        DisplayOrder = 900,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    });
    db.CatalogServices.Add(new CatalogService
    {
        Id = serviceId,
        DefinitionKey = definitionKey,
        EntityId = entityId,
        Code = "P12-STATE-SYNTHETIC",
        NameAr = "خدمة اختبار الحالة",
        NameEn = "P12 Operational State Synthetic Service",
        DescriptionAr = "اختبار اصطناعي فقط",
        DescriptionEn = "Synthetic acceptance only",
        Active = true,
        Version = 7,
        IsCurrent = true,
        FirstUsedAtUtc = now.AddDays(-1),
        CreatedAtUtc = now.AddDays(-2),
        UpdatedAtUtc = now.AddDays(-1)
    });
    db.ServiceEnvironmentConfigs.AddRange(
        new ServiceEnvironmentConfig
        {
            Id = uatConfigId,
            ServiceId = serviceId,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            BaseUrl = "https://uat.synthetic.invalid",
            RelativePath = "/p12/state",
            HttpMethod = "GET",
            ContentType = "application/json",
            NonSecretHeadersJson = "{}",
            TimeoutSeconds = 17,
            TlsPolicy = "SystemDefault",
            ValidateServerCertificate = true,
            ProxyUrl = string.Empty,
            HealthPath = "/health",
            HealthMethod = "HEAD",
            Active = true,
            LastTestStatus = "PREVIOUS_UAT_STATUS",
            AuthProfileId = null
        },
        new ServiceEnvironmentConfig
        {
            Id = productionConfigId,
            ServiceId = serviceId,
            EnvironmentId = CatalogEnvironmentCodes.ProductionId,
            BaseUrl = "https://production.synthetic.invalid",
            RelativePath = "/p12/state",
            HttpMethod = "GET",
            ContentType = "application/json",
            NonSecretHeadersJson = "{}",
            TimeoutSeconds = 29,
            TlsPolicy = "SystemDefault",
            ValidateServerCertificate = true,
            ProxyUrl = string.Empty,
            HealthPath = "/health",
            HealthMethod = "HEAD",
            Active = true,
            LastTestStatus = "PREVIOUS_PRODUCTION_STATUS",
            AuthProfileId = null
        });
    await db.SaveChangesAsync();

    var permissions = new AcceptancePermissionEvaluator { Allow = true };
    var audit = new AcceptanceAuditWriter();
    var clock = new AcceptanceClock(now);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")) },
        "P12Acceptance"));
    var state = new AdminOperationalStateService(db, permissions, audit, clock);

    var disabled = await state.SetEnvironmentStateAsync(
        principal,
        serviceId,
        CatalogEnvironmentCodes.UatId,
        false);
    Assert(disabled.ServiceId == serviceId, "Disable result changed ServiceId.");
    Assert(disabled.EnvironmentId == CatalogEnvironmentCodes.UatId, "Disable result changed EnvironmentId.");
    Assert(!disabled.IsActive, "Disable result did not report inactive state.");

    db.ChangeTracker.Clear();
    await AssertIdentityAndSiblingIsolationAsync(db, serviceId, definitionKey, uatConfigId, productionConfigId, false);
    Assert(audit.Events.Count == 1, "Disable must emit exactly one audit event.");
    Assert(audit.Events[0].Action == "Admin.Environment.Disable", "Disable audit action is incorrect.");
    Assert(audit.Events[0].ServiceCode == "P12-STATE-SYNTHETIC", "Disable audit service scope is incorrect.");

    permissions.Allow = false;
    await AssertThrowsAsync<AdminOperationsAccessDeniedException>(() =>
        state.SetEnvironmentStateAsync(principal, serviceId, CatalogEnvironmentCodes.UatId, true));
    db.ChangeTracker.Clear();
    await AssertIdentityAndSiblingIsolationAsync(db, serviceId, definitionKey, uatConfigId, productionConfigId, false);

    permissions.Allow = true;
    await AssertThrowsAsync<AdminOperationsTargetRejectedException>(() =>
        state.SetEnvironmentStateAsync(principal, serviceId, Guid.NewGuid(), true));
    db.ChangeTracker.Clear();
    await AssertIdentityAndSiblingIsolationAsync(db, serviceId, definitionKey, uatConfigId, productionConfigId, false);

    var currentService = await db.CatalogServices.SingleAsync(x => x.Id == serviceId);
    currentService.Active = false;
    await db.SaveChangesAsync();
    await AssertThrowsAsync<AdminOperationsStateConflictException>(() =>
        state.SetEnvironmentStateAsync(principal, serviceId, CatalogEnvironmentCodes.UatId, true));
    db.ChangeTracker.Clear();
    await AssertIdentityAndSiblingIsolationAsync(db, serviceId, definitionKey, uatConfigId, productionConfigId, false, expectedServiceActive: false);

    currentService = await db.CatalogServices.SingleAsync(x => x.Id == serviceId);
    currentService.Active = true;
    await db.SaveChangesAsync();
    var activated = await state.SetEnvironmentStateAsync(
        principal,
        serviceId,
        CatalogEnvironmentCodes.UatId,
        true);
    Assert(activated.ServiceId == serviceId, "Activate result changed ServiceId.");
    db.ChangeTracker.Clear();
    await AssertIdentityAndSiblingIsolationAsync(db, serviceId, definitionKey, uatConfigId, productionConfigId, true);
    Assert(audit.Events.Count == 2, "Authorized disable/activate must emit exactly two audit events.");
    Assert(audit.Events[1].Action == "Admin.Environment.Activate", "Activate audit action is incorrect.");

    Console.WriteLine("PASS: used service retained exact ServiceId and metadata version during operational toggles");
    Console.WriteLine("PASS: exact UAT config identity retained while Production sibling remained unchanged");
    Console.WriteLine("PASS: unauthorized, forged-environment and inactive-parent activation paths fail closed");
    Console.WriteLine("PASS: authorized state transitions emitted scoped audit facts");
    Console.WriteLine("P12_OPERATIONAL_STATE_ISOLATION=PASS checks=4");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static async Task AssertIdentityAndSiblingIsolationAsync(
    GsipDbContext db,
    Guid serviceId,
    Guid definitionKey,
    Guid uatConfigId,
    Guid productionConfigId,
    bool expectedUatActive,
    bool expectedServiceActive = true)
{
    var current = await db.CatalogServices.AsNoTracking()
        .Where(x => x.DefinitionKey == definitionKey && x.IsCurrent)
        .ToListAsync();
    Assert(current.Count == 1, "Operational toggle created or removed a current metadata revision.");
    Assert(current[0].Id == serviceId, "Operational toggle changed the current ServiceId.");
    Assert(current[0].Version == 7, "Operational toggle changed metadata version.");
    Assert(current[0].FirstUsedAtUtc.HasValue, "Acceptance precondition requires an already-used service.");
    Assert(current[0].Active == expectedServiceActive, "Operational environment toggle changed parent service state.");

    var configs = await db.ServiceEnvironmentConfigs.AsNoTracking()
        .Where(x => x.ServiceId == serviceId)
        .OrderBy(x => x.EnvironmentId)
        .ToListAsync();
    Assert(configs.Count == 2, "Operational toggle changed sibling environment cardinality.");
    var uat = configs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.UatId);
    var production = configs.Single(x => x.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
    Assert(uat.Id == uatConfigId, "Operational toggle replaced the UAT configuration row.");
    Assert(uat.Active == expectedUatActive, "UAT active state does not match the requested operation.");
    Assert(uat.BaseUrl == "https://uat.synthetic.invalid", "Operational toggle changed UAT endpoint metadata.");
    Assert(uat.TimeoutSeconds == 17, "Operational toggle changed UAT timeout metadata.");
    Assert(uat.LastTestStatus == "PREVIOUS_UAT_STATUS", "Operational toggle changed UAT diagnostic status.");
    Assert(production.Id == productionConfigId, "Operational toggle replaced the Production configuration row.");
    Assert(production.Active, "UAT operational toggle mutated Production active state.");
    Assert(production.BaseUrl == "https://production.synthetic.invalid", "UAT operational toggle changed Production endpoint metadata.");
    Assert(production.TimeoutSeconds == 29, "UAT operational toggle changed Production timeout metadata.");
    Assert(production.LastTestStatus == "PREVIOUS_PRODUCTION_STATUS", "UAT operational toggle changed Production diagnostic status.");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class AcceptancePermissionEvaluator : IGsipPermissionEvaluator
{
    public bool Allow { get; set; }
    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(Allow);
    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(Allow && serviceCode == "P12-STATE-SYNTHETIC" && permission == GsipPermissions.ServicesManage);
}

sealed class AcceptanceAuditWriter : IAuditTrailWriter
{
    public List<AuditTrailEvent> Events { get; } = [];
    public Task<AuditTrailEntry> WriteAsync(AuditTrailEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.FromResult(new AuditTrailEntry(
            Guid.NewGuid(),
            Events.Count,
            auditEvent.ActorUserId,
            auditEvent.Action,
            auditEvent.TargetType,
            auditEvent.TargetId,
            DateTimeOffset.UtcNow,
            auditEvent.Succeeded,
            auditEvent.Outcome,
            auditEvent.CorrelationId ?? string.Empty,
            auditEvent.RequestId,
            auditEvent.EntityCode,
            auditEvent.ServiceCode,
            auditEvent.Source,
            auditEvent.Device,
            "{}",
            string.Empty,
            string.Empty));
    }
}

sealed class AcceptanceClock(DateTimeOffset value) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = value;
}
