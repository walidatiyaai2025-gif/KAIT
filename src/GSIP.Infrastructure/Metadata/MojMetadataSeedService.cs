using System.Data;
using GSIP.Application.Abstractions;
using GSIP.Application.Setup;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GSIP.Infrastructure.Metadata;

public sealed class MojMetadataSeedService(GsipDbContext db, ISystemClock clock)
{
    public const int DefinitionVersion = 1;
    public const string EntityCode = "MOJ";
    public const string ProductionGatewayPrefix = "https://moj.api.cait.gov.kw";
    public const string Api129UatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage";
    public const string Api129TargetPath = "/marriageCasesAPIGEE";
    public const string Api129TokenPath = "/genToken";

    public static IReadOnlyList<string> CanonicalServiceCodes { get; } = Array.AsReadOnly(new[]
    {
        "MARRIAGECASES",
        "ISSINGLEBASIC",
        "MARRIAGECOUPLELASTCASE",
        "FAMILYJUDGMENTTEXT",
        "PROCURATIONSTATUS"
    });

    private static readonly Guid MojEntityId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000901");
    private static readonly Guid Api129FieldId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000951");
    private static readonly Guid Api129AuthProfileId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000952");
    private static readonly Guid Api129AuthBindingId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000953");

    private static readonly ServiceSeedDefinition[] Definitions =
    [
        new(
            129,
            "MARRIAGECASES",
            "خدمة حالات الزواج",
            "Marriage Cases Service",
            "خدمة وزارة العدل API 129. عقد UAT مثبت جزئيًا فقط؛ تفاصيل النتائج والإنتاج غير المثبتة تظل مؤجلة.",
            "MOJ API 129. Only the evidenced UAT contract is seeded; unproven result and Production details remain deferred.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000911"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000921"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000931"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000941")),
        new(
            132,
            "ISSINGLEBASIC",
            "خدمة الاستعلام الأساسي عن حالة عدم الزواج",
            "Is Single Basic Service",
            "خدمة وزارة العدل API 132. تفاصيل التشغيل الرسمية غير متاحة حاليًا وتظل مؤجلة ومغلقة.",
            "MOJ API 132. Operation-level contract details are currently unavailable and remain deferred and disabled.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000912"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000922"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000932"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000942")),
        new(
            130,
            "MARRIAGECOUPLELASTCASE",
            "خدمة آخر حالة زواج للزوجين",
            "Marriage Couple Last Case Service",
            "خدمة وزارة العدل API 130. تفاصيل التشغيل الرسمية غير متاحة حاليًا وتظل مؤجلة ومغلقة.",
            "MOJ API 130. Operation-level contract details are currently unavailable and remain deferred and disabled.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000913"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000923"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000933"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000943")),
        new(
            196,
            "FAMILYJUDGMENTTEXT",
            "خدمة نص حكم الأسرة",
            "Family Judgment Text Service",
            "خدمة وزارة العدل API 196. تفاصيل التشغيل الرسمية غير متاحة حاليًا وتظل مؤجلة ومغلقة.",
            "MOJ API 196. Operation-level contract details are currently unavailable and remain deferred and disabled.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000914"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000924"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000934"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000944")),
        new(
            134,
            "PROCURATIONSTATUS",
            "خدمة حالة التوكيل",
            "Procuration Status Service",
            "خدمة وزارة العدل API 134. تفاصيل التشغيل الرسمية غير متاحة حاليًا وتظل مؤجلة ومغلقة.",
            "MOJ API 134. Operation-level contract details are currently unavailable and remain deferred and disabled.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000915"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000925"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000935"),
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000945"))
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var environments = await db.CatalogEnvironments
            .Where(environment => environment.Id == CatalogEnvironmentCodes.UatId
                || environment.Id == CatalogEnvironmentCodes.ProductionId)
            .ToListAsync(cancellationToken);
        var uat = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.UatId);
        var production = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.ProductionId);
        if (uat is null || production is null
            || !string.Equals(uat.Code, CatalogEnvironmentCodes.Uat, StringComparison.Ordinal)
            || !string.Equals(production.Code, CatalogEnvironmentCodes.Production, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical UAT and Production metadata environments are required before MOJ seeding.");
        }

        var entityMatches = await db.CatalogEntities
            .Where(entity => entity.Id == MojEntityId || entity.Code == EntityCode)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (entityMatches.Count > 1)
        {
            throw new InvalidOperationException("MOJ metadata identity is ambiguous; automatic seeding stopped without overwriting existing metadata.");
        }

        var entity = entityMatches.SingleOrDefault();
        if (entity is null)
        {
            entity = new CatalogEntity
            {
                Id = MojEntityId,
                Code = EntityCode,
                NameAr = "وزارة العدل",
                NameEn = "Ministry of Justice",
                Logo = string.Empty,
                Active = true,
                DisplayOrder = 1,
                CreatedAtUtc = clock.UtcNow,
                UpdatedAtUtc = clock.UtcNow
            };
            db.CatalogEntities.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var definition in Definitions)
        {
            var alreadyExists = await db.CatalogServices.AnyAsync(service =>
                service.EntityId == entity.Id
                && (service.DefinitionKey == definition.DefinitionKey || service.Code == definition.Code),
                cancellationToken);
            if (alreadyExists)
            {
                continue;
            }

            var service = BuildService(entity.Id, definition);
            db.CatalogServices.Add(service);
            await db.SaveChangesAsync(cancellationToken);

            if (definition.ApiId == 129)
            {
                await AddApi129AuthProfileAsync(service, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private CatalogService BuildService(Guid entityId, ServiceSeedDefinition definition)
    {
        var service = new CatalogService
        {
            Id = definition.ServiceId,
            DefinitionKey = definition.DefinitionKey,
            EntityId = entityId,
            Code = definition.Code,
            NameAr = definition.NameAr,
            NameEn = definition.NameEn,
            DescriptionAr = definition.DescriptionAr,
            DescriptionEn = definition.DescriptionEn,
            Active = true,
            Version = DefinitionVersion,
            IsCurrent = true,
            FirstUsedAtUtc = null,
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };

        var uatConfig = definition.ApiId == 129
            ? BuildApi129UatConfig(definition)
            : BuildDeferredConfig(definition.ServiceId, definition.UatConfigId, CatalogEnvironmentCodes.UatId, false);
        var productionConfig = BuildDeferredConfig(
            definition.ServiceId,
            definition.ProductionConfigId,
            CatalogEnvironmentCodes.ProductionId,
            true);
        service.EnvironmentConfigs.Add(uatConfig);
        service.EnvironmentConfigs.Add(productionConfig);

        if (definition.ApiId == 129)
        {
            service.Fields.Add(new ServiceFieldDefinition
            {
                Id = Api129FieldId,
                ServiceId = service.Id,
                Key = "civilId",
                LabelAr = "civilId",
                LabelEn = "civilId",
                FieldType = "deferred",
                Required = true,
                Regex = string.Empty,
                Minimum = null,
                Maximum = null,
                MinLength = null,
                MaxLength = null,
                OptionsJson = "[]",
                DisplayOrder = 10,
                Sensitive = true,
                Masking = "Last4"
            });
        }

        return service;
    }

    private static ServiceEnvironmentConfig BuildApi129UatConfig(ServiceSeedDefinition definition) => new()
    {
        Id = definition.UatConfigId,
        ServiceId = definition.ServiceId,
        EnvironmentId = CatalogEnvironmentCodes.UatId,
        BaseUrl = Api129UatBaseUrl,
        RelativePath = Api129TargetPath,
        HttpMethod = "POST",
        ContentType = "application/x-www-form-urlencoded",
        NonSecretHeadersJson = "{\"X-GSIP-TokenEndpointPath\":\"/genToken\"}",
        TimeoutSeconds = 30,
        TlsPolicy = "SystemDefault",
        ValidateServerCertificate = true,
        ProxyUrl = string.Empty,
        HealthPath = string.Empty,
        HealthMethod = string.Empty,
        Active = false,
        LastTestedAtUtc = null,
        LastTestStatus = "DEFERRED_CREDENTIAL_CONFIGURATION",
        AuthProfileId = null
    };

    private static ServiceEnvironmentConfig BuildDeferredConfig(
        Guid serviceId,
        Guid configId,
        Guid environmentId,
        bool production) => new()
    {
        Id = configId,
        ServiceId = serviceId,
        EnvironmentId = environmentId,
        BaseUrl = production ? ProductionGatewayPrefix : string.Empty,
        RelativePath = string.Empty,
        HttpMethod = string.Empty,
        ContentType = string.Empty,
        NonSecretHeadersJson = "{}",
        TimeoutSeconds = 30,
        TlsPolicy = "SystemDefault",
        ValidateServerCertificate = true,
        ProxyUrl = string.Empty,
        HealthPath = string.Empty,
        HealthMethod = string.Empty,
        Active = false,
        LastTestedAtUtc = null,
        LastTestStatus = "DEFERRED_EXTERNAL_CONTRACT",
        AuthProfileId = null
    };

    private async Task AddApi129AuthProfileAsync(CatalogService service, CancellationToken cancellationToken)
    {
        var uatConfig = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var profile = new AuthProfile
        {
            Id = Api129AuthProfileId,
            OwnerServiceId = service.Id,
            OwnerEnvironmentId = CatalogEnvironmentCodes.UatId,
            Name = "MOJ API 129 UAT composite authentication",
            AuthType = AuthProfileType.TokenEndpoint,
            IsEnabled = false,
            Version = 1,
            CreatedBy = "P09MetadataSeed",
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        profile.Bindings.Add(new AuthProfileBinding
        {
            Id = Api129AuthBindingId,
            AuthProfileId = profile.Id,
            ServiceId = service.Id,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            IsShared = false,
            DecisionBy = "P09MetadataSeed",
            DecisionReason = "Official API 129 UAT authentication scope; credential material is intentionally absent from seed data.",
            DecisionAtUtc = clock.UtcNow
        });

        db.AuthProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        uatConfig.AuthProfileId = profile.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ServiceSeedDefinition(
        int ApiId,
        string Code,
        string NameAr,
        string NameEn,
        string DescriptionAr,
        string DescriptionEn,
        Guid ServiceId,
        Guid DefinitionKey,
        Guid UatConfigId,
        Guid ProductionConfigId);
}

internal sealed class MojMetadataBootstrapService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        var status = await setup.GetStatusAsync(cancellationToken);
        if (!status.IsCompleted)
        {
            return;
        }

        await scope.ServiceProvider.GetRequiredService<MojMetadataSeedService>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
