using System.Reflection;
using System.Security.Claims;
using System.Xml.Linq;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Secrets;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

var root = FindRepositoryRoot();
var viewPath = Path.Combine(root, "src", "GSIP.Web", "Views", "AuthProfiles", "Index.cshtml");
var modelPath = Path.Combine(root, "src", "GSIP.Web", "Models", "AuthProfileAdminViewModels.cs");
var controllerPath = Path.Combine(root, "src", "GSIP.Web", "Controllers", "AuthProfilesController.cs");
var cssPath = Path.Combine(root, "src", "GSIP.Web", "wwwroot", "css", "auth-profiles.css");
var layoutPath = Path.Combine(root, "src", "GSIP.Web", "Views", "Shared", "_Layout.cshtml");
var enPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "AuthProfileResource.resx");
var arPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "AuthProfileResource.ar-KW.resx");
var shellEnPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "ShellResource.resx");
var shellArPath = Path.Combine(root, "src", "GSIP.Web", "Resources", "ShellResource.ar-KW.resx");

var view = File.ReadAllText(viewPath);
var model = File.ReadAllText(modelPath);
var controllerSource = File.ReadAllText(controllerPath);
var css = File.ReadAllText(cssPath);
var layout = File.ReadAllText(layoutPath);
var en = XDocument.Load(enPath);
var ar = XDocument.Load(arPath);
var shellEn = XDocument.Load(shellEnPath);
var shellAr = XDocument.Load(shellArPath);

Check(!model.Contains("SecretValue", StringComparison.Ordinal), "Presentation model cannot carry plaintext SecretValue.");
Check(!model.Contains("PlaintextValue", StringComparison.Ordinal), "Presentation model cannot carry plaintext secret aliases.");
Check(view.CountOccurrences("type=\"password\"") >= 2, "Secret create and rotation UI must use password inputs.");
Check(view.CountOccurrences("autocomplete=\"new-password\"") >= 2, "Secret inputs must never be browser-recovered values.");
Check(view.CountOccurrences("@Html.AntiForgeryToken()") >= 7, "Every mutation form family must render an anti-forgery token.");
Check(!view.Contains("value=\"@secret.", StringComparison.OrdinalIgnoreCase), "Secret metadata must never populate a value attribute.");
Check(!view.Contains("@secret.SecretValue", StringComparison.OrdinalIgnoreCase)
    && !view.Contains("@Model.SecretValue", StringComparison.OrdinalIgnoreCase)
    && !view.Contains(".PlaintextValue", StringComparison.OrdinalIgnoreCase)
    && !view.Contains("SecretReference", StringComparison.Ordinal),
    "Razor must not render plaintext or canonical SecretRef values.");
Check(view.Contains("name=\"ownerBinding\"", StringComparison.Ordinal)
    && view.Contains("name=\"targetBinding\"", StringComparison.Ordinal)
    && view.Contains("name=\"confirmShare\"", StringComparison.Ordinal),
    "Create must own one exact binding and sharing must be an explicit separate action.");
Check(view.Contains("ApiKeyHeader", StringComparison.Ordinal)
    && view.Contains("StaticBearer", StringComparison.Ordinal)
    && view.Contains("TokenEndpoint", StringComparison.Ordinal)
    && view.Contains("ApiKeyPlusBearer", StringComparison.Ordinal)
    && view.Contains("CustomHeaders", StringComparison.Ordinal),
    "UI authentication types must match the canonical AuthProfileType contract.");
Check(view.Contains("confirmRotation", StringComparison.Ordinal), "Rotation requires explicit confirmation.");
Check(view.Contains("CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft", StringComparison.Ordinal), "UI must respond to RTL/LTR culture direction.");
Check(css.Contains(":focus-visible", StringComparison.Ordinal) && css.Contains("@media(max-width:480px)", StringComparison.Ordinal), "Keyboard focus and narrow mobile behavior are required.");
Check(layout.Contains("isAuthProfiles", StringComparison.Ordinal)
    && layout.Contains("href=\"/auth-profiles?culture=", StringComparison.Ordinal)
    && layout.Contains("@L[\"AuthProfiles\"]", StringComparison.Ordinal),
    "AuthProfile administration must be discoverable in the governed GSIP shell with an active-route state.");
Check(!controllerSource.Contains("ILogger", StringComparison.Ordinal), "The secret administration controller must not log submitted secret material.");
Check(controllerSource.Contains("CryptographicOperations.ZeroMemory(clearBytes)", StringComparison.Ordinal), "Submitted secret bytes must be cleared after vault persistence.");
Check(controllerSource.Contains("ModelState.Remove(nameof(secretValue))", StringComparison.Ordinal), "Secret inputs must be removed from validation state before rendering/redirect paths.");

var enKeys = ResourceKeys(en);
var arKeys = ResourceKeys(ar);
Check(enKeys.SetEquals(arKeys), "English and Arabic AuthProfile localization keys must remain in parity.");
Check(ar.Descendants("value").Any(value => value.Value.Any(ch => ch is >= '\u0600' and <= '\u06FF')), "Arabic AuthProfile resource must contain Arabic localized content.");
var shellEnKeys = ResourceKeys(shellEn);
var shellArKeys = ResourceKeys(shellAr);
Check(shellEnKeys.SetEquals(shellArKeys), "English and Arabic shell localization keys must remain in parity.");
Check(shellEnKeys.Contains("AuthProfiles"), "Shell localization must include the AuthProfiles navigation key.");

var controllerType = typeof(AuthProfilesController);
var authorize = controllerType.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();
Check(authorize?.Policy == GsipPermissions.ServiceSecretsManage, "AuthProfilesController must enforce ServiceSecrets.Manage server-side.");

var mutationNames = new[]
{
    nameof(AuthProfilesController.Create),
    nameof(AuthProfilesController.Share),
    nameof(AuthProfilesController.CreateSecret),
    nameof(AuthProfilesController.RotateSecret),
    nameof(AuthProfilesController.SetState),
    nameof(AuthProfilesController.UpdateMetadata),
    nameof(AuthProfilesController.Delete)
};
foreach (var mutationName in mutationNames)
{
    var method = controllerType.GetMethod(mutationName) ?? throw new InvalidOperationException($"Missing mutation action {mutationName}.");
    Check(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null, $"{mutationName} must require anti-forgery validation.");
    Check(method.GetParameters().All(parameter => parameter.ParameterType != typeof(SecretRef)), $"{mutationName} must not accept a client-supplied SecretRef.");
}

var fixture = SecurityFixture.Create();
var fakeCatalog = new FakeCatalog(fixture.Snapshot);
var fakeProfiles = new FakeAuthProfiles(fixture.Profile);
var fakeVault = new FakeVault(fixture.SecretReference);
var controller = new AuthProfilesController(fakeCatalog, fakeProfiles, fakeVault)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-acceptance"),
                    new Claim(ClaimTypes.Name, "admin-acceptance")
                ],
                authenticationType: "P06Checks"))
        }
    }
};

var forgedProfileId = Guid.NewGuid();
Check(await controller.Delete(forgedProfileId, CancellationToken.None) is NotFoundResult, "Forged AuthProfile ID must return NotFound.");
Check(await controller.SetState(forgedProfileId, true, null, CancellationToken.None) is NotFoundResult, "Forged AuthProfile state mutation must return NotFound.");
Check(await controller.RotateSecret(forgedProfileId, Guid.NewGuid(), "synthetic-secret-value", true, null, CancellationToken.None) is NotFoundResult, "Forged AuthProfile rotation must return NotFound.");

var crossEntityTarget = $"{fixture.EntityAId:D}|{fixture.ServiceBId:D}|{fixture.EnvironmentId:D}";
var shareCallsBefore = fakeProfiles.ShareCalls;
Check(await controller.Share(fixture.Profile.Id, crossEntityTarget, "synthetic explicit share reason", true, null, CancellationToken.None) is NotFoundResult,
    "Cross-entity/cross-service target mutation must fail exact hierarchy validation.");
Check(fakeProfiles.ShareCalls == shareCallsBefore, "Rejected cross-service sharing must not reach canonical persistence.");

var createCallsBefore = fakeProfiles.CreateCalls;
Check(await controller.Create("Synthetic", nameof(AuthProfileType.ApiKeyHeader), crossEntityTarget, null, CancellationToken.None) is NotFoundResult,
    "Forged owner hierarchy must be rejected before AuthProfile creation.");
Check(fakeProfiles.CreateCalls == createCallsBefore, "Rejected owner hierarchy must produce zero persisted create mutation.");

Check(await controller.Share(fixture.Profile.Id, fixture.ValidShareTarget, "synthetic reason", false, null, CancellationToken.None) is BadRequestObjectResult,
    "Sharing without explicit confirmation must be rejected.");
Check(fakeProfiles.ShareCalls == shareCallsBefore, "Unconfirmed sharing must not reach canonical persistence.");

var forgedSecretId = Guid.NewGuid();
Check(await controller.RotateSecret(fixture.Profile.Id, forgedSecretId, "synthetic-secret-value", true, null, CancellationToken.None) is NotFoundResult,
    "Forged secret-slot ID must be rejected without exposing SecretRef.");
Check(fakeVault.CreateCalls == 0 && fakeVault.RevokeCalls == 0, "Forged secret rotation must not touch the vault.");

Check(await controller.Delete(fixture.Profile.Id, CancellationToken.None) is ConflictObjectResult,
    "Deletion of an in-use AuthProfile must be rejected fail-closed.");

Check(await controller.CreateSecret(fixture.Profile.Id, fixture.SecretName, "synthetic-secret-value", null, CancellationToken.None) is ConflictObjectResult,
    "Existing secret replacement must require atomic rotation instead of unconditional replacement.");
Check(fakeVault.CreateCalls == 0, "Rejected replacement must not stage a new secret.");

Check(await controller.UpdateMetadata(fixture.Profile.Id, "Renamed", nameof(AuthProfileType.StaticBearer), CancellationToken.None) is ConflictObjectResult,
    "Metadata mutation must fail closed until the canonical foundation owns that persistence operation.");

Console.WriteLine("P06 admin controller/security checks: PASS");
Console.WriteLine($"Anti-forgery forms: {view.CountOccurrences("@Html.AntiForgeryToken()")}");
Console.WriteLine($"Write-only password inputs: {view.CountOccurrences("type=\"password\"")}");
Console.WriteLine($"AuthProfile localization keys: {enKeys.Count}");
Console.WriteLine("IDOR/cross-scope/unsafe-delete negative checks: PASS");
return;

static HashSet<string> ResourceKeys(XDocument document) => document
    .Root!
    .Elements("data")
    .Select(element => element.Attribute("name")?.Value ?? string.Empty)
    .Where(name => name.Length > 0)
    .ToHashSet(StringComparer.Ordinal);

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src", "GSIP.Web")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

sealed record SecurityFixture(
    Guid EntityAId,
    Guid EntityBId,
    Guid ServiceAId,
    Guid ServiceBId,
    Guid EnvironmentId,
    string ValidShareTarget,
    string SecretName,
    SecretRef SecretReference,
    AuthProfileDescriptor Profile,
    MetadataCatalogSnapshot Snapshot)
{
    public static SecurityFixture Create()
    {
        var entityAId = Guid.NewGuid();
        var entityBId = Guid.NewGuid();
        var serviceAId = Guid.NewGuid();
        var serviceBId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        const string secretName = "api-key";
        var secretReference = new SecretRef($"{SecretRef.Prefix}{new string('A', SecretRef.TokenLength)}");
        var now = DateTimeOffset.UtcNow;

        var profile = new AuthProfileDescriptor(
            profileId,
            serviceAId,
            environmentId,
            "Synthetic Profile",
            AuthProfileType.ApiKeyHeader,
            true,
            "acceptance",
            now,
            now,
            [new AuthProfileSecretDescriptor(secretName, secretReference)],
            [new AuthProfileBindingDescriptor(serviceAId, environmentId, false, "acceptance", "OwnerBinding", now)]);

        var entityA = new CatalogEntity { Id = entityAId, Code = "A", NameAr = "جهة أ", NameEn = "Entity A", Active = true };
        var entityB = new CatalogEntity { Id = entityBId, Code = "B", NameAr = "جهة ب", NameEn = "Entity B", Active = true };
        var environment = new CatalogEnvironment { Id = environmentId, Code = "UAT", NameAr = "اختبار", NameEn = "UAT", Active = true };
        var serviceA = new CatalogService
        {
            Id = serviceAId,
            EntityId = entityAId,
            Code = "SERVICE-A",
            NameAr = "خدمة أ",
            NameEn = "Service A",
            Active = true,
            IsCurrent = true,
            EnvironmentConfigs =
            [
                new ServiceEnvironmentConfig
                {
                    Id = Guid.NewGuid(),
                    ServiceId = serviceAId,
                    EnvironmentId = environmentId,
                    Active = true,
                    AuthProfileId = profileId
                }
            ]
        };
        var serviceB = new CatalogService
        {
            Id = serviceBId,
            EntityId = entityBId,
            Code = "SERVICE-B",
            NameAr = "خدمة ب",
            NameEn = "Service B",
            Active = true,
            IsCurrent = true,
            EnvironmentConfigs =
            [
                new ServiceEnvironmentConfig
                {
                    Id = Guid.NewGuid(),
                    ServiceId = serviceBId,
                    EnvironmentId = environmentId,
                    Active = true,
                    AuthProfileId = null
                }
            ]
        };

        var snapshot = new MetadataCatalogSnapshot([entityA, entityB], [serviceA, serviceB], [environment]);
        return new SecurityFixture(
            entityAId,
            entityBId,
            serviceAId,
            serviceBId,
            environmentId,
            $"{entityBId:D}|{serviceBId:D}|{environmentId:D}",
            secretName,
            secretReference,
            profile,
            snapshot);
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

sealed class FakeAuthProfiles(AuthProfileDescriptor profile) : IAuthProfileService
{
    public int CreateCalls { get; private set; }
    public int ShareCalls { get; private set; }

    public Task<AuthProfileDescriptor> CreateAsync(CreateAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        return Task.FromResult(profile);
    }

    public Task<AuthProfileDescriptor> GetAsync(Guid authProfileId, CancellationToken cancellationToken = default) =>
        authProfileId == profile.Id
            ? Task.FromResult(profile)
            : Task.FromException<AuthProfileDescriptor>(new KeyNotFoundException());

    public Task<AuthProfileDescriptor?> ResolveAsync(Guid serviceId, Guid environmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthProfileDescriptor?>(serviceId == profile.OwnerServiceId && environmentId == profile.OwnerEnvironmentId ? profile : null);

    public Task<AuthProfileDescriptor> ShareAsync(ShareAuthProfileCommand command, CancellationToken cancellationToken = default)
    {
        ShareCalls++;
        return Task.FromResult(profile);
    }

    public Task<AuthProfileDescriptor> SetSecretReferenceAsync(Guid authProfileId, string secretName, SecretRef secretRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(profile);

    public Task<AuthProfileDescriptor> SetEnabledAsync(Guid authProfileId, bool enabled, CancellationToken cancellationToken = default) =>
        Task.FromResult(profile);
}

sealed class FakeVault(SecretRef reference) : ISecretVault
{
    public int CreateCalls { get; private set; }
    public int RevokeCalls { get; private set; }

    public Task<SecretDescriptor> CreateActiveAsync(ReadOnlyMemory<byte> secretMaterial, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        return Task.FromResult(new SecretDescriptor(reference, SecretLifecycleState.Active, 1, DateTimeOffset.UtcNow, null));
    }

    public Task<SecretDescriptor> GetDescriptorAsync(SecretRef secretRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SecretDescriptor(secretRef, SecretLifecycleState.Active, 1, DateTimeOffset.UtcNow, null));

    public Task<SecretDescriptor> RevokeAsync(SecretRef secretRef, CancellationToken cancellationToken = default)
    {
        RevokeCalls++;
        return Task.FromResult(new SecretDescriptor(secretRef, SecretLifecycleState.Revoked, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }
}

static class StringExtensions
{
    public static int CountOccurrences(this string value, string fragment)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }
        return count;
    }
}
