using System.Reflection;
using System.Security.Claims;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Execution;

const string sensitiveSentinel = "SYNTHETIC_SENSITIVE_RESULT_9A71_DO_NOT_EXPOSE";
const string maskingEngineTypeName = "GSIP.Infrastructure.Execution.SensitiveResponseMaskingExecutionEngine";

var infrastructureAssembly = typeof(GenericServiceExecutionEngine).Assembly;
var maskingEngineType = infrastructureAssembly.GetType(maskingEngineTypeName, throwOnError: false);
Check(maskingEngineType is not null,
    "P07 must provide an execution-result masking boundary so Sensitive ResultMappings cannot leak through RawResponse.");
Check(typeof(IServiceExecutionEngine).IsAssignableFrom(maskingEngineType),
    "Sensitive response masking boundary must implement IServiceExecutionEngine.");

await MasksSensitiveMappedValueInRawResponseAsync(maskingEngineType!);
await MalformedSensitiveRawResponseFailsClosedAsync(maskingEngineType!);
await NonSensitiveRawResponseRemainsAvailableAsync(maskingEngineType!);

Console.WriteLine("P07 sensitive raw-response masking checks passed.");
return;

async Task MasksSensitiveMappedValueInRawResponseAsync(Type type)
{
    var service = Service([
        Mapping("$.data.secret", sensitive: true),
        Mapping("$.data.name", sensitive: false)
    ]);
    var raw = $"{{\"data\":{{\"secret\":\"{sensitiveSentinel}\",\"name\":\"Synthetic Public Value\"}}}}";
    var engine = CreateMaskingEngine(type, service, raw);

    var result = await engine.ExecuteAsync(Command(service.Id));

    Check(!result.RawResponse.Contains(sensitiveSentinel, StringComparison.Ordinal),
        "Sensitive ResultMapping value leaked through RawResponse.");
    Check(result.RawResponse.Contains("[MASKED]", StringComparison.Ordinal),
        "Sensitive RawResponse value was not replaced with the canonical mask marker.");
    Check(result.RawResponse.Contains("Synthetic Public Value", StringComparison.Ordinal),
        "Masking removed unrelated non-sensitive response data.");
}

async Task MalformedSensitiveRawResponseFailsClosedAsync(Type type)
{
    var service = Service([Mapping("$.data.secret", sensitive: true)]);
    var engine = CreateMaskingEngine(type, service, sensitiveSentinel);

    var result = await engine.ExecuteAsync(Command(service.Id));

    Check(!result.RawResponse.Contains(sensitiveSentinel, StringComparison.Ordinal),
        "Malformed response with a sensitive mapping failed open and exposed the synthetic sentinel.");
    Check(string.IsNullOrEmpty(result.RawResponse),
        "Malformed response with a sensitive mapping must suppress RawResponse rather than return unredacted content.");
}

async Task NonSensitiveRawResponseRemainsAvailableAsync(Type type)
{
    var service = Service([Mapping("$.data.name", sensitive: false)]);
    const string raw = "{\"data\":{\"name\":\"Synthetic Public Value\"}}";
    var engine = CreateMaskingEngine(type, service, raw);

    var result = await engine.ExecuteAsync(Command(service.Id));

    Check(result.RawResponse == raw,
        "RawResponse without Sensitive ResultMappings must remain available unchanged.");
}

IServiceExecutionEngine CreateMaskingEngine(Type type, CatalogService service, string raw)
{
    var inner = new FakeExecutionEngine(raw);
    var metadata = new FakeMetadata(new MetadataCatalogSnapshot([], [service], []));
    var instance = Activator.CreateInstance(type, inner, metadata);
    Check(instance is IServiceExecutionEngine,
        "Sensitive response masking boundary must expose a public constructor accepting IServiceExecutionEngine and IMetadataCatalogService.");
    return (IServiceExecutionEngine)instance!;
}

static ServiceExecutionCommand Command(Guid serviceId)
{
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "90000000-0000-0000-0000-000000000701")],
        "SyntheticMasking"));
    return new ServiceExecutionCommand(
        principal,
        serviceId,
        CatalogEnvironmentCodes.UatId,
        new Dictionary<string, string?>());
}

static CatalogService Service(IReadOnlyList<ResultMappingDefinition> mappings) => new()
{
    Id = Guid.Parse("90000000-0000-0000-0000-000000000702"),
    DefinitionKey = Guid.Parse("90000000-0000-0000-0000-000000000703"),
    EntityId = Guid.Parse("90000000-0000-0000-0000-000000000704"),
    Code = "SYNTH-MASK",
    NameAr = "اختبار إخفاء",
    NameEn = "Synthetic Masking",
    Active = true,
    IsCurrent = true,
    Version = 1,
    ResultMappings = mappings.ToList()
};

static ResultMappingDefinition Mapping(string path, bool sensitive) => new()
{
    Id = Guid.NewGuid(),
    ServiceId = Guid.Parse("90000000-0000-0000-0000-000000000702"),
    SourcePath = path,
    LabelAr = "قيمة",
    LabelEn = "Value",
    ResultType = "text",
    Formatter = string.Empty,
    Sensitive = sensitive,
    DisplayOrder = sensitive ? 1 : 2
};

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class FakeExecutionEngine(string rawResponse) : IServiceExecutionEngine
{
    public Task<ServiceExecutionResult> ExecuteAsync(ServiceExecutionCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ServiceExecutionResult(
            "GSIP-SYNTH-MASK",
            Guid.NewGuid().ToString("D"),
            "SYNTH-MASK",
            CatalogEnvironmentCodes.Uat,
            "SYNTH-MASK:UAT",
            200,
            1,
            1,
            ServiceExecutionOutcome.Success,
            "Success",
            [],
            rawResponse));
    }
}

sealed class FakeMetadata(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
{
    public Task<MetadataCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
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
