using System.Reflection;
using System.Security.Claims;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;

const string GateTypeName = "GSIP.Application.Execution.IServiceExecutionSecurityGate";
const string RejectionTypeName = "GSIP.Application.Execution.ServiceExecutionRejectedException";

var applicationAssembly = typeof(IMetadataCatalogService).Assembly;
var gateType = applicationAssembly.GetType(GateTypeName, throwOnError: false);
var rejectionType = applicationAssembly.GetType(RejectionTypeName, throwOnError: false);

Check(gateType is not null,
    "P07 execution must expose a server-side security gate before any outbound execution can be authorized.");
Check(gateType!.IsInterface,
    "P07 execution security gate must be an interface so authorization/isolation remains independently testable.");
var authorize = gateType.GetMethod("AuthorizeAsync", BindingFlags.Public | BindingFlags.Instance);
Check(authorize is not null,
    "P07 execution security gate must expose AuthorizeAsync.");
var parameterTypes = authorize!.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
Check(parameterTypes.Any(type => type == typeof(ClaimsPrincipal)),
    "AuthorizeAsync must authorize the authenticated principal server-side.");
Check(parameterTypes.Count(type => type == typeof(Guid)) >= 2,
    "AuthorizeAsync must receive exact ServiceId and EnvironmentId values; no ambient/fallback environment lookup is allowed.");
Check(rejectionType is not null && typeof(Exception).IsAssignableFrom(rejectionType),
    "P07 execution must use one safe fail-closed rejection exception for unauthorized/forged/cross-scope requests.");
Check(!typeof(AuthorizedServiceExecutionBinding).GetProperties().Any(property =>
        property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || property.Name.Contains("Reference", StringComparison.OrdinalIgnoreCase)),
    "Authorized execution binding must not expose secret material or SecretRef values.");

var serviceAId = Guid.Parse("10000000-0000-0000-0000-000000000701");
var serviceBId = Guid.Parse("10000000-0000-0000-0000-000000000702");
var profileAId = Guid.Parse("20000000-0000-0000-0000-000000000701");
var forgedServiceId = Guid.Parse("30000000-0000-0000-0000-000000000701");
var principal = SyntheticPrincipal();
var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

var exactProfile = Profile(
    profileAId,
    serviceAId,
    CatalogEnvironmentCodes.ProductionId,
    enabled: true,
    [Binding(serviceAId, CatalogEnvironmentCodes.ProductionId, shared: false)]);
var serviceA = Service(
    serviceAId,
    "SYNTH-A",
    [Config(serviceAId, CatalogEnvironmentCodes.ProductionId, profileAId)]);
var fullSnapshot = Snapshot(serviceA);

var authProfiles = new FakeAuthProfiles((serviceId, environmentId) =>
    serviceId == serviceAId && environmentId == CatalogEnvironmentCodes.ProductionId ? exactProfile : null);
var deniedPermission = new FakePermissionEvaluator(false);
var deniedGate = new ServiceExecutionSecurityGate(new FakeMetadata(fullSnapshot), deniedPermission, authProfiles);
await ExpectRejectedAsync(
    () => deniedGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "service-scoped Services.Execute denial must fail closed");
Check(authProfiles.ResolveCalls == 0,
    "Unauthorized execution must not resolve AuthProfile metadata.");

var allowedPermission = new FakePermissionEvaluator(true);
var forgedGate = new ServiceExecutionSecurityGate(new FakeMetadata(fullSnapshot), allowedPermission, new FakeAuthProfiles((_, _) => exactProfile));
await ExpectRejectedAsync(
    () => forgedGate.AuthorizeAsync(principal, forgedServiceId, CatalogEnvironmentCodes.ProductionId),
    "forged ServiceId must fail closed without IDOR detail");

var unauthenticatedGate = new ServiceExecutionSecurityGate(new FakeMetadata(fullSnapshot), allowedPermission, new FakeAuthProfiles((_, _) => exactProfile));
await ExpectRejectedAsync(
    () => unauthenticatedGate.AuthorizeAsync(unauthenticatedPrincipal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "unauthenticated execution must fail closed");

var uatOnlyService = Service(
    serviceAId,
    "SYNTH-A",
    [Config(serviceAId, CatalogEnvironmentCodes.UatId, profileAId)]);
var noFallbackAuth = new FakeAuthProfiles((_, _) => exactProfile);
var noFallbackGate = new ServiceExecutionSecurityGate(new FakeMetadata(Snapshot(uatOnlyService)), allowedPermission, noFallbackAuth);
await ExpectRejectedAsync(
    () => noFallbackGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "Production execution must never fall back to UAT configuration");
Check(noFallbackAuth.ResolveCalls == 0,
    "Missing exact Production configuration must fail before AuthProfile resolution; UAT fallback is forbidden.");

var crossEnvironmentProfile = Profile(
    profileAId,
    serviceAId,
    CatalogEnvironmentCodes.UatId,
    enabled: true,
    [Binding(serviceAId, CatalogEnvironmentCodes.UatId, shared: false)]);
var crossEnvironmentGate = new ServiceExecutionSecurityGate(
    new FakeMetadata(fullSnapshot),
    allowedPermission,
    new FakeAuthProfiles((_, _) => crossEnvironmentProfile));
await ExpectRejectedAsync(
    () => crossEnvironmentGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "Production execution must reject an AuthProfile that is bound only to UAT");

var crossServiceProfile = Profile(
    profileAId,
    serviceBId,
    CatalogEnvironmentCodes.ProductionId,
    enabled: true,
    [Binding(serviceBId, CatalogEnvironmentCodes.ProductionId, shared: false)]);
var crossServiceGate = new ServiceExecutionSecurityGate(
    new FakeMetadata(fullSnapshot),
    allowedPermission,
    new FakeAuthProfiles((_, _) => crossServiceProfile));
await ExpectRejectedAsync(
    () => crossServiceGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "Service A execution must reject Service B AuthProfile without an explicit shared binding");

var disabledProfile = exactProfile with { IsEnabled = false };
var disabledGate = new ServiceExecutionSecurityGate(
    new FakeMetadata(fullSnapshot),
    allowedPermission,
    new FakeAuthProfiles((_, _) => disabledProfile));
await ExpectRejectedAsync(
    () => disabledGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId),
    "disabled AuthProfile must fail closed");

var positivePermission = new FakePermissionEvaluator(true);
var positiveGate = new ServiceExecutionSecurityGate(
    new FakeMetadata(fullSnapshot),
    positivePermission,
    new FakeAuthProfiles((_, _) => exactProfile));
var authorized = await positiveGate.AuthorizeAsync(principal, serviceAId, CatalogEnvironmentCodes.ProductionId);
Check(authorized.ServiceId == serviceAId, "Authorized binding must preserve exact ServiceId.");
Check(authorized.EnvironmentId == CatalogEnvironmentCodes.ProductionId, "Authorized binding must preserve exact EnvironmentId.");
Check(authorized.AuthProfileId == profileAId, "Authorized binding must preserve exact AuthProfileId metadata only.");
Check(authorized.AuthProfileVersion == exactProfile.Version, "Authorized binding must preserve AuthProfile version for downstream cache identity.");
Check(positivePermission.LastServiceCode == "SYNTH-A" && positivePermission.LastPermission == GsipPermissions.ServicesExecute,
    "Execution authorization must use the exact service code and Services.Execute permission.");

Console.WriteLine("P07 execution authorization and binding-isolation checks passed.");
return;

static ClaimsPrincipal SyntheticPrincipal()
{
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "40000000-0000-0000-0000-000000000701")],
        "SyntheticTest");
    return new ClaimsPrincipal(identity);
}

static MetadataCatalogSnapshot Snapshot(CatalogService service) => new(
    [],
    [service],
    [
        new CatalogEnvironment { Id = CatalogEnvironmentCodes.UatId, Code = CatalogEnvironmentCodes.Uat, Active = true },
        new CatalogEnvironment { Id = CatalogEnvironmentCodes.ProductionId, Code = CatalogEnvironmentCodes.Production, Active = true }
    ]);

static CatalogService Service(Guid serviceId, string code, IReadOnlyList<ServiceEnvironmentConfig> configs) => new()
{
    Id = serviceId,
    DefinitionKey = serviceId,
    Code = code,
    Active = true,
    IsCurrent = true,
    EnvironmentConfigs = configs.ToList()
};

static ServiceEnvironmentConfig Config(Guid serviceId, Guid environmentId, Guid? profileId) => new()
{
    Id = Guid.NewGuid(),
    ServiceId = serviceId,
    EnvironmentId = environmentId,
    BaseUrl = environmentId == CatalogEnvironmentCodes.ProductionId
        ? "https://synthetic-production.invalid"
        : "https://synthetic-uat.invalid",
    RelativePath = "/synthetic",
    HttpMethod = "POST",
    ContentType = "application/json",
    TimeoutSeconds = 10,
    TlsPolicy = "SystemDefault",
    ValidateServerCertificate = true,
    Active = true,
    AuthProfileId = profileId
};

static AuthProfileBindingDescriptor Binding(Guid serviceId, Guid environmentId, bool shared) => new(
    serviceId,
    environmentId,
    shared,
    "synthetic-actor",
    shared ? "synthetic explicit sharing" : "synthetic owner binding",
    DateTimeOffset.UnixEpoch);

static AuthProfileDescriptor Profile(
    Guid profileId,
    Guid ownerServiceId,
    Guid ownerEnvironmentId,
    bool enabled,
    IReadOnlyList<AuthProfileBindingDescriptor> bindings) => new(
        profileId,
        ownerServiceId,
        ownerEnvironmentId,
        "Synthetic Profile",
        AuthProfileType.ApiKeyHeader,
        enabled,
        7,
        "synthetic-actor",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        [],
        bindings);

static async Task ExpectRejectedAsync(Func<Task<AuthorizedServiceExecutionBinding>> action, string scenario)
{
    try
    {
        _ = await action();
        throw new InvalidOperationException($"Expected rejection: {scenario}");
    }
    catch (ServiceExecutionRejectedException exception)
    {
        Check(exception.Message == ServiceExecutionRejectedException.SafeMessage,
            $"Rejection for '{scenario}' must use the constant safe message.");
        Check(!exception.Message.Contains("SYNTH", StringComparison.OrdinalIgnoreCase),
            $"Rejection for '{scenario}' must not disclose service/profile details.");
    }
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
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

sealed class FakePermissionEvaluator(bool allowed) : IGsipPermissionEvaluator
{
    public string LastServiceCode { get; private set; } = string.Empty;
    public string LastPermission { get; private set; } = string.Empty;

    public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> HasServicePermissionAsync(ClaimsPrincipal principal, string serviceCode, string permission, CancellationToken cancellationToken = default)
    {
        LastServiceCode = serviceCode;
        LastPermission = permission;
        return Task.FromResult(allowed);
    }
}

sealed class FakeAuthProfiles(Func<Guid, Guid, AuthProfileDescriptor?> resolver) : IAuthProfileService
{
    public int ResolveCalls { get; private set; }

    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return Task.FromResult(resolver(serviceId, environmentId));
    }

    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
