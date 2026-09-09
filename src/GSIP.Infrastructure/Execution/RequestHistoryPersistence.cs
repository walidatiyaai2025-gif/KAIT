using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GSIP.Application.Abstractions;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Execution;

[Index(nameof(RequestId), IsUnique = true)]
[Index(nameof(ActorUserId), nameof(StartedAtUtc))]
[Index(nameof(DepartmentCode), nameof(StartedAtUtc))]
[Index(nameof(ServiceId), nameof(StartedAtUtc))]
[Index(nameof(RetainUntilUtc), nameof(LifecycleStatus))]
public sealed class RequestExecutionRecord
{
    public Guid Id { get; set; }
    [MaxLength(100)] public string RequestId { get; set; } = string.Empty;
    [MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid? UserReferenceId { get; set; }
    [ForeignKey(nameof(UserReferenceId))] public ApplicationUser? User { get; set; }
    [MaxLength(100)] public string? DepartmentCode { get; set; }
    public Guid EntityId { get; set; }
    [MaxLength(40)] public string EntityCode { get; set; } = string.Empty;
    public Guid ServiceId { get; set; }
    [MaxLength(120)] public string ServiceCode { get; set; } = string.Empty;
    public int ServiceVersion { get; set; }
    public Guid EnvironmentId { get; set; }
    [MaxLength(40)] public string EnvironmentCode { get; set; } = string.Empty;
    public string MaskedInputJson { get; set; } = "{}";
    public byte[]? ProtectedStructuredResult { get; set; }
    public byte[]? ProtectedRawResponse { get; set; }
    public RequestLifecycleStatus LifecycleStatus { get; set; }
    [MaxLength(80)] public string OutcomeCode { get; set; } = string.Empty;
    public int? HttpStatusCode { get; set; }
    public long DurationMilliseconds { get; set; }
    public int Attempts { get; set; }
    public bool ResultSuppressedByBound { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class RequestHistoryOptions
{
    public const string SectionName = "RequestHistory";
    public bool StoreStructuredResult { get; set; } = true;
    public bool StoreRawResponse { get; set; }
    public int RetentionDays { get; set; } = 90;
    public int MaxStoredPayloadBytes { get; set; } = 262_144;
    public int MaxExportRows { get; set; } = 5000;
    public int MaxPdfRows { get; set; } = 1000;
    public Dictionary<string, RequestHistoryServiceOptions> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RequestHistoryServiceOptions
{
    public bool? StoreStructuredResult { get; set; }
    public bool? StoreRawResponse { get; set; }
    public int? RetentionDays { get; set; }
    public int? MaxStoredPayloadBytes { get; set; }
}

public sealed class RequestHistoryStore(
    GsipDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IMetadataCatalogService metadataCatalog,
    ISystemClock clock,
    IOptions<RequestHistoryOptions> options) : IRequestHistoryStore
{
    private const string Mask = "[MASKED]";
    private const int MaxInputValueChars = 4096;
    private const int MaxInputJsonBytes = 65_536;
    private static readonly Regex ForceSensitiveKey = new(
        "civil|password|token|secret|api[-_ ]?key|authorization|credential",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("GSIP.RequestHistory.v1");
    private readonly RequestHistoryOptions _options = options.Value;

    public async Task<string> StartAsync(
        ServiceExecutionCommand command,
        AuthorizedServiceExecutionBinding binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var userId = GetRequiredUserId(command.Principal);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId && item.IsEnabled, cancellationToken)
            ?? throw new ServiceExecutionRejectedException();

        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        var services = snapshot.Services.Where(item => item.Id == binding.ServiceId && item.IsCurrent && item.Active).Take(2).ToArray();
        if (services.Length != 1)
            throw new ServiceExecutionRejectedException();
        var service = services[0];
        var entity = snapshot.Entities.SingleOrDefault(item => item.Id == service.EntityId)
            ?? throw new ServiceExecutionRejectedException();

        var maskedInputJson = BuildMaskedInputJson(service.Fields, command.Inputs);
        var policy = ResolvePolicy(service.Code);
        var now = clock.UtcNow;
        var trackingRequestId = $"GSIP-P10-{Guid.NewGuid():N}";
        var record = new RequestExecutionRecord
        {
            Id = Guid.NewGuid(), RequestId = trackingRequestId, CorrelationId = Guid.NewGuid().ToString("D"),
            ActorUserId = userId, UserReferenceId = userId, DepartmentCode = NormalizeDepartment(user.DepartmentCode),
            EntityId = entity.Id, EntityCode = entity.Code, ServiceId = service.Id, ServiceCode = service.Code,
            ServiceVersion = service.Version, EnvironmentId = binding.EnvironmentId, EnvironmentCode = binding.EnvironmentCode,
            MaskedInputJson = maskedInputJson, LifecycleStatus = RequestLifecycleStatus.Started, OutcomeCode = "Started",
            StartedAtUtc = now, RetainUntilUtc = now.AddDays(policy.RetentionDays)
        };
        db.Set<RequestExecutionRecord>().Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return trackingRequestId;
    }

    public async Task CompleteAsync(string trackingRequestId, ServiceExecutionResult result, CancellationToken cancellationToken = default)
    {
        var records = db.Set<RequestExecutionRecord>();
        var record = await records.SingleOrDefaultAsync(item => item.RequestId == trackingRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Canonical request history start record is missing.");
        if (record.LifecycleStatus != RequestLifecycleStatus.Started)
            return;

        var policy = ResolvePolicy(record.ServiceCode);
        record.RequestId = BoundTrackingId(result.RequestId);
        record.CorrelationId = BoundTrackingId(result.CorrelationId);
        record.LifecycleStatus = result.Outcome == ServiceExecutionOutcome.Success ? RequestLifecycleStatus.Succeeded : RequestLifecycleStatus.Failed;
        record.OutcomeCode = BoundText(result.MessageCode, 80);
        record.HttpStatusCode = result.StatusCode;
        record.DurationMilliseconds = Math.Max(0, result.DurationMilliseconds);
        record.Attempts = Math.Clamp(result.Attempts, 0, 100);
        record.CompletedAtUtc = clock.UtcNow;

        if (policy.StoreStructuredResult)
        {
            var safeStructured = result.StructuredResult.Select(item => new
            {
                item.SourcePath, item.LabelAr, item.LabelEn, item.ResultType,
                Value = item.Sensitive ? Mask : BoundText(item.Value, 16_384), item.Sensitive, item.DisplayOrder
            }).ToArray();
            record.ProtectedStructuredResult = ProtectBounded(JsonSerializer.Serialize(safeStructured), policy.MaxStoredPayloadBytes, out var structuredSuppressed);
            record.ResultSuppressedByBound |= structuredSuppressed;
        }
        if (policy.StoreRawResponse && !string.IsNullOrEmpty(result.RawResponse))
        {
            record.ProtectedRawResponse = ProtectBounded(result.RawResponse, policy.MaxStoredPayloadBytes, out var rawSuppressed);
            record.ResultSuppressedByBound |= rawSuppressed;
        }
        await SaveTerminalAsync(record, cancellationToken);
    }

    public async Task TerminateAsync(string trackingRequestId, RequestLifecycleStatus status, string outcomeCode, CancellationToken cancellationToken = default)
    {
        if (status is not (RequestLifecycleStatus.Failed or RequestLifecycleStatus.Cancelled))
            throw new ArgumentOutOfRangeException(nameof(status));
        var record = await db.Set<RequestExecutionRecord>().SingleOrDefaultAsync(item => item.RequestId == trackingRequestId, cancellationToken);
        if (record is null || record.LifecycleStatus != RequestLifecycleStatus.Started)
            return;
        record.LifecycleStatus = status;
        record.OutcomeCode = BoundText(outcomeCode, 80);
        record.CompletedAtUtc = clock.UtcNow;
        await SaveTerminalAsync(record, cancellationToken);
    }

    private async Task SaveTerminalAsync(RequestExecutionRecord record, CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(record).ReloadAsync(cancellationToken);
            if (record.LifecycleStatus == RequestLifecycleStatus.Started) throw;
        }
    }

    private EffectivePolicy ResolvePolicy(string serviceCode)
    {
        _options.Services.TryGetValue(serviceCode, out var specific);
        return new EffectivePolicy(
            specific?.StoreStructuredResult ?? _options.StoreStructuredResult,
            specific?.StoreRawResponse ?? _options.StoreRawResponse,
            Math.Clamp(specific?.RetentionDays ?? _options.RetentionDays, 1, 3650),
            Math.Clamp(specific?.MaxStoredPayloadBytes ?? _options.MaxStoredPayloadBytes, 1024, 1_048_576));
    }

    private byte[]? ProtectBounded(string value, int maxBytes, out bool suppressed)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > maxBytes) { suppressed = true; return null; }
        suppressed = false;
        return _protector.Protect(bytes);
    }

    private static string BuildMaskedInputJson(IReadOnlyCollection<GSIP.Domain.Metadata.ServiceFieldDefinition> fields, IReadOnlyDictionary<string, string?> inputs)
    {
        var fieldByKey = fields.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var safe = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var input in inputs.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!fieldByKey.TryGetValue(input.Key, out var field)) continue;
            safe[field.Key] = field.Sensitive || ForceSensitiveKey.IsMatch(field.Key) ? Mask : BoundText(input.Value, MaxInputValueChars);
        }
        var json = JsonSerializer.Serialize(safe);
        if (Encoding.UTF8.GetByteCount(json) > MaxInputJsonBytes)
            throw new ServiceExecutionValidationException(new Dictionary<string, string> { ["_"] = "InputSnapshotTooLarge" });
        return json;
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var id) || id == Guid.Empty) throw new ServiceExecutionRejectedException();
        return id;
    }

    private static string? NormalizeDepartment(string? department) => string.IsNullOrWhiteSpace(department) ? null : department.Trim().ToUpperInvariant();
    private static string BoundTrackingId(string value) => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) ? throw new InvalidOperationException("Execution returned an invalid tracking identifier.") : BoundText(value, 100);
    private static string BoundText(string? value, int max) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
    private sealed record EffectivePolicy(bool StoreStructuredResult, bool StoreRawResponse, int RetentionDays, int MaxStoredPayloadBytes);
}
