using System.Globalization;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("requests")]
public sealed class RequestHistoryController(
    IRequestHistoryService history,
    IGsipPermissionEvaluator permissions) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        RequestHistoryScope scope = RequestHistoryScope.Own,
        string? search = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        RequestLifecycleStatus? status = null,
        string? entityCode = null,
        string? serviceCode = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var filter = new RequestHistoryFilter(scope, search, fromUtc, toUtc, status, entityCode, serviceCode, page, pageSize);
        try
        {
            var model = new RequestHistoryViewModel
            {
                Page = await history.QueryAsync(User, filter, cancellationToken),
                Filter = filter,
                CanViewOwn = await permissions.HasPermissionAsync(User, GsipPermissions.RequestsViewOwn, cancellationToken),
                CanViewDepartment = await permissions.HasPermissionAsync(User, GsipPermissions.RequestsViewDepartment, cancellationToken),
                CanViewAll = await permissions.HasPermissionAsync(User, GsipPermissions.RequestsViewAll, cancellationToken),
                CanExport = await permissions.HasPermissionAsync(User, GsipPermissions.RequestsExport, cancellationToken),
                IsArabic = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
            };
            return View("~/Views/Requests/Index.cshtml", model);
        }
        catch (RequestHistoryAccessDeniedException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpGet("{requestId}")]
    public async Task<IActionResult> Details(
        string requestId,
        RequestHistoryScope scope = RequestHistoryScope.Own,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await history.GetAsync(User, requestId, scope, cancellationToken);
            if (item is null) return NotFound();
            ViewData["IsArabic"] = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
            return View("~/Views/Requests/Details.cshtml", item);
        }
        catch (RequestHistoryAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        RequestHistoryExportFormat format,
        RequestHistoryScope scope = RequestHistoryScope.Own,
        string? search = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        RequestLifecycleStatus? status = null,
        string? entityCode = null,
        string? serviceCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new RequestHistoryFilter(scope, search, fromUtc, toUtc, status, entityCode, serviceCode, 1, 100);
            var export = await history.ExportAsync(User, filter, format, cancellationToken);
            return File(export.Content, export.ContentType, format == RequestHistoryExportFormat.Print ? null : export.FileName);
        }
        catch (RequestHistoryAccessDeniedException)
        {
            return Forbid();
        }
        catch (RequestHistoryExportLimitException)
        {
            return BadRequest(new { code = "EXPORT_LIMIT_EXCEEDED" });
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }
}
