using GSIP.Application.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Route("setup")]
public sealed class SetupController(ISetupService setupService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(SetupStep? step, CancellationToken cancellationToken)
    {
        var status = await setupService.GetStatusAsync(cancellationToken);
        if (status.IsCompleted)
        {
            return RedirectToAction("Login", "Shell");
        }

        var requestedStep = step ?? status.Draft.CurrentStep;
        SetupOperationResult? operation = null;
        IReadOnlyList<string> checks = [];
        if (requestedStep == SetupStep.Preflight)
        {
            operation = await setupService.RunPreflightAsync(cancellationToken);
            checks = operation.Success
                ? ["Application data directory is writable.", "Data Protection encryption round-trip succeeded.", ".NET application runtime is active."]
                : [operation.Message];
        }

        return View("Wizard", new SetupWizardViewModel
        {
            Step = requestedStep,
            Draft = status.Draft,
            Operation = operation,
            PreflightChecks = checks
        });
    }

    [HttpPost("welcome")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Welcome(string culture, CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        draft.Culture = string.Equals(culture, "ar-KW", StringComparison.OrdinalIgnoreCase) ? "ar-KW" : "en";
        draft.CurrentStep = SetupStep.Preflight;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Preflight, culture = draft.Culture });
    }

    [HttpPost("preflight")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preflight(CancellationToken cancellationToken)
    {
        var result = await setupService.RunPreflightAsync(cancellationToken);
        if (!result.Success)
        {
            return await RenderAsync(SetupStep.Preflight, result, cancellationToken);
        }

        var draft = await GetDraftAsync(cancellationToken);
        draft.CurrentStep = SetupStep.Database;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Database, culture = draft.Culture });
    }

    [HttpPost("database")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Database(DatabaseSetupOptions database, string command, CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);

        if (database.UseWindowsAuthentication)
        {
            database.Username = string.Empty;
            database.Password = string.Empty;
        }
        else if (string.IsNullOrEmpty(database.Password)
            && !draft.Database.UseWindowsAuthentication
            && string.Equals(database.Server, draft.Database.Server, StringComparison.OrdinalIgnoreCase)
            && string.Equals(database.Username, draft.Database.Username, StringComparison.Ordinal))
        {
            // Passwords are deliberately never rendered back into HTML. Reuse the encrypted
            // draft value when the user repeats Test/Next without changing the SQL identity.
            database.Password = draft.Database.Password;
        }

        draft.Database = database;
        draft.DatabaseConnectionVerified = false;
        draft.DatabaseProvisioned = false;

        var result = await setupService.TestDatabaseAsync(database, cancellationToken);
        if (!result.Success || string.Equals(command, "test", StringComparison.OrdinalIgnoreCase))
        {
            draft.DatabaseConnectionVerified = result.Success;
            await setupService.SaveDraftAsync(draft, cancellationToken);
            return await RenderAsync(SetupStep.Database, result, cancellationToken);
        }

        draft.DatabaseConnectionVerified = true;
        draft.CurrentStep = SetupStep.Provision;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Provision, culture = draft.Culture });
    }

    [HttpPost("provision")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Provision(CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        if (!draft.DatabaseConnectionVerified)
        {
            return await RenderAsync(SetupStep.Database, SetupOperationResult.Fail("DATABASE_TEST_REQUIRED", "Test Connection must succeed before provisioning."), cancellationToken);
        }

        var result = await setupService.ProvisionDatabaseAsync(draft.Database, cancellationToken);
        if (!result.Success)
        {
            return await RenderAsync(SetupStep.Provision, result, cancellationToken);
        }

        draft.DatabaseProvisioned = true;
        draft.CurrentStep = SetupStep.Administrator;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Administrator, culture = draft.Culture });
    }

    [HttpPost("administrator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Administrator(AdministratorSetupOptions administrator, string confirmPassword, CancellationToken cancellationToken)
    {
        if (!string.Equals(administrator.Password, confirmPassword, StringComparison.Ordinal))
        {
            return await RenderAsync(SetupStep.Administrator, SetupOperationResult.Fail("PASSWORD_CONFIRMATION", "Administrator password confirmation does not match."), cancellationToken);
        }

        var draft = await GetDraftAsync(cancellationToken);
        draft.Administrator = administrator;
        draft.CurrentStep = SetupStep.Branding;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Branding, culture = draft.Culture });
    }

    [HttpPost("branding")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Branding(BrandingSetupOptions branding, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branding.OrganizationNameEn) || string.IsNullOrWhiteSpace(branding.OrganizationNameAr))
        {
            return await RenderAsync(SetupStep.Branding, SetupOperationResult.Fail("BRANDING_REQUIRED", "Arabic and English organization names are required."), cancellationToken);
        }

        var draft = await GetDraftAsync(cancellationToken);
        draft.Branding = branding;
        draft.CurrentStep = SetupStep.Security;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Security, culture = draft.Culture });
    }

    [HttpPost("security")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(SecuritySetupOptions security, CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        draft.Security = security;
        draft.CurrentStep = SetupStep.Integration;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Integration, culture = draft.Culture });
    }

    [HttpPost("integration")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Integration(IntegrationSetupOptions integration, CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        draft.Integration = integration;
        draft.CurrentStep = SetupStep.Notifications;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Notifications, culture = draft.Culture });
    }

    [HttpPost("notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications(NotificationSetupOptions notifications, CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        draft.Notifications = notifications;
        draft.CurrentStep = SetupStep.Review;
        await setupService.SaveDraftAsync(draft, cancellationToken);
        return RedirectToAction(nameof(Index), new { step = SetupStep.Review, culture = draft.Culture });
    }

    [HttpPost("finish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(CancellationToken cancellationToken)
    {
        var draft = await GetDraftAsync(cancellationToken);
        var result = await setupService.CompleteAsync(draft, cancellationToken);
        if (!result.Success)
        {
            return await RenderAsync(SetupStep.Review, result, cancellationToken);
        }

        return RedirectToAction("Login", "Shell", new { culture = draft.Culture, setup = "complete" });
    }

    private async Task<SetupDraft> GetDraftAsync(CancellationToken cancellationToken)
    {
        var status = await setupService.GetStatusAsync(cancellationToken);
        if (status.IsCompleted)
        {
            throw new InvalidOperationException("Setup is locked after successful completion.");
        }
        return status.Draft;
    }

    private async Task<IActionResult> RenderAsync(SetupStep step, SetupOperationResult operation, CancellationToken cancellationToken)
    {
        var status = await setupService.GetStatusAsync(cancellationToken);
        return View("Wizard", new SetupWizardViewModel
        {
            Step = step,
            Draft = status.Draft,
            Operation = operation,
            PreflightChecks = step == SetupStep.Preflight ? [operation.Message] : []
        });
    }
}
