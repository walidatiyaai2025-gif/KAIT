using System.Reflection;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

var root = FindRepositoryRoot();
var sourcePath = Path.Combine(root, "src", "GSIP.Web", "Controllers", "AuthProfileBindingLifecycleController.cs");
var viewPath = Path.Combine(root, "src", "GSIP.Web", "Views", "AuthProfiles", "Index.cshtml");
var source = File.ReadAllText(sourcePath);
var view = File.ReadAllText(viewPath);

var type = typeof(AuthProfileBindingLifecycleController);
var authorize = type.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();
Check(authorize?.Policy == GsipPermissions.ServiceSecretsManage,
    "Shared-binding lifecycle must enforce ServiceSecrets.Manage server-side.");
var method = type.GetMethod(nameof(AuthProfileBindingLifecycleController.UnbindShared))
    ?? throw new InvalidOperationException("Missing shared-binding unbind action.");
Check(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null,
    "Shared-binding unbind must require anti-forgery validation.");
Check(method.GetParameters().All(parameter => parameter.ParameterType != typeof(SecretRef)),
    "Shared-binding unbind must never accept a client-supplied SecretRef.");
Check(source.Contains("authProfiles.UnbindAsync", StringComparison.Ordinal)
    && source.Contains("new UnbindAuthProfileCommand", StringComparison.Ordinal),
    "Shared-binding unbind must delegate to the canonical foundation operation.");
Check(!source.Contains("DbContext", StringComparison.Ordinal)
    && !source.Contains("ILogger", StringComparison.Ordinal)
    && !source.Contains("SecretRef", StringComparison.Ordinal),
    "Shared-binding lifecycle must not own persistence, logging, or secret references.");
Check(view.Contains("environment.IsSharedProfile", StringComparison.Ordinal)
    && view.Contains("/unbind\"", StringComparison.Ordinal),
    "The UI must expose unbind only from the explicit shared-binding branch.");
Check(!view.Contains("/delete\"", StringComparison.Ordinal),
    "The UI must not expose unsupported AuthProfile deletion.");

var fixture = Fixture.Create();
var profileService = new FakeProfiles(fixture.Profile);
var catalog = new FakeCatalog(fixture.Snapshot);
var context = new DefaultHttpContext();
var controller = new AuthProfileBindingLifecycleController(catalog, profileService)
{
    ControllerContext = new ControllerContext { HttpContext = context },
    TempData = new TempDataDictionary(context, new FakeTempDataProvider())
};

Check(await controller.UnbindShared(Guid.NewGuid(), fixture.SharedServiceId, fixture.EnvironmentId, null, CancellationToken.None) is NotFoundResult,
    "Forged AuthProfile ID must be rejected.");
Check(profileService.UnbindCalls == 0, "Forged profile rejection must not reach persistence.");

Check(await controller.UnbindShared(fixture.Profile.Id, fixture.OwnerServiceId, fixture.EnvironmentId, null, CancellationToken.None) is ConflictObjectResult,
    "Owner binding must be non-removable through shared unbind.");
Check(profileService.UnbindCalls == 0, "Owner binding rejection must not reach persistence.");

Check(await controller.UnbindShared(fixture.Profile.Id, Guid.NewGuid(), fixture.EnvironmentId, null, CancellationToken.None) is NotFoundResult,
    "Forged service binding must be rejected.");
Check(profileService.UnbindCalls == 0, "Forged binding rejection must not reach persistence.");

var result = await controller.UnbindShared(
    fixture.Profile.Id,
    fixture.SharedServiceId,
    fixture.EnvironmentId,
    "ar-KW",
    CancellationToken.None);
Check(result is RedirectToActionResult redirect
    && redirect.ActionName == "Index"
    && redirect.ControllerName == "AuthProfiles"
    && string.Equals(redirect.RouteValues?["culture"]?.ToString(), "ar-KW", StringComparison.Ordinal),
    "Valid shared unbind must redirect safely while preserving Arabic culture.");
Check(profileService.UnbindCalls == 1
    && profileService.LastUnbind?.ServiceId == fixture.SharedServiceId
    && profileService.LastUnbind?.EnvironmentId == fixture.EnvironmentId,
    "Valid shared unbind must invoke the canonical operation for the exact Service + Environment only.");
Check(profileService.Current.Bindings.Count == 1
    && profileService.Current.Bindings.Single().ServiceId == fixture.OwnerServiceId
    && !profileService.Current.Bindings.Single().IsShared,
    "Shared unbind must preserve the owner binding.");

Console.WriteLine("P06 shared-binding admin checks: PASS");
return;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src", "GSIP.Web"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

sealed record Fixture(
    Guid OwnerServiceId,
    Guid SharedServiceId,
    Guid EnvironmentId,
    AuthProfileDescriptor Profile,
    MetadataCatalogSnapshot Snapshot)
{
    public static Fixture Create()
    {
        var entityId = Guid.NewGuid();
        var ownerServiceId = Guid.NewGuid();
        var sharedServiceId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var profile = new AuthProfileDescriptor(
            profileId,
            ownerServiceId,
            environmentId,
            "Binding Acceptance",
            AuthProfileType.ApiKeyHeader,
            true,
            2,
            "acceptance",
            now,
            now,
            [],
            [
                new AuthProfileBindingDescriptor(ownerServiceId, environmentId, false, "acceptance", "OwnerBinding", now),
                new AuthProfileBindingDescriptor(sharedServiceId, environmentId, true, "acceptance", "ExplicitShare", now)
            ]);

        var entity = new CatalogEntity { Id = entityId, Code = "E", NameAr = "جهة", NameEn = "Entity", Active = true };
        var environment = new CatalogEnvironment { Id = environmentId, Code = "UAT", NameAr = "اختبار", NameEn = "UAT", Active = true };
        var owner = new CatalogService
        {
            Id = ownerServiceId,
            EntityId = entityId,
            Code = "OWNER",
            NameAr = "الخدمة المالكة",
            NameEn = "Owner Service",
            Active = true,
            IsCurrent = true,
            EnvironmentConfigs = [new ServiceEnvironmentConfig { Id = Guid.NewGuid(), ServiceId = ownerServiceId, EnvironmentId = environmentId, Active = true, AuthProfileId = profileId }]
        };
        var shared = new CatalogService
        {
            Id = sharedServiceId,
            EntityId = entityId,
            Code = "SHARED",
            NameAr = "الخدمة المشتركة",
            NameEn = "Shared Service",
            Active = true,
            IsCurrent = true,
            EnvironmentConfigs = [new ServiceEnvironmentConfig { Id = Guid.NewGuid(), ServiceId = sharedServiceId, EnvironmentId = environmentId, Active = true, AuthProfileId = profileId }]
        };

        return new Fixture(ownerServiceId, sharedServiceId, environmentId, profile,
            new MetadataCatalogSnapshot([entity], [owner, shared], [environment]));
    }
}

sealed class FakeCatalog(MetadataCatalogSnapshot snapshot) : IMetadataCatalogService
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

sealed class FakeProfiles(AuthProfileDescriptor initial) : IAuthProfileService
{
    private AuthProfileDescriptor current = initial;
    public AuthProfileDescriptor Current => current;
    public int UnbindCalls { get; private set; }
    public UnbindAuthProfileCommand? LastUnbind { get; private set; }

    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        authProfileId == current.Id ? Task.FromResult(current) : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());

    public Task<AuthProfileDescriptor> UnbindAsync(UnbindAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AuthProfileId != current.Id) return Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());
        if (current.OwnerServiceId == command.ServiceId && current.OwnerEnvironmentId == command.EnvironmentId)
            return Task.FromException<AuthProfileDescriptor>(new InvalidOperationException());
        var binding = current.Bindings.SingleOrDefault(candidate => candidate.ServiceId == command.ServiceId && candidate.EnvironmentId == command.EnvironmentId);
        if (binding is null || !binding.IsShared) return Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());

        UnbindCalls++;
        LastUnbind = command;
        current = current with
        {
            Version = current.Version + 1,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Bindings = current.Bindings.Where(candidate => candidate != binding).ToList()
        };
        return Task.FromResult(current);
    }

    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> UpdateAsync(UpdateAuthProfileCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ActivateSecretReferenceAsync(Guid serviceId, Guid environmentId, Guid authProfileId, string secretName, SecretRef expectedCurrentReference, int expectedGeneration, SecretRef stagedReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
