using System.Data;
using System.Security.Cryptography;
using System.Text;
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
    public const int DefinitionVersion = 2;
    public const string EntityCode = "MOJ";
    public const string ProductionGatewayPrefix = "https://moj.api.cait.gov.kw";
    public const string MarriageUatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage";
    public const string VerdictUatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/verdict";
    public const string ProcurationUatBaseUrl = "https://moj-uat.api-non-prod.cait.gov.kw/MOJProcuration/V1/api";

    public const string TokenPathMetadataKey = "X-GSIP-TokenEndpointPath";
    public const string TokenRequestContentTypeMetadataKey = "X-GSIP-TokenRequestContentType";
    public const string TokenResponsePathMetadataKey = "X-GSIP-TokenResponsePath";
    public const string TokenUsernameFieldMetadataKey = "X-GSIP-TokenUsernameField";
    public const string TokenPasswordFieldMetadataKey = "X-GSIP-TokenPasswordField";
    public const string TokenGehaFieldMetadataKey = "X-GSIP-TokenGehaField";
    public const string TokenDocumentedTtlSecondsMetadataKey = "X-GSIP-TokenDocumentedTtlSeconds";

    public static IReadOnlyList<string> CanonicalServiceCodes { get; } = Array.AsReadOnly(new[]
    {
        "MARRIAGECASES",
        "ISSINGLEBASIC",
        "MARRIAGECOUPLELASTCASE",
        "FAMILYJUDGMENTTEXT",
        "PROCURATIONSTATUS"
    });

    private static readonly Guid MojEntityId = Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000901");

    private static readonly ServiceSeedDefinition[] Definitions =
    [
        new(
            129,
            "MARRIAGECASES",
            "خدمة حالات الزواج",
            "Marriage Cases Service",
            "عقد UAT الرسمي API 129: استعلام حالات الزواج باستخدام الرقم المدني.",
            "Official API 129 UAT contract: retrieve marriage cases by civil ID.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000921"),
            MarriageUatBaseUrl,
            "/marriageCasesAPIGEE",
            "application/x-www-form-urlencoded",
            "/genToken",
            "application/x-www-form-urlencoded",
            "data",
            "username",
            "password",
            null,
            null,
            true,
            [
                F("civilId", "الرقم المدني", "Civil ID", true, true)
            ],
            [
                R("data[0].CIVILID", "الرقم المدني", "Civil ID", "text", true),
                R("data[0].CONTRACT_DATE", "تاريخ العقد", "Contract Date", "text", false),
                R("data[0].CONTRACT_NO", "رقم العقد", "Contract Number", "text", true),
                R("data[0].CONTRACT_TYPE", "نوع العقد", "Contract Type", "text", false),
                R("data[0].FEMALE_NAME", "اسم الزوجة", "Female Name", "text", true),
                R("data[0].FEM_CIVILID", "الرقم المدني للزوجة", "Female Civil ID", "text", true),
                R("data[0].LEGAL_PRIVACY", "الخصوصية القانونية", "Legal Privacy", "text", true),
                R("data[0].MALE_NAME", "اسم الزوج", "Male Name", "text", true),
                R("data[0].NO_DAT", "بيان الحالة", "Case Data", "text", false),
                R("data[0].marriedCouple", "زوجان", "Married Couple", "boolean", true),
                R("data[0].typPrf", "نوع الإثبات", "Proof Type", "text", false),
                R("errorDTO.errorCode", "رمز الخطأ", "Error Code", "text", false),
                R("errorDTO.errorDescription", "وصف الخطأ", "Error Description", "text", false),
                R("errorDTO.isClean", "حالة الخطأ", "Error Clean State", "boolean", false),
                R("errorDTO.civilID", "الرقم المدني المرتبط بالخطأ", "Error Civil ID", "text", true)
            ]),
        new(
            132,
            "ISSINGLEBASIC",
            "خدمة الاستعلام الأساسي عن حالة عدم الزواج",
            "Is Single Basic Service",
            "عقد UAT الرسمي API 132: التحقق من حالة عدم الزواج باستخدام الرقم المدني.",
            "Official API 132 UAT contract: check single/unmarried status by civil ID.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000922"),
            MarriageUatBaseUrl,
            "/isSingleBasicAPIGEE",
            "application/x-www-form-urlencoded",
            "/genToken",
            "application/x-www-form-urlencoded",
            "data",
            "username",
            "password",
            null,
            null,
            false,
            [F("civilId", "الرقم المدني", "Civil ID", true, true)],
            [
                R("data[0].FENATI_NAME", "اسم الجنسية بالإنجليزية", "Nationality Name (EN)", "text", true),
                R("data[0].NATI_NAME", "اسم الجنسية", "Nationality Name", "text", true),
                R("data[0].maritalStatus", "الحالة الاجتماعية", "Marital Status", "text", true),
                R("data[0].marriageFree", "حالة خلو الزواج", "Marriage Free", "text", true),
                R("data[0].marriedCouple", "زوجان", "Married Couple", "boolean", true),
                R("errorDTO.errorCode", "رمز الخطأ", "Error Code", "text", false),
                R("errorDTO.errorDescription", "وصف الخطأ", "Error Description", "text", false),
                R("errorDTO.isClean", "حالة الخطأ", "Error Clean State", "boolean", false),
                R("errorDTO.civilID", "الرقم المدني المرتبط بالخطأ", "Error Civil ID", "text", true)
            ]),
        new(
            130,
            "MARRIAGECOUPLELASTCASE",
            "خدمة آخر حالة زواج للزوجين",
            "Marriage Couple Last Case Service",
            "عقد UAT الرسمي API 130: آخر حالة زواج لزوجين باستخدام الرقمين المدنيين.",
            "Official API 130 UAT contract: retrieve the last marriage case for a couple.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000923"),
            MarriageUatBaseUrl,
            "/marriageCoupleLastAPIGEE",
            "application/x-www-form-urlencoded",
            "/genToken",
            "application/x-www-form-urlencoded",
            "data",
            "username",
            "password",
            null,
            null,
            false,
            [
                F("male_civilId", "الرقم المدني للزوج", "Male Civil ID", true, true),
                F("female_civilId", "الرقم المدني للزوجة", "Female Civil ID", true, true)
            ],
            [
                R("data[0].FENATI_NAME", "اسم الجنسية بالإنجليزية", "Nationality Name (EN)", "text", true),
                R("data[0].NATI_NAME", "اسم الجنسية", "Nationality Name", "text", true),
                R("data[0].NO_DAT", "بيان الحالة", "Case Data", "text", false),
                R("data[0].maritalStatus", "الحالة الاجتماعية", "Marital Status", "text", true),
                R("data[0].marriedCouple", "زوجان", "Married Couple", "boolean", true),
                R("errorDTO.errorCode", "رمز الخطأ", "Error Code", "text", false),
                R("errorDTO.errorDescription", "وصف الخطأ", "Error Description", "text", false),
                R("errorDTO.isClean", "حالة الخطأ", "Error Clean State", "boolean", false),
                R("errorDTO.civilID", "الرقم المدني المرتبط بالخطأ", "Error Civil ID", "text", true)
            ]),
        new(
            196,
            "FAMILYJUDGMENTTEXT",
            "خدمة نص حكم الأسرة",
            "Family Judgment Text Service",
            "عقد UAT الرسمي API 196: استرجاع نص الحكم لقضية. هدف UAT موثق كـ Mock للتحقق البنيوي فقط.",
            "Official API 196 UAT contract: retrieve family judgment text. CAIT documents the UAT try-out as a mock target for structural validation only.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000924"),
            VerdictUatBaseUrl,
            "/familyJudgmentText",
            "application/json",
            "/token",
            "application/json",
            "token",
            "username",
            "password",
            null,
            null,
            false,
            [
                F("caseNo", "رقم القضية", "Case Number", false, true),
                F("type", "نوع المحكمة", "Court Type", false, false)
            ],
            [R("data.VRDESC", "نص الحكم", "Judgment Text", "text", true)]),
        new(
            134,
            "PROCURATIONSTATUS",
            "خدمة حالة التوكيل",
            "Procuration Status Service",
            "عقد UAT الرسمي API 134: الاستعلام عن حالة التوكيل. هدف UAT موثق كـ Mock للتحقق البنيوي فقط.",
            "Official API 134 UAT contract: retrieve procuration status. CAIT documents the UAT try-out as a mock target for structural validation only.",
            Guid.Parse("6a1e6cbb-1d3d-43c0-87bb-000000000925"),
            ProcurationUatBaseUrl,
            "/Procuration/ProcurationStatus",
            "application/json",
            "/Authenticate/Token",
            "application/json",
            "token",
            "UserName",
            "Password",
            "Geha",
            21600,
            false,
            [
                F("CivilClient", "الرقم المدني للموكل", "Client Civil ID", false, true),
                F("CivilAgent", "الرقم المدني للوكيل", "Agent Civil ID", false, true),
                F("year", "سنة التوكيل", "Procuration Year", false, false),
                F("Number", "الرقم المسلسل للتوكيل", "Procuration Serial Number", false, true)
            ],
            [
                R("Status", "الحالة", "Status", "text", false),
                R("Message", "الرسالة", "Message", "text", false),
                R("Data.type", "نوع التوكيل", "Procuration Type", "text", false),
                R("Data.Status", "حالة التوكيل", "Procuration Status", "text", false)
            ])
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var environments = await db.CatalogEnvironments
            .Where(environment => environment.Id == CatalogEnvironmentCodes.UatId || environment.Id == CatalogEnvironmentCodes.ProductionId)
            .ToListAsync(cancellationToken);
        var uat = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.UatId);
        var production = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.ProductionId);
        if (uat is null || production is null
            || !string.Equals(uat.Code, CatalogEnvironmentCodes.Uat, StringComparison.Ordinal)
            || !string.Equals(production.Code, CatalogEnvironmentCodes.Production, StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical UAT and Production metadata environments are required before MOJ seeding.");

        var entityMatches = await db.CatalogEntities.Where(entity => entity.Id == MojEntityId || entity.Code == EntityCode).Take(2).ToListAsync(cancellationToken);
        if (entityMatches.Count > 1)
            throw new InvalidOperationException("MOJ metadata identity is ambiguous; automatic seeding stopped without overwriting existing metadata.");

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
            await EnsureCurrentVersionAsync(entity.Id, definition, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureCurrentVersionAsync(Guid entityId, ServiceSeedDefinition definition, CancellationToken cancellationToken)
    {
        var versions = await db.CatalogServices
            .Include(service => service.EnvironmentConfigs)
            .Include(service => service.Fields)
            .Include(service => service.ResultMappings)
            .Where(service => service.DefinitionKey == definition.DefinitionKey || (service.EntityId == entityId && service.Code == definition.Code))
            .OrderByDescending(service => service.Version)
            .ToListAsync(cancellationToken);

        var current = versions.SingleOrDefault(service => service.IsCurrent);
        if (current is not null && current.Version >= DefinitionVersion)
            return;

        if (current is not null)
        {
            if (!IsSafeLegacySeed(current, definition.ApiId))
                return; // Preserve owner-used/edited metadata. An administrator can intentionally reconcile it later.
            current.IsCurrent = false;
            current.Active = false;
            current.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var service = BuildService(entityId, definition);
        db.CatalogServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        await AddAuthProfileAsync(service, definition, cancellationToken);
    }

    private static bool IsSafeLegacySeed(CatalogService service, int apiId)
    {
        if (service.Version != 1 || service.FirstUsedAtUtc is not null)
            return false;
        var uat = service.EnvironmentConfigs.SingleOrDefault(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        var production = service.EnvironmentConfigs.SingleOrDefault(config => config.EnvironmentId == CatalogEnvironmentCodes.ProductionId);
        if (uat is null || production is null || production.Active || production.BaseUrl != ProductionGatewayPrefix)
            return false;
        if (apiId == 129)
            return !uat.Active && uat.BaseUrl == MarriageUatBaseUrl && uat.RelativePath == "/marriageCasesAPIGEE";
        return !uat.Active && string.IsNullOrEmpty(uat.BaseUrl) && string.IsNullOrEmpty(uat.RelativePath)
            && service.Fields.Count == 0 && service.ResultMappings.Count == 0;
    }

    private CatalogService BuildService(Guid entityId, ServiceSeedDefinition definition)
    {
        var serviceId = StableGuid($"service:v2:{definition.ApiId}");
        var service = new CatalogService
        {
            Id = serviceId,
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

        service.EnvironmentConfigs.Add(BuildUatConfig(serviceId, definition));
        service.EnvironmentConfigs.Add(BuildProductionConfig(serviceId, definition));

        var order = 10;
        foreach (var field in definition.Fields)
        {
            service.Fields.Add(new ServiceFieldDefinition
            {
                Id = StableGuid($"field:v2:{definition.ApiId}:{field.Key}"),
                ServiceId = serviceId,
                Key = field.Key,
                LabelAr = field.LabelAr,
                LabelEn = field.LabelEn,
                FieldType = "text",
                Required = field.Required,
                Regex = string.Empty,
                Minimum = null,
                Maximum = null,
                MinLength = null,
                MaxLength = null,
                OptionsJson = "[]",
                DisplayOrder = order,
                Sensitive = field.Sensitive,
                Masking = field.Sensitive ? "Last4" : "None"
            });
            order += 10;
        }

        order = 10;
        foreach (var mapping in definition.ResultMappings)
        {
            service.ResultMappings.Add(new ResultMappingDefinition
            {
                Id = StableGuid($"result:v2:{definition.ApiId}:{mapping.SourcePath}"),
                ServiceId = serviceId,
                SourcePath = mapping.SourcePath,
                LabelAr = mapping.LabelAr,
                LabelEn = mapping.LabelEn,
                ResultType = mapping.ResultType,
                Formatter = string.Empty,
                Sensitive = mapping.Sensitive,
                DisplayOrder = order
            });
            order += 10;
        }

        return service;
    }

    private static ServiceEnvironmentConfig BuildUatConfig(Guid serviceId, ServiceSeedDefinition definition) => new()
    {
        Id = StableGuid($"config:uat:v2:{definition.ApiId}"),
        ServiceId = serviceId,
        EnvironmentId = CatalogEnvironmentCodes.UatId,
        BaseUrl = definition.UatBaseUrl,
        RelativePath = definition.RelativePath,
        HttpMethod = "POST",
        ContentType = definition.ContentType,
        NonSecretHeadersJson = BuildTokenMetadata(definition),
        TimeoutSeconds = 30,
        TlsPolicy = "SystemDefault",
        ValidateServerCertificate = true,
        ProxyUrl = string.Empty,
        HealthPath = string.Empty,
        HealthMethod = string.Empty,
        Active = false,
        LastTestedAtUtc = null,
        LastTestStatus = "CONTRACT_READY_CREDENTIALS_REQUIRED",
        AuthProfileId = null
    };

    private static ServiceEnvironmentConfig BuildProductionConfig(Guid serviceId, ServiceSeedDefinition definition) => new()
    {
        Id = StableGuid($"config:production:v2:{definition.ApiId}"),
        ServiceId = serviceId,
        EnvironmentId = CatalogEnvironmentCodes.ProductionId,
        BaseUrl = ProductionGatewayPrefix,
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
        LastTestStatus = "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT",
        AuthProfileId = null
    };

    private async Task AddAuthProfileAsync(CatalogService service, ServiceSeedDefinition definition, CancellationToken cancellationToken)
    {
        var profileId = StableGuid($"auth:v2:{definition.ApiId}:uat");
        var bindingId = StableGuid($"auth-binding:v2:{definition.ApiId}:uat");
        var profile = new AuthProfile
        {
            Id = profileId,
            OwnerServiceId = service.Id,
            OwnerEnvironmentId = CatalogEnvironmentCodes.UatId,
            Name = $"MOJ API {definition.ApiId} UAT token authentication",
            AuthType = AuthProfileType.TokenEndpoint,
            IsEnabled = false,
            Version = 1,
            CreatedBy = "P09OfficialContractSeed",
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        profile.Bindings.Add(new AuthProfileBinding
        {
            Id = bindingId,
            AuthProfileId = profile.Id,
            ServiceId = service.Id,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            IsShared = false,
            DecisionBy = "P09OfficialContractSeed",
            DecisionReason = $"Independent official UAT authentication scope for MOJ API {definition.ApiId}; no credential material is seeded.",
            DecisionAtUtc = clock.UtcNow
        });
        db.AuthProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        uat.AuthProfileId = profile.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildTokenMetadata(ServiceSeedDefinition definition)
    {
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TokenPathMetadataKey] = definition.TokenPath,
            [TokenRequestContentTypeMetadataKey] = definition.TokenContentType,
            [TokenResponsePathMetadataKey] = definition.TokenResponsePath,
            [TokenUsernameFieldMetadataKey] = definition.TokenUsernameField,
            [TokenPasswordFieldMetadataKey] = definition.TokenPasswordField
        };
        if (!string.IsNullOrWhiteSpace(definition.TokenGehaField))
            items[TokenGehaFieldMetadataKey] = definition.TokenGehaField;
        if (definition.DocumentedTokenTtlSeconds is int ttl)
            items[TokenDocumentedTtlSecondsMetadataKey] = ttl;
        if (definition.ApiKeyOperationProven)
            items["X-GSIP-TokenApiKeyRequired"] = true;
        return System.Text.Json.JsonSerializer.Serialize(items);
    }

    private static Guid StableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("GSIP:P09:" + key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static FieldSeed F(string key, string labelAr, string labelEn, bool required, bool sensitive) => new(key, labelAr, labelEn, required, sensitive);
    private static ResultSeed R(string path, string labelAr, string labelEn, string type, bool sensitive) => new(path, labelAr, labelEn, type, sensitive);

    private sealed record ServiceSeedDefinition(
        int ApiId,
        string Code,
        string NameAr,
        string NameEn,
        string DescriptionAr,
        string DescriptionEn,
        Guid DefinitionKey,
        string UatBaseUrl,
        string RelativePath,
        string ContentType,
        string TokenPath,
        string TokenContentType,
        string TokenResponsePath,
        string TokenUsernameField,
        string TokenPasswordField,
        string? TokenGehaField,
        int? DocumentedTokenTtlSeconds,
        bool ApiKeyOperationProven,
        IReadOnlyList<FieldSeed> Fields,
        IReadOnlyList<ResultSeed> ResultMappings);

    private sealed record FieldSeed(string Key, string LabelAr, string LabelEn, bool Required, bool Sensitive);
    private sealed record ResultSeed(string SourcePath, string LabelAr, string LabelEn, string ResultType, bool Sensitive);
}

internal sealed class MojMetadataBootstrapService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        var status = await setup.GetStatusAsync(cancellationToken);
        if (!status.IsCompleted)
            return;
        await scope.ServiceProvider.GetRequiredService<MojMetadataSeedService>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
