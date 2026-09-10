using System.Data;
using System.Security.Cryptography;
using System.Text;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Metadata;

/// <summary>
/// Seeds the approved green-service matrix supplied by the owner.
/// MOJ is intentionally excluded here because its five approved services are already
/// seeded by <see cref="MojMetadataSeedService"/> with authoritative UAT contracts.
/// The remaining services are catalog-only until their Swagger/OpenAPI contracts are supplied.
/// No endpoint, field, credential, AuthProfile, API key, token path or Production URL is invented.
/// </summary>
public sealed class GreenGovernmentCatalogSeedService(GsipDbContext db, ISystemClock clock)
{
    public const int SourceEntityCount = 10;
    public const int SourceServiceCount = 43;
    public const int AdditionalEntityCount = 9;
    public const int AdditionalServiceCount = 38;

    private static readonly EntitySeedDefinition[] Definitions =
    [
        E("CSC", "ديوان الخدمة المدنية", "Civil Service Commission", 2,
            S("CSC_EMPLOYEE_DATA", "خدمة بيانات الموظف", "Employee Data Service"),
            S("CSC_EMPLOYEE_FINANCIAL_DATA", "خدمة البيانات المالية للموظف", "Employee Financial Data Service"),
            S("CSC_EMPLOYEE_SALARY_DETAILS", "خدمة تفاصيل راتب الموظف", "Employee Salary Details Service")),

        E("MOE", "وزارة التربية", "Ministry of Education", 3,
            S("MOE_LAST_ACTIVE_RECORD", "خدمة آخر سجل نشط", "Last Active Record Service"),
            S("MOE_LAST_STUDENT_RECORD", "خدمة آخر سجل طالب", "Last Student Record Service"),
            S("MOE_LAST_SUCCESS_RECORD", "خدمة آخر سجل نجاح", "Last Success Record Service")),

        E("PACI", "الهيئة العامة للمعلومات المدنية", "Public Authority for Civil Information", 5,
            S("PACI_ADDRESS_AVAILABILITY", "خدمة توفر العنوان", "Address Availability Service"),
            S("PACI_CARD_STATUS", "خدمة حالة البطاقة", "Card Status Service"),
            S("PACI_CARD_VALIDITY", "خدمة صلاحية البطاقة", "Card Validity Service")),

        E("PIFSS", "المؤسسة العامة للتأمينات الاجتماعية", "Public Institution for Social Security", 6,
            S("PIFSS_COMMUTATIONS", "خدمة استبدال المعاش", "Commutations Service"),
            S("PIFSS_CONTRIBUTIONS", "خدمة الاشتراكات", "Contributions Service"),
            S("PIFSS_EMPLOYEE_SALARY", "خدمة راتب الموظف", "Employee Salary Service")),

        E("KCB", "بنك الائتمان الكويتي", "Kuwait Credit Bank", 7,
            S("KCB_LOAN_CERTIFICATES", "خدمة شهادات القرض", "Loan Certificates Service"),
            S("KCB_RELATIONS", "خدمة العلاقات", "Relations Service")),

        E("PAMP", "الهيئة العامة للقوى العاملة", "Public Authority for Manpower", 8,
            S("PAMP_EMPLOYEE_INFORMATION", "خدمة معلومات الموظف", "Employee Information Service"),
            S("PAMP_EMPLOYEE_STATUS", "خدمة حالة الموظف", "Employee Status Service"),
            S("PAMP_EMPLOYEES_ALL_CIVIL_IDS", "خدمة الأرقام المدنية للموظفين", "Employees AllCivilIds Service"),
            S("PAMP_EMPLOYEES_COUNT", "خدمة عدد الموظفين", "Employees Count Service"),
            S("PAMP_EMPLOYEES_HISTORY", "خدمة تاريخ الموظفين", "Employees History Service"),
            S("PAMP_EMPLOYEES_SALARY", "خدمة رواتب الموظفين", "Employees Salary Service"),
            S("PAMP_EMPLOYEES_WORK_PERMIT", "خدمة تصاريح عمل الموظفين", "Employees WorkPermit Service"),
            S("PAMP_FILES_DHAMAN", "خدمة ضمان الملفات", "Files Dhaman Service"),
            S("PAMP_FILES_LICENSE", "خدمة تراخيص الملفات", "Files License Service"),
            S("PAMP_FILES_OWNERSHIPS", "خدمة ملكية الملفات", "Files Ownerships Service"),
            S("PAMP_FILES_PARTNERSHIPS", "خدمة شراكات الملفات", "Files Partnerships Service"),
            S("PAMP_PERSONS_DISBURSEMENTS", "خدمة صرف مبالغ الأشخاص", "Persons Disbursements Service"),
            S("PAMP_STUDENTS_REWARD", "خدمة مكافآت الطلبة", "Students Reward Service"),
            S("PAMP_FILE_INFO", "خدمة معلومات الملف", "File Info Service")),

        E("MOI", "وزارة الداخلية", "Ministry of Interior", 9,
            S("MOI_NATIONALITY_DETAILS", "خدمة تفاصيل الجنسية", "Nationality Details Service"),
            S("MOI_PERSON_DETAILS", "خدمة تفاصيل الشخص", "Person Details Service"),
            S("MOI_PERSON_MOVEMENT_STATUS", "خدمة حالة حركة الشخص", "Person Movement Status Service"),
            S("MOI_RESIDENCIES_SPONSORSHIP", "خدمة كفالات الإقامات", "Residencies Sponsorship Service"),
            S("MOI_VISAS_SPONSORSHIP", "خدمة كفالات التأشيرات", "Visas Sponsorship Service"),
            S("MOI_VEHICLES_LIST", "خدمة قائمة المركبات", "Vehicles List Service"),
            S("MOI_VEHICLE_DETAILS", "خدمة تفاصيل المركبة", "Vehicle Details Service"),
            S("MOI_DRIVING_LICENSE_DETAILS", "خدمة تفاصيل رخصة القيادة", "Driving License Details Service")),

        E("MOCI", "وزارة التجارة والصناعة", "Ministry of Commerce and Industry", 10,
            S("MOCI_LICENSE_DATA", "خدمة بيانات الرخصة", "License Data Service")),

        E("PADA", "الهيئة العامة لشؤون ذوي الإعاقة", "Public Authority for Disability Affairs", 11,
            S("PADA_GET_DISABLED_INFO", "خدمة بيانات ذوي الإعاقة", "Get Disabled Info Service"))
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        ValidateSourceMatrix();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        foreach (var entityDefinition in Definitions)
        {
            var entityMatches = await db.CatalogEntities
                .Where(entity => entity.Code == entityDefinition.Code)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (entityMatches.Count > 1)
                throw new InvalidOperationException($"Catalog entity code '{entityDefinition.Code}' is ambiguous; green catalog seeding stopped.");

            var entity = entityMatches.SingleOrDefault();
            if (entity is null)
            {
                entity = new CatalogEntity
                {
                    Id = StableGuid($"entity:{entityDefinition.Code}"),
                    Code = entityDefinition.Code,
                    NameAr = entityDefinition.NameAr,
                    NameEn = entityDefinition.NameEn,
                    Logo = string.Empty,
                    Active = true,
                    DisplayOrder = entityDefinition.DisplayOrder,
                    CreatedAtUtc = clock.UtcNow,
                    UpdatedAtUtc = clock.UtcNow
                };
                db.CatalogEntities.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var serviceDefinition in entityDefinition.Services)
                await EnsureCatalogServiceAsync(entity.Id, entityDefinition.Code, serviceDefinition, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureCatalogServiceAsync(
        Guid entityId,
        string entityCode,
        ServiceSeedDefinition definition,
        CancellationToken cancellationToken)
    {
        var definitionKey = StableGuid($"definition:{entityCode}:{definition.Code}");
        var matches = await db.CatalogServices
            .Where(service => service.DefinitionKey == definitionKey
                || (service.EntityId == entityId && service.Code == definition.Code))
            .OrderByDescending(service => service.Version)
            .ToListAsync(cancellationToken);

        // Preserve any existing owner-created/edited record. This seed is additive only.
        if (matches.Count > 0)
            return;

        db.CatalogServices.Add(new CatalogService
        {
            Id = StableGuid($"service:v1:{entityCode}:{definition.Code}"),
            DefinitionKey = definitionKey,
            EntityId = entityId,
            Code = definition.Code,
            NameAr = definition.NameAr,
            NameEn = definition.NameEn,
            DescriptionAr = "خدمة معتمدة ضمن القائمة الخضراء. بيانات Swagger والمسارات وحقول الطلب والاستجابة والمصادقة ستُضاف عند استلام العقد الرسمي.",
            DescriptionEn = "Approved green-list catalog service. Swagger paths, request/response fields and authentication details will be added only after the official contract is supplied.",
            Active = true,
            Version = 1,
            IsCurrent = true,
            FirstUsedAtUtc = null,
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateSourceMatrix()
    {
        if (Definitions.Length != AdditionalEntityCount)
            throw new InvalidOperationException("Green catalog entity matrix drift detected.");

        var serviceCount = Definitions.Sum(entity => entity.Services.Count);
        if (serviceCount != AdditionalServiceCount)
            throw new InvalidOperationException("Green catalog service matrix drift detected.");

        var duplicateEntity = Definitions
            .GroupBy(entity => entity.Code, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateEntity is not null)
            throw new InvalidOperationException($"Duplicate green catalog entity code '{duplicateEntity.Key}'.");

        var duplicateService = Definitions
            .SelectMany(entity => entity.Services.Select(service => $"{entity.Code}:{service.Code}"))
            .GroupBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateService is not null)
            throw new InvalidOperationException($"Duplicate green catalog service key '{duplicateService.Key}'.");
    }

    private static Guid StableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("GSIP:GREEN-GOVERNMENT-CATALOG:" + key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static EntitySeedDefinition E(
        string code,
        string nameAr,
        string nameEn,
        int displayOrder,
        params ServiceSeedDefinition[] services) =>
        new(code, nameAr, nameEn, displayOrder, Array.AsReadOnly(services));

    private static ServiceSeedDefinition S(string code, string nameAr, string nameEn) =>
        new(code, nameAr, nameEn);

    private sealed record EntitySeedDefinition(
        string Code,
        string NameAr,
        string NameEn,
        int DisplayOrder,
        IReadOnlyList<ServiceSeedDefinition> Services);

    private sealed record ServiceSeedDefinition(string Code, string NameAr, string NameEn);
}
