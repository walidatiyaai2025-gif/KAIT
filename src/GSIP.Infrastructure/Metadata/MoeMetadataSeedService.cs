using System.Data;
using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Metadata;

/// <summary>
/// Materializes the owner-supplied MOE Student API UAT contracts. The contracts are
/// fail-closed: no credentials are seeded, every UAT configuration/profile starts
/// disabled, and Production is never inferred from UAT evidence.
/// </summary>
public sealed class MoeMetadataSeedService(GsipDbContext db, ISystemClock clock)
{
    public const int DefinitionVersion = 2;
    public const string EntityCode = "MOE";
    public const string UatBaseUrl = "https://moe-uat.api-non-prod.cait.gov.kw/Student-API/v1/studentdata";

    private static readonly ServiceDefinition[] Definitions =
    [
        new(
            "MOE_LAST_ACTIVE_RECORD",
            "خدمة آخر سجل دراسي نشط",
            "Last Active Record Service",
            "استرجاع آخر سجل أكاديمي كان الطالب مقيداً فيه، مع بيان حالة القيد الحالية.",
            "Retrieve the last academic record where the student was enrolled and indicate current-year enrollment status.",
            "/lastactive",
            [
                R("cid", "الرقم المدني", "Civil ID", "integer", true),
                R("name", "اسم الطالب", "Student Name", "text", true),
                R("school", "المدرسة", "School", "text", false),
                R("level", "المرحلة", "Level", "text", false),
                R("division", "الشعبة", "Division", "text", false),
                R("status", "الحالة", "Status", "text", false),
                R("year", "السنة الدراسية", "Academic Year", "integer", false),
                R("currentstatus", "حالة القيد الحالية", "Current Status", "text", false)
            ]),
        new(
            "MOE_LAST_STUDENT_RECORD",
            "خدمة آخر سجل للطالب",
            "Last Student Record Service",
            "استرجاع أحدث سجل أكاديمي للطالب بالرقم المدني مع بيان حالة القيد الحالية وتاريخ الانقطاع عند توفره.",
            "Fetch the student's most recent academic record by Civil ID, including current enrollment status and quit date when supplied.",
            "/last",
            [
                R("cid", "الرقم المدني", "Civil ID", "integer", true),
                R("name", "اسم الطالب", "Student Name", "text", true),
                R("school", "المدرسة", "School", "text", false),
                R("level", "المرحلة", "Level", "text", false),
                R("division", "الشعبة", "Division", "text", false),
                R("status", "الحالة", "Status", "text", false),
                R("year", "السنة الدراسية", "Academic Year", "integer", false),
                R("currentstatus", "حالة القيد الحالية", "Current Status", "text", false),
                R("quitdate", "تاريخ الانقطاع", "Quit Date", "text", false)
            ]),
        new(
            "MOE_LAST_SUCCESS_RECORD",
            "خدمة آخر سجل نجاح للطالب",
            "Last Success Record Service",
            "استرجاع آخر سنة أكاديمية أتم فيها الطالب الصف بنجاح وفق عقد UAT الرسمي.",
            "Return the student's last academic year in which the grade was successfully completed.",
            "/lastsuccess",
            [
                R("cid", "الرقم المدني", "Civil ID", "integer", true),
                R("name", "اسم الطالب", "Student Name", "text", true),
                R("school", "المدرسة", "School", "text", false),
                R("level", "المرحلة", "Level", "text", false),
                R("division", "الشعبة", "Division", "text", false),
                R("status", "الحالة", "Status", "text", false),
                R("year", "السنة الدراسية", "Academic Year", "integer", false),
                R("currentstatus", "حالة القيد الحالية", "Current Status", "text", false)
            ])
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await ValidateCanonicalEnvironmentsAsync(cancellationToken);
        var entity = await EnsureEntityAsync(cancellationToken);

        foreach (var definition in Definitions)
            await EnsureServiceAsync(entity, definition, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ValidateCanonicalEnvironmentsAsync(CancellationToken cancellationToken)
    {
        var environments = await db.CatalogEnvironments
            .Where(environment => environment.Id == CatalogEnvironmentCodes.UatId
                                  || environment.Id == CatalogEnvironmentCodes.ProductionId)
            .ToListAsync(cancellationToken);

        var uat = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.UatId);
        var production = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.ProductionId);
        if (uat is null || production is null
            || !string.Equals(uat.Code, CatalogEnvironmentCodes.Uat, StringComparison.Ordinal)
            || !string.Equals(production.Code, CatalogEnvironmentCodes.Production, StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical UAT and Production metadata environments are required before MOE contract seeding.");
    }

    private async Task<CatalogEntity> EnsureEntityAsync(CancellationToken cancellationToken)
    {
        var matches = await db.CatalogEntities.Where(entity => entity.Code == EntityCode).Take(2).ToListAsync(cancellationToken);
        if (matches.Count > 1)
            throw new InvalidOperationException("MOE catalog entity code is ambiguous; contract seeding stopped.");
        if (matches.Count == 1)
            return matches[0];

        var entity = new CatalogEntity
        {
            Id = StableGuid("entity:MOE"),
            Code = EntityCode,
            NameAr = "وزارة التربية",
            NameEn = "Ministry of Education",
            Logo = string.Empty,
            Active = true,
            DisplayOrder = 13,
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        db.CatalogEntities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task EnsureServiceAsync(
        CatalogEntity entity,
        ServiceDefinition definition,
        CancellationToken cancellationToken)
    {
        var versions = await db.CatalogServices
            .Include(service => service.EnvironmentConfigs)
            .Include(service => service.Fields)
            .Include(service => service.ResultMappings)
            .Where(service => service.EntityId == entity.Id && service.Code == definition.Code)
            .OrderByDescending(service => service.Version)
            .ToListAsync(cancellationToken);

        var current = versions.SingleOrDefault(service => service.IsCurrent);
        if (current is not null && current.Version >= DefinitionVersion)
            return;

        Guid definitionKey;
        if (current is not null)
        {
            if (!IsSafeCatalogPlaceholder(current))
                return;

            definitionKey = current.DefinitionKey;
            current.IsCurrent = false;
            current.Active = false;
            current.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            definitionKey = StableGuid($"definition:{definition.Code}");
        }

        var service = BuildService(entity.Id, definitionKey, definition);
        db.CatalogServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        await AddBasicAuthProfileAsync(service, definition, cancellationToken);
    }

    private static bool IsSafeCatalogPlaceholder(CatalogService service) =>
        service.Version == 1
        && service.FirstUsedAtUtc is null
        && service.EnvironmentConfigs.Count == 0
        && service.Fields.Count == 0
        && service.ResultMappings.Count == 0;

    private CatalogService BuildService(Guid entityId, Guid definitionKey, ServiceDefinition definition)
    {
        var serviceId = StableGuid($"service:v2:{definition.Code}");
        var service = new CatalogService
        {
            Id = serviceId,
            DefinitionKey = definitionKey,
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

        service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
        {
            Id = StableGuid($"config:uat:v2:{definition.Code}"),
            ServiceId = serviceId,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            BaseUrl = UatBaseUrl,
            RelativePath = definition.RelativePath,
            HttpMethod = "GET",
            ContentType = "application/json",
            NonSecretHeadersJson = "{}",
            TimeoutSeconds = 30,
            TlsPolicy = "SystemDefault",
            ValidateServerCertificate = true,
            ProxyUrl = string.Empty,
            HealthPath = string.Empty,
            HealthMethod = string.Empty,
            Active = false,
            LastTestedAtUtc = null,
            LastTestStatus = "CONTRACT_READY_BASIC_CREDENTIALS_REQUIRED",
            AuthProfileId = null
        });
        service.EnvironmentConfigs.Add(new ServiceEnvironmentConfig
        {
            Id = StableGuid($"config:production:v2:{definition.Code}"),
            ServiceId = serviceId,
            EnvironmentId = CatalogEnvironmentCodes.ProductionId,
            BaseUrl = string.Empty,
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
        });

        service.Fields.Add(new ServiceFieldDefinition
        {
            Id = StableGuid($"field:v2:{definition.Code}:cid"),
            ServiceId = serviceId,
            Key = "cid",
            LabelAr = "الرقم المدني للطالب",
            LabelEn = "Student Civil ID",
            FieldType = "integer",
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

        var order = 10;
        foreach (var mapping in definition.ResultMappings)
        {
            service.ResultMappings.Add(new ResultMappingDefinition
            {
                Id = StableGuid($"result:v2:{definition.Code}:{mapping.SourcePath}"),
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

    private async Task AddBasicAuthProfileAsync(
        CatalogService service,
        ServiceDefinition definition,
        CancellationToken cancellationToken)
    {
        var profileId = StableGuid($"auth:uat:v2:{definition.Code}");
        var profile = new AuthProfile
        {
            Id = profileId,
            OwnerServiceId = service.Id,
            OwnerEnvironmentId = CatalogEnvironmentCodes.UatId,
            Name = $"MOE {definition.Code} UAT Basic authentication",
            // Generic CustomHeaders secret resolution is converted to Authorization: Basic
            // by BasicAuthenticationTransformHandler immediately before network transport.
            AuthType = AuthProfileType.CustomHeaders,
            IsEnabled = false,
            Version = 1,
            CreatedBy = "P17MoeContractSeed",
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        profile.Bindings.Add(new AuthProfileBinding
        {
            Id = StableGuid($"auth-binding:uat:v2:{definition.Code}"),
            AuthProfileId = profile.Id,
            ServiceId = service.Id,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            IsShared = false,
            DecisionBy = "P17MoeContractSeed",
            DecisionReason = "Independent MOE UAT Basic-auth scope. Credential material is never seeded.",
            DecisionAtUtc = clock.UtcNow
        });

        db.AuthProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        uat.AuthProfileId = profile.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid StableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("GSIP:MOE-OFFICIAL-CONTRACT:" + key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static ResultSeed R(string path, string labelAr, string labelEn, string type, bool sensitive) =>
        new(path, labelAr, labelEn, type, sensitive);

    private sealed record ServiceDefinition(
        string Code,
        string NameAr,
        string NameEn,
        string DescriptionAr,
        string DescriptionEn,
        string RelativePath,
        IReadOnlyList<ResultSeed> ResultMappings);

    private sealed record ResultSeed(
        string SourcePath,
        string LabelAr,
        string LabelEn,
        string ResultType,
        bool Sensitive);
}
