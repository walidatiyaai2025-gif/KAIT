using GSIP.Application.Authorization;
using GSIP.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("operations")]
public sealed class OperationsController(
    IAdminOperationsService operations,
    IAdminOperationalStateService operationalState) : Controller
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
        try
        {
            var result = await operationalState.SetEnvironmentStateAsync(
                User,
                serviceId,
                environmentId,
                isActive,
                cancellationToken);
            TempData["OperationsSuccess"] = result.IsActive ? "EnvironmentActivated" : "EnvironmentDisabled";
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
        catch (AdminOperationsStateConflictException)
        {
            return Conflict("An inactive service or environment cannot be activated through the operational binding.");
        }
    }

    private void SetDiagnostic(OperationalDiagnostic result)
    {
        TempData["OperationsDiagnostic"] = $"{result.Category}|{result.Code}|{result.State}|{result.CorrelationId}";
    }

    private IActionResult RedirectToIndex(string? culture) =>
        RedirectToAction(nameof(Index), new { culture = NormalizeCulture(culture) });

    private static string NormalizeCulture(string? culture) =>
        string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";
}
