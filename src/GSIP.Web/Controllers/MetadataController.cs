using System.Text;
using System.Text.Json;
using GSIP.Application.Authorization;
using GSIP.Application.Metadata;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

[Authorize]
[Route("metadata")]
public sealed class MetadataController(IMetadataCatalogService catalog) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [HttpGet("")]
    [Authorize(Policy = GsipPermissions.EntitiesView)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await catalog.GetSnapshotAsync(cancellationToken);
        var environmentCodes = snapshot.Environments.ToDictionary(x => x.Id, x => x.Code);
        var services = snapshot.Services.Select(service => new MetadataServiceViewModel(
            service.Id,
            service.EntityId,
            service.Code,
            service.NameAr,
            service.NameEn,
            service.Active,
            service.Version,
            service.FirstUsedAtUtc.HasValue,
            service.EnvironmentConfigs.Count,
            service.Fields.Count,
            service.ResultMappings.Count,
            JsonSerializer.Serialize(new ServiceInput(
                service.Code,
                service.NameAr,
                service.NameEn,
                service.DescriptionAr,
                service.DescriptionEn,
                service.Active,
                service.EnvironmentConfigs.OrderBy(x => environmentCodes[x.EnvironmentId]).Select(config => new ServiceEnvironmentInput(
                    environmentCodes[config.EnvironmentId], config.BaseUrl, config.RelativePath, config.HttpMethod, config.ContentType,
                    config.NonSecretHeadersJson, config.TimeoutSeconds, config.TlsPolicy, config.ValidateServerCertificate,
                    config.ProxyUrl, config.HealthPath, config.HealthMethod, config.Active, config.AuthProfileId)).ToList(),
                service.Fields.OrderBy(x => x.DisplayOrder).Select(field => new ServiceFieldInput(
                    field.Key, field.LabelAr, field.LabelEn, field.FieldType, field.Required, field.Regex, field.Minimum, field.Maximum,
                    field.MinLength, field.MaxLength, field.OptionsJson, field.DisplayOrder, field.Sensitive, field.Masking)).ToList(),
                service.ResultMappings.OrderBy(x => x.DisplayOrder).Select(mapping => new ResultMappingInput(
                    mapping.SourcePath, mapping.LabelAr, mapping.LabelEn, mapping.ResultType, mapping.Formatter,
                    mapping.Sensitive, mapping.DisplayOrder)).ToList()), JsonOptions))).ToList();

        return View(new MetadataDashboardViewModel
        {
            Entities = snapshot.Entities.Select(x => new MetadataEntityViewModel(x.Id, x.Code, x.NameAr, x.NameEn, x.Logo, x.Active, x.DisplayOrder)).ToList(),
            Services = services,
            ServiceTemplateJson = BuildTemplateJson(),
            ErrorMessage = TempData["MetadataError"] as string,
            SuccessMessage = TempData["MetadataSuccess"] as string
        });
    }

    [HttpPost("entities")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.EntitiesManage)]
    public async Task<IActionResult> CreateEntity(string code, string nameAr, string nameEn, string? logo, int displayOrder, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.CreateEntityAsync(new EntityInput(code, nameAr, nameEn, logo ?? string.Empty, true, displayOrder), cancellationToken);
            TempData["MetadataSuccess"] = "created";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("entities/{entityId:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.EntitiesManage)]
    public async Task<IActionResult> UpdateEntity(Guid entityId, string code, string nameAr, string nameEn, string? logo, bool active, int displayOrder, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.UpdateEntityAsync(entityId, new EntityInput(code, nameAr, nameEn, logo ?? string.Empty, active, displayOrder), cancellationToken);
            TempData["MetadataSuccess"] = "updated";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("entities/{entityId:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.EntitiesManage)]
    public async Task<IActionResult> DeactivateEntity(Guid entityId, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.DeactivateEntityAsync(entityId, cancellationToken);
            TempData["MetadataSuccess"] = "deactivated";
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("entities/{entityId:guid}/services")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.ServicesManage)]
    public async Task<IActionResult> CreateService(Guid entityId, string serviceJson, CancellationToken cancellationToken)
    {
        try
        {
            var input = DeserializeService(serviceJson);
            await catalog.CreateServiceAsync(entityId, input, cancellationToken);
            TempData["MetadataSuccess"] = "created";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("services/{serviceId:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.ServicesManage)]
    public async Task<IActionResult> UpdateService(Guid serviceId, string serviceJson, CancellationToken cancellationToken)
    {
        try
        {
            var input = DeserializeService(serviceJson);
            await catalog.UpdateServiceAsync(serviceId, input, cancellationToken);
            TempData["MetadataSuccess"] = "updated";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("services/{serviceId:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.ServicesManage)]
    public async Task<IActionResult> DeactivateService(Guid serviceId, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.DeactivateServiceAsync(serviceId, cancellationToken);
            TempData["MetadataSuccess"] = "deactivated";
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("export")]
    [Authorize(Policy = GsipPermissions.ServicesView)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var json = await catalog.ExportJsonAsync(cancellationToken);
        return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", $"gsip-metadata-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
    }

    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = GsipPermissions.EntitiesManage)]
    [Authorize(Policy = GsipPermissions.ServicesManage)]
    public async Task<IActionResult> Import(string metadataJson, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.ImportJsonAsync(metadataJson, cancellationToken);
            TempData["MetadataSuccess"] = "imported";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            TempData["MetadataError"] = "rejected";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("schema")]
    [Authorize(Policy = GsipPermissions.ServicesView)]
    public IActionResult Schema() => PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas", "gsip-metadata.schema.json"), "application/schema+json", "gsip-metadata.schema.json");

    private static ServiceInput DeserializeService(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 500_000) throw new ArgumentException("Service metadata is empty or too large.", nameof(json));
        return JsonSerializer.Deserialize<ServiceInput>(json, JsonOptions) ?? throw new InvalidOperationException("Service metadata is empty.");
    }

    private static string BuildTemplateJson() => JsonSerializer.Serialize(new ServiceInput(
        "SERVICE-CODE", "اسم الخدمة", "Service Name", "وصف الخدمة", "Service description", true,
        [
            new ServiceEnvironmentInput("UAT", "https://uat.example.gov.kw", "/api/service", "POST", "application/json", "{}", 30, "SystemDefault", true, "", "/health", "HEAD", true, null),
            new ServiceEnvironmentInput("Production", "https://api.example.gov.kw", "/api/service", "POST", "application/json", "{}", 45, "SystemDefault", true, "", "/health", "HEAD", false, null)
        ],
        [new ServiceFieldInput("civil-id", "الرقم المدني", "Civil ID", "text", true, "^[0-9]{12}$", null, null, 12, 12, "[]", 10, true, "Last4")],
        [new ResultMappingInput("$.data.status", "الحالة", "Status", "text", "", false, 10)]), JsonOptions);
}