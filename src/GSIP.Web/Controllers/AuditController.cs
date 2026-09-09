using System.Globalization;
using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("audit")]
public sealed class AuditController(
    IAuditTrailService auditTrail,
    IGsipPermissionEvaluator permissions) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? entityCode = null,
        string? serviceCode = null,
        Guid? actorUserId = null,
        bool? succeeded = null,
        [FromQuery(Name = "action")] string? action = null,
        Guid? selectedId = null,
        string? integrityResult = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new AuditTrailFilter(
                fromUtc,
                toUtc,
                Bound(entityCode, 40),
                Bound(serviceCode, 120),
                actorUserId,
                succeeded,
                Bound(action, 64),
                Math.Clamp(page, 1, 100_000),
                Math.Clamp(pageSize, 1, 100));

            var pageResult = await auditTrail.QueryAsync(User, filter, cancellationToken);
            var monitoring = await auditTrail.GetMonitoringSnapshotAsync(User, cancellationToken);
            var selected = selectedId is Guid id
                ? await auditTrail.GetAsync(User, id, cancellationToken)
                : null;

            ViewData["IntegrityResult"] = integrityResult is "Healthy" or "Tampered" ? integrityResult : null;
            var model = new AuditDashboardViewModel
            {
                Page = pageResult,
                Monitoring = monitoring,
                Filter = filter,
                Selected = selected,
                CanExport = await permissions.HasPermissionAsync(User, GsipPermissions.AuditExport, cancellationToken),
                CanVerifyIntegrity = await permissions.HasPermissionAsync(User, GsipPermissions.DiagnosticsRun, cancellationToken),
                IsArabic = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft,
                Volume = BuildVolume(pageResult.Items)
            };
            return View("~/Views/Audit/Index.cshtml", model);
        }
        catch (AuditTrailAccessDeniedException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? entityCode = null,
        string? serviceCode = null,
        Guid? actorUserId = null,
        bool? succeeded = null,
        [FromQuery(Name = "action")] string? action = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new AuditTrailFilter(
                fromUtc,
                toUtc,
                Bound(entityCode, 40),
                Bound(serviceCode, 120),
                actorUserId,
                succeeded,
                Bound(action, 64),
                1,
                100);
            var export = await auditTrail.ExportAsync(User, filter, cancellationToken);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (AuditTrailAccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { code = "AUDIT_EXPORT_BOUND_EXCEEDED" });
        }
    }

    [HttpPost("verify-integrity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyIntegrity(CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await auditTrail.VerifyIntegrityAsync(User, cancellationToken);
            return RedirectToAction(nameof(Index), new
            {
                culture = CultureInfo.CurrentUICulture.Name,
                integrityResult = report.Status
            });
        }
        catch (AuditTrailAccessDeniedException)
        {
            return Forbid();
        }
    }

    private static string? Bound(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength) throw new ArgumentException("Audit filter exceeds the supported bound.");
        return trimmed;
    }

    private static IReadOnlyList<AuditVolumePoint> BuildVolume(IReadOnlyList<AuditTrailEntry> items) =>
        items
            .GroupBy(item => new DateTimeOffset(
                item.OccurredAtUtc.Year,
                item.OccurredAtUtc.Month,
                item.OccurredAtUtc.Day,
                item.OccurredAtUtc.Hour,
                0,
                0,
                TimeSpan.Zero))
            .OrderBy(group => group.Key)
            .TakeLast(12)
            .Select(group => new AuditVolumePoint(group.Key, group.Count(), group.Count(item => !item.Succeeded)))
            .ToArray();
}
