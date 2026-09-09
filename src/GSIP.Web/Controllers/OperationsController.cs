using System.Security.Claims;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Application.Operations;
using GSIP.Domain.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("operations")]
public sealed class OperationsController(
    IAdminOperationsService operations,
    IMetadataCatalogService catalog,
    IGsipPermissionEvaluator permissions,
    IAuditTrailWriter auditTrail) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var model = await operations.GetHealthAsync(User, cancellationToken);
            return View(model);
        }
        catch (AdminOperationsAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("diagnostics/database")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.DiagnosticsRun)]
    public async Task<IActionResult> TestDatabase(string? culture, CancellationToken cancellationToken)
    {
        try
        {
            var result = await operations.RunDatabaseDiagnosticAsync(User, cancellationToken);
            SetDiagnostic(result);
            return RedirectToIndex(culture);
        }
        catch (AdminOperationsAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("services/{serviceId:guid}/environments/{environmentId:guid}/test-connection")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.DiagnosticsRun)]
    public async Task<IActionResult> TestConnection(
        Guid serviceId,
        Guid environmentId,
        string? culture,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await operations.RunIntegrationDiagnosticAsync(User, serviceId, environmentId, cancellationToken);
            SetDiagnostic(result);
            return RedirectToIndex(culture);
        }
        catch (AdminOperationsAccessDeniedException)
        {
            return Forbid();
        }
        catch (AdminOperationsTargetRejectedException)
        {
            return NotFound();
        }
    }

    [HttpPost("services/{serviceId:guid}/environments/{environmentId:guid}/auth/{authProfileId:guid}/test")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.DiagnosticsRun)]
    [Authorize(Policy = GsipPermissions.ServiceSecretsManage)]
    public async Task<IActionResult> TestAuthentication(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        string? culture,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await operations.RunAuthenticationDiagnosticAsync(
                User,
                serviceId,
                environmentId,
                authProfileId,
                cancellationToken);
            SetDiagnostic(result);
            return RedirectToIndex(culture);
        }
        catch (AdminOperationsAccessDeniedException)
        {
            return Forbid();
        }
        catch (AdminOperationsTargetRejectedException)
        {
            return NotFound();
        }
    }

    [HttpPost("services/{serviceId:guid}/environments/{environmentId:guid}/state")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.ServicesManage)]
    public async Task<IActionResult> SetEnvironmentState(
        Guid serviceId,
        Guid environmentId,
        bool isActive,
        string? culture,
        CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty || environmentId == Guid.Empty)
        {
            return NotFound();
        }

        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        var serviceMatches = snapshot.Services.Where(item => item.Id == serviceId && item.IsCurrent).Take(2).ToArray();
        var environmentMatches = snapshot.Environments.Where(item => item.Id == environmentId).Take(2).ToArray();
        if (serviceMatches.Length != 1 || environmentMatches.Length != 1)
        {
            return NotFound();
        }

        var service = serviceMatches[0];
        var environment = environmentMatches[0];
        var configs = service.EnvironmentConfigs.Where(item => item.EnvironmentId == environmentId).Take(2).ToArray();
        if (configs.Length != 1)
        {
            return NotFound();
        }

        if (!await permissions.HasServicePermissionAsync(User, service.Code, GsipPermissions.ServicesManage, cancellationToken))
        {
            return Forbid();
        }

        if (isActive && (!service.Active || !environment.Active))
        {
            return Conflict("An inactive service or environment cannot be activated through the operational binding.");
        }

        var input = BuildServiceInput(snapshot, service, environmentId, isActive);
        await catalog.UpdateServiceAsync(service.Id, input, cancellationToken);

        await auditTrail.WriteAsync(new AuditTrailEvent(
            ReadActorId(User),
            isActive ? "Admin.Environment.Activate" : "Admin.Environment.Disable",
            "ServiceEnvironmentConfig",
            $"{service.Code}:{environment.Code}",
            true,
            isActive ? "Activated" : "Disabled",
            ServiceCode: service.Code,
            Metadata: new Dictionary<string, object?>
            {
                ["environmentCode"] = environment.Code,
                ["active"] = isActive
            }), cancellationToken);

        TempData["OperationsSuccess"] = isActive ? "EnvironmentActivated" : "EnvironmentDisabled";
        return RedirectToIndex(culture);
    }

    private static ServiceInput BuildServiceInput(
        MetadataCatalogSnapshot snapshot,
        CatalogService service,
        Guid targetEnvironmentId,
        bool targetActive)
    {
        var environmentCodes = snapshot.Environments.ToDictionary(item => item.Id, item => item.Code);
        return new ServiceInput(
            service.Code,
            service.NameAr,
            service.NameEn,
            service.DescriptionAr,
            service.DescriptionEn,
            service.Active,
            service.EnvironmentConfigs.OrderBy(item => environmentCodes[item.EnvironmentId], StringComparer.OrdinalIgnoreCase).Select(config =>
                new ServiceEnvironmentInput(
                    environmentCodes[config.EnvironmentId],
                    config.BaseUrl,
                    config.RelativePath,
                    config.HttpMethod,
                    config.ContentType,
                    config.NonSecretHeadersJson,
                    config.TimeoutSeconds,
                    config.TlsPolicy,
                    config.ValidateServerCertificate,
                    config.ProxyUrl,
                    config.HealthPath,
                    config.HealthMethod,
                    config.EnvironmentId == targetEnvironmentId ? targetActive : config.Active,
                    config.AuthProfileId)).ToList(),
            service.Fields.OrderBy(item => item.DisplayOrder).Select(field => new ServiceFieldInput(
                field.Key,
                field.LabelAr,
                field.LabelEn,
                field.FieldType,
                field.Required,
                field.Regex,
                field.Minimum,
                field.Maximum,
                field.MinLength,
                field.MaxLength,
                field.OptionsJson,
                field.DisplayOrder,
                field.Sensitive,
                field.Masking)).ToList(),
            service.ResultMappings.OrderBy(item => item.DisplayOrder).Select(mapping => new ResultMappingInput(
                mapping.SourcePath,
                mapping.LabelAr,
                mapping.LabelEn,
                mapping.ResultType,
                mapping.Formatter,
                mapping.Sensitive,
                mapping.DisplayOrder)).ToList());
    }

    private void SetDiagnostic(OperationalDiagnostic result)
    {
        TempData["OperationsDiagnostic"] = $"{result.Category}|{result.Code}|{result.State}|{result.CorrelationId}";
    }

    private IActionResult RedirectToIndex(string? culture) =>
        RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture) });

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
