using System.IO.Compression;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Application.Authorization;
using GSIP.Application.Execution;
using GSIP.Application.Metadata;
using GSIP.Infrastructure.Identity;
using GSIP.Infrastructure.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GSIP.Infrastructure.Execution;

public sealed class RequestHistoryService(
    GsipDbContext db,
    IGsipPermissionEvaluator permissions,
    IMetadataCatalogService metadataCatalog,
    IDataProtectionProvider dataProtectionProvider,
    ISystemClock clock,
    IOptions<RequestHistoryOptions> options) : IRequestHistoryService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("GSIP.RequestHistory.v1");
    private readonly RequestHistoryOptions _options = options.Value;

    public async Task<RequestHistoryPage> QueryAsync(
        ClaimsPrincipal principal,
        RequestHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (_, query) = await BuildAuthorizedQueryAsync(principal, filter.Scope, cancellationToken);
        query = ApplyFilters(query, filter);
        var page = Math.Clamp(filter.Page, 1, 100_000);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.StartedAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(cancellationToken);
        return new RequestHistoryPage(rows.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<RequestHistoryDetail?> GetAsync(
        ClaimsPrincipal principal,
        string requestId,
        RequestHistoryScope scope = RequestHistoryScope.Own,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 100)
            return null;
        var (_, query) = await BuildAuthorizedQueryAsync(principal, scope, cancellationToken);
        var row = await query.AsNoTracking().SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
        if (row is null) return null;
        return new RequestHistoryDetail(Map(row), Unprotect(row.ProtectedStructuredResult), Unprotect(row.ProtectedRawResponse));
    }

    public async Task<RequestHistoryExport> ExportAsync(
        ClaimsPrincipal principal,
        RequestHistoryFilter filter,
        RequestHistoryExportFormat format,
        CancellationToken cancellationToken = default)
    {
        if (!await permissions.HasPermissionAsync(principal, GsipPermissions.RequestsExport, cancellationToken))
            throw new RequestHistoryAccessDeniedException();

        var (userId, query) = await BuildAuthorizedQueryAsync(principal, filter.Scope, cancellationToken);
        query = ApplyFilters(query, filter);
        var maxRows = Math.Clamp(_options.MaxExportRows, 1, 20_000);
        var rows = await query.OrderByDescending(item => item.StartedAtUtc).ThenByDescending(item => item.Id)
            .Take(maxRows + 1).AsNoTracking().ToListAsync(cancellationToken);
        if (rows.Count > maxRows) throw new RequestHistoryExportLimitException();

        var items = rows.Select(Map).ToArray();
        byte[] content;
        string contentType;
        string extension;
        switch (format)
        {
            case RequestHistoryExportFormat.Csv:
                content = BuildCsv(items); contentType = "text/csv; charset=utf-8"; extension = "csv"; break;
            case RequestHistoryExportFormat.Excel:
                content = BuildExcel(items); contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; extension = "xlsx"; break;
            case RequestHistoryExportFormat.Pdf:
                if (items.Length > Math.Clamp(_options.MaxPdfRows, 1, maxRows)) throw new RequestHistoryExportLimitException();
                content = BuildPdf(items); contentType = "application/pdf"; extension = "pdf"; break;
            case RequestHistoryExportFormat.Print:
                content = Encoding.UTF8.GetBytes(BuildPrintHtml(items)); contentType = "text/html; charset=utf-8"; extension = "html"; break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }

        db.AuthenticationAuditEvents.Add(new AuthenticationAuditEvent
        {
            Id = Guid.NewGuid(), UserId = userId, EventType = "RequestHistory.Export", Succeeded = true,
            ResultCode = $"{filter.Scope}:{format}:{items.Length}", CorrelationId = Guid.NewGuid().ToString("D"), OccurredAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var stamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss");
        return new RequestHistoryExport(content, contentType, $"GSIP-Request-History-{stamp}.{extension}", items.Length);
    }

    public async Task<int> PurgeExpiredAsync(int batchSize = 500, CancellationToken cancellationToken = default)
    {
        var bounded = Math.Clamp(batchSize, 1, 1000);
        var now = clock.UtcNow;
        var expired = await db.Set<RequestExecutionRecord>()
            .Where(item => item.LifecycleStatus != RequestLifecycleStatus.Started && item.RetainUntilUtc < now)
            .OrderBy(item => item.RetainUntilUtc).Take(bounded).ToListAsync(cancellationToken);
        if (expired.Count == 0) return 0;
        db.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private async Task<(Guid UserId, IQueryable<RequestExecutionRecord> Query)> BuildAuthorizedQueryAsync(
        ClaimsPrincipal principal, RequestHistoryScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var userId = GetRequiredUserId(principal);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId && item.IsEnabled, cancellationToken)
            ?? throw new RequestHistoryAccessDeniedException();

        var requiredPermission = scope switch
        {
            RequestHistoryScope.Own => GsipPermissions.RequestsViewOwn,
            RequestHistoryScope.Department => GsipPermissions.RequestsViewDepartment,
            RequestHistoryScope.All => GsipPermissions.RequestsViewAll,
            _ => throw new RequestHistoryAccessDeniedException()
        };
        if (!await permissions.HasPermissionAsync(principal, requiredPermission, cancellationToken))
            throw new RequestHistoryAccessDeniedException();

        var snapshot = await metadataCatalog.GetSnapshotAsync(cancellationToken);
        var allowedCodes = new List<string>();
        foreach (var service in snapshot.Services.Where(item => item.IsCurrent && item.Active).OrderBy(item => item.Code))
        {
            if (await permissions.HasServicePermissionAsync(principal, service.Code, GsipPermissions.ServicesView, cancellationToken))
                allowedCodes.Add(service.Code);
        }

        var query = db.Set<RequestExecutionRecord>().Where(item => allowedCodes.Contains(item.ServiceCode));
        switch (scope)
        {
            case RequestHistoryScope.Own:
                query = query.Where(item => item.ActorUserId == userId);
                break;
            case RequestHistoryScope.Department:
                var department = NormalizeDepartment(user.DepartmentCode);
                if (department is null) throw new RequestHistoryAccessDeniedException();
                query = query.Where(item => item.DepartmentCode == department);
                break;
            case RequestHistoryScope.All:
                break;
        }
        return (userId, query);
    }

    private static IQueryable<RequestExecutionRecord> ApplyFilters(IQueryable<RequestExecutionRecord> query, RequestHistoryFilter filter)
    {
        if (filter.FromUtc is not null && filter.ToUtc is not null && filter.FromUtc > filter.ToUtc)
            throw new ArgumentException("Invalid request history date range.", nameof(filter));
        if (filter.FromUtc is DateTimeOffset from) query = query.Where(item => item.StartedAtUtc >= from);
        if (filter.ToUtc is DateTimeOffset to) query = query.Where(item => item.StartedAtUtc <= to);
        if (filter.Status is RequestLifecycleStatus status) query = query.Where(item => item.LifecycleStatus == status);
        if (!string.IsNullOrWhiteSpace(filter.EntityCode))
        {
            var entity = BoundFilter(filter.EntityCode, 40); query = query.Where(item => item.EntityCode == entity);
        }
        if (!string.IsNullOrWhiteSpace(filter.ServiceCode))
        {
            var service = BoundFilter(filter.ServiceCode, 120); query = query.Where(item => item.ServiceCode == service);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = BoundFilter(filter.Search, 100);
            query = query.Where(item => item.RequestId.Contains(search) || item.CorrelationId.Contains(search)
                || item.ServiceCode.Contains(search) || item.EntityCode.Contains(search) || item.OutcomeCode.Contains(search));
        }
        return query;
    }

    private string? Unprotect(byte[]? protectedPayload)
    {
        if (protectedPayload is null || protectedPayload.Length == 0) return null;
        try { return Encoding.UTF8.GetString(_protector.Unprotect(protectedPayload)); }
        catch { return null; }
    }

    private static RequestHistoryItem Map(RequestExecutionRecord item) => new(
        item.RequestId, item.CorrelationId, item.ActorUserId, item.DepartmentCode, item.EntityCode, item.ServiceCode,
        item.ServiceVersion, item.EnvironmentCode, item.MaskedInputJson, item.LifecycleStatus, item.OutcomeCode,
        item.HttpStatusCode, item.DurationMilliseconds, item.Attempts, item.ResultSuppressedByBound, item.StartedAtUtc, item.CompletedAtUtc);

    private static byte[] BuildCsv(IReadOnlyList<RequestHistoryItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RequestId,CorrelationId,Department,Entity,Service,Version,Environment,Status,Outcome,HttpStatus,DurationMs,Attempts,StartedUtc,CompletedUtc,MaskedInput");
        foreach (var item in items)
        {
            var values = Columns(item).Select(CsvCell);
            builder.AppendLine(string.Join(',', values));
        }
        return new UTF8Encoding(true).GetBytes(builder.ToString());
    }

    private static byte[] BuildExcel(IReadOnlyList<RequestHistoryItem> items)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            AddZipText(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            AddZipText(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            AddZipText(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Requests\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            AddZipText(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            var sheet = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            var headers = new[] { "RequestId","CorrelationId","Department","Entity","Service","Version","Environment","Status","Outcome","HttpStatus","DurationMs","Attempts","StartedUtc","CompletedUtc","MaskedInput" };
            AppendExcelRow(sheet, headers);
            foreach (var item in items) AppendExcelRow(sheet, Columns(item));
            sheet.Append("</sheetData></worksheet>");
            AddZipText(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return stream.ToArray();
    }

    private static byte[] BuildPdf(IReadOnlyList<RequestHistoryItem> items)
    {
        var lines = new List<string> { "GSIP Request History" };
        lines.AddRange(items.Select(item => $"{item.StartedAtUtc:u} | {item.RequestId} | {item.ServiceCode} | {item.Status} | {item.OutcomeCode}"));
        var content = new StringBuilder("BT /F1 8 Tf 36 806 Td 11 TL ");
        foreach (var line in lines.Take(72)) content.Append('(').Append(PdfText(line)).Append(") Tj T* ");
        content.Append("ET");
        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream"
        };
        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        output.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) output.Append(offset.ToString("D10")).Append(" 00000 n \n");
        output.Append("trailer << /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string BuildPrintHtml(IReadOnlyList<RequestHistoryItem> items)
    {
        var rows = string.Join("", items.Select(item => $"<tr><td>{WebUtility.HtmlEncode(item.StartedAtUtc.ToString("u"))}</td><td>{WebUtility.HtmlEncode(item.RequestId)}</td><td>{WebUtility.HtmlEncode(item.ServiceCode)}</td><td>{WebUtility.HtmlEncode(item.Status.ToString())}</td><td>{WebUtility.HtmlEncode(item.OutcomeCode)}</td></tr>"));
        return "<!doctype html><html dir=\"auto\"><head><meta charset=\"utf-8\"><title>GSIP Request History</title></head><body><h1>GSIP Request History / سجل الطلبات</h1><table><thead><tr><th>Time</th><th>Request ID</th><th>Service</th><th>Status</th><th>Outcome</th></tr></thead><tbody>" + rows + "</tbody></table><script>window.print()</script></body></html>";
    }

    private static string[] Columns(RequestHistoryItem item) =>
    [
        item.RequestId, item.CorrelationId, item.DepartmentCode ?? string.Empty, item.EntityCode, item.ServiceCode,
        item.ServiceVersion.ToString(), item.EnvironmentCode, item.Status.ToString(), item.OutcomeCode,
        item.HttpStatusCode?.ToString() ?? string.Empty, item.DurationMilliseconds.ToString(), item.Attempts.ToString(),
        item.StartedAtUtc.ToString("O"), item.CompletedAtUtc?.ToString("O") ?? string.Empty, item.MaskedInputJson
    ];

    private static string CsvCell(string value)
    {
        var safe = PreventSpreadsheetFormula(value);
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }

    private static string PreventSpreadsheetFormula(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static void AppendExcelRow(StringBuilder builder, IEnumerable<string> values)
    {
        builder.Append("<row>");
        foreach (var value in values)
            builder.Append("<c t=\"inlineStr\"><is><t>").Append(SecurityElement.Escape(PreventSpreadsheetFormula(value)) ?? string.Empty).Append("</t></is></c>");
        builder.Append("</row>");
    }

    private static void AddZipText(ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string PdfText(string value)
    {
        var ascii = new string(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var id) || id == Guid.Empty) throw new RequestHistoryAccessDeniedException();
        return id;
    }

    private static string BoundFilter(string value, int max) => value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
    private static string? NormalizeDepartment(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
