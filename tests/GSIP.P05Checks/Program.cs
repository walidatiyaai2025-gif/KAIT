using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Application.Metadata;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("GSIP_P05_SQL");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("GSIP_P05_SQL is required.");

var options = new DbContextOptionsBuilder<GsipDbContext>().UseSqlServer(connection).Options;
await using var db = new GsipDbContext(options);
await db.Database.EnsureDeletedAsync();
try
{
    await db.Database.MigrateAsync();
    var catalog = new MetadataCatalogService(db, new FixedClock(DateTimeOffset.Parse("2026-09-08T17:20:00Z")));

    var entity = await catalog.CreateEntityAsync(new EntityInput("SYNTH", "جهة تجريبية", "Synthetic Authority", "/img/synth.svg", true, 10));
    Assert(entity.Code == "SYNTH", "Entity code normalization failed.");

    var service = await catalog.CreateServiceAsync(entity.Id, BuildService("SAMPLE-CHECK", "Synthetic Service"));
    Assert(service.Version == 1 && service.IsCurrent, "Initial service version is invalid.");
    var configs = await db.ServiceEnvironmentConfigs.AsNoTracking().Where(x => x.ServiceId == service.Id).ToListAsync();
    Assert(configs.Count == 2, "Service must have exactly UAT and Production configurations.");
    Assert(configs.Select(x => x.EnvironmentId).Distinct().Count() == 2, "UAT and Production must be isolated rows.");
    Assert(configs.Select(x => x.BaseUrl).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "UAT and Production endpoint bindings were unexpectedly shared.");
    Assert(await db.ServiceFieldDefinitions.CountAsync(x => x.ServiceId == service.Id) == 2, "Metadata-driven fields were not persisted.");
    Assert(await db.ResultMappingDefinitions.CountAsync(x => x.ServiceId == service.Id) == 2, "Result mappings were not persisted.");

    await ExpectInvalidAsync(() => catalog.CreateServiceAsync(entity.Id, BuildService("HTTP-PROD", "Bad Production", productionUrl: "http://prod.example.invalid")), "Production HTTP was accepted.");
    await ExpectInvalidAsync(() => catalog.CreateServiceAsync(entity.Id, BuildService("NO-CERT", "Bad Certificate", validateProductionCertificate: false)), "Production certificate validation was allowed to be disabled.");
    await ExpectInvalidAsync(() => catalog.CreateServiceAsync(entity.Id, BuildService("SECRET-HDR", "Secret Header", nonSecretHeadersJson: "{\"Authorization\":\"Bearer synthetic\"}")), "Secret-bearing Authorization header was accepted as metadata.");

    var rejectedCodes = new[] { "HTTP-PROD", "NO-CERT", "SECRET-HDR" };
    Assert(!db.ChangeTracker.Entries<CatalogService>().Any(entry => entry.State == EntityState.Added && rejectedCodes.Contains(entry.Entity.Code, StringComparer.OrdinalIgnoreCase)),
        "Rejected service definitions remained attached to the EF change tracker.");
    Assert(!await db.CatalogServices.AsNoTracking().AnyAsync(x => rejectedCodes.Contains(x.Code)),
        "Rejected service definitions were persisted immediately.");

    await catalog.MarkServiceUsedAsync(service.Id);
    var revised = await catalog.UpdateServiceAsync(service.Id, BuildService("SAMPLE-CHECK", "Synthetic Service Revised"));
    Assert(revised.Id != service.Id && revised.DefinitionKey == service.DefinitionKey && revised.Version == 2 && revised.IsCurrent,
        "Used service definition was silently mutated instead of versioned.");
    var old = await db.CatalogServices.AsNoTracking().SingleAsync(x => x.Id == service.Id);
    Assert(!old.IsCurrent && old.Version == 1 && old.NameEn == "Synthetic Service", "Historical service definition was not preserved.");
    Assert(!await db.CatalogServices.AsNoTracking().AnyAsync(x => rejectedCodes.Contains(x.Code)),
        "A later valid SaveChanges persisted previously rejected service definitions.");

    var exported = await catalog.ExportJsonAsync();
    Assert(exported.Contains("\"schemaVersion\": 1", StringComparison.Ordinal), "Export schema version is missing.");
    Assert(!exported.Contains("Bearer synthetic", StringComparison.OrdinalIgnoreCase), "Export leaked a rejected synthetic secret value.");
    var exportedPackage = JsonSerializer.Deserialize<MetadataPackage>(exported, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("Exported metadata package could not be deserialized for round-trip diagnostics.");
    Console.WriteLine($"P05_EXPORT_DIAGNOSTIC entities={exportedPackage.Entities.Count}");
    foreach (var exportedEntity in exportedPackage.Entities)
    {
        Console.WriteLine($"P05_EXPORT_ENTITY code={exportedEntity.Code} services={exportedEntity.Services.Count}");
        foreach (var exportedService in exportedEntity.Services)
        {
            var environmentCodes = string.Join(",", exportedService.EnvironmentConfigs.Select(x => x.EnvironmentCode));
            Console.WriteLine($"P05_EXPORT_SERVICE code={exportedService.Code} environments={exportedService.EnvironmentConfigs.Count} [{environmentCodes}] fields={exportedService.Fields.Count} mappings={exportedService.ResultMappings.Count}");
        }
    }
    Assert(exportedPackage.Entities.Count == 1, "Export must contain exactly the synthetic entity.");
    Assert(exportedPackage.Entities[0].Services.Count == 1, "Export must contain exactly the current synthetic service definition.");
    Assert(exportedPackage.Entities[0].Services[0].EnvironmentConfigs.Count == 2,
        "Exported current service must contain exactly two environment bindings before import.");
    Assert(exportedPackage.Entities[0].Services[0].EnvironmentConfigs.Select(x => x.EnvironmentCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
        "Exported UAT and Production bindings must remain distinct before import.");

    var imported = await catalog.ImportJsonAsync(exported);
    Assert(imported.EntitiesProcessed == 1 && imported.ServicesProcessed == 1, "Schema-governed export/import round trip failed.");

    var controllerFiles = Directory.GetFiles(Path.Combine("src", "GSIP.Web", "Controllers"), "*.cs", SearchOption.AllDirectories);
    var viewFiles = Directory.GetFiles(Path.Combine("src", "GSIP.Web", "Views"), "*.cshtml", SearchOption.AllDirectories);
    Assert(!controllerFiles.Concat(viewFiles).Any(path => File.ReadAllText(path).Contains("SAMPLE-CHECK", StringComparison.OrdinalIgnoreCase)),
        "Synthetic service unexpectedly required custom controller/view code.");

    var evidenceDirectory = Path.Combine("artifacts", "p05-evidence");
    Directory.CreateDirectory(evidenceDirectory);
    var manifest = new
    {
        phase = "P05",
        sampleEntity = entity.Code,
        sampleService = revised.Code,
        versionsVerified = new[] { 1, 2 },
        environments = new[] { CatalogEnvironmentCodes.Uat, CatalogEnvironmentCodes.Production },
        independentBindings = true,
        productionHttpsEnforced = true,
        certificateValidationEnforced = true,
        secretHeaderRejection = true,
        rejectedDefinitionAtomicity = true,
        jsonSchemaRoundTrip = true,
        customServiceControllerOrViewRequired = false
    };
    await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("P05_METADATA_CHECKS=PASS");
}
finally
{
    await db.Database.EnsureDeletedAsync();
}

static ServiceInput BuildService(string code, string nameEn, string productionUrl = "https://prod.example.invalid", bool validateProductionCertificate = true, string nonSecretHeadersJson = "{\"X-Correlation-Mode\":\"generated\"}") =>
    new(code, "خدمة تجريبية", nameEn, "تعريف تجريبي", "Synthetic metadata definition", true,
    [
        new ServiceEnvironmentInput("UAT", "https://uat.example.invalid", "/api/sample", "POST", "application/json", nonSecretHeadersJson, 30, "SystemDefault", true, "", "/health", "HEAD", true, null),
        new ServiceEnvironmentInput("Production", productionUrl, "/api/sample", "POST", "application/json", nonSecretHeadersJson, 45, "SystemDefault", validateProductionCertificate, "", "/health", "HEAD", true, null)
    ],
    [
        new ServiceFieldInput("civil-id", "الرقم المدني", "Civil ID", "text", true, "^[0-9]{12}$", null, null, 12, 12, "[]", 10, true, "Last4"),
        new ServiceFieldInput("channel", "القناة", "Channel", "select", false, "", null, null, null, null, "[\"web\",\"mobile\"]", 20, false, "None")
    ],
    [
        new ResultMappingInput("$.data.status", "الحالة", "Status", "text", "", false, 10),
        new ResultMappingInput("$.data.reference", "المرجع", "Reference", "text", "", true, 20)
    ]);

static async Task ExpectInvalidAsync(Func<Task> action, string message)
{
    try { await action(); }
    catch (InvalidOperationException) { return; }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
