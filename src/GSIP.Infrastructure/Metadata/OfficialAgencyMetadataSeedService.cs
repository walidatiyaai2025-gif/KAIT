using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSIP.Application.Abstractions;
using GSIP.Domain.Metadata;
using GSIP.Domain.Secrets;
using GSIP.Infrastructure.Execution;
using GSIP.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Infrastructure.Metadata;

/// <summary>
/// Materializes owner-supplied official UAT Swagger contracts for agencies outside the
/// canonical MOJ seed. Contract metadata is additive/fail-closed: credentials are never
/// seeded, UAT configurations remain disabled until an administrator stores credentials
/// and explicitly enables them, and Production is never inferred from UAT evidence.
/// </summary>
public sealed class OfficialAgencyMetadataSeedService(GsipDbContext db, ISystemClock clock)
{
    public const int DefinitionVersion = 2;
    public const string MohEntityCode = "MOH";
    public const string CscEntityCode = "CSC";

    private static readonly ContractDefinition[] Definitions =
    [
        new(
            MohEntityCode,
            "MOH_CERTIFICATE_INFORMATION",
            "خدمة معلومات شهادة ما قبل الزواج",
            "Certificate Information Service",
            "استعلام حالة وتفاصيل شهادة ما قبل الزواج للزوجين وفق عقد UAT الرسمي المقدم من المالك.",
            "Retrieve pre-marriage certificate status and details for a couple from the owner-supplied official UAT contract.",
            "https://moh-uat.api-non-prod.cait.gov.kw/prmapi/moj/v1",
            "/certificate",
            "POST",
            "application/json",
            "/authenticate",
            "application/json",
            "data",
            "username",
            "password",
            null,
            "string",
            true,
            [
                F("maleCivilId", "الرقم المدني للزوج", "Male Civil ID", "text", true, true),
                F("femaleCivilId", "الرقم المدني للزوجة", "Female Civil ID", "text", true, true)
            ],
            [
                R("code", "رمز النتيجة", "Result Code", "integer", false),
                R("message", "الرسالة", "Message", "text", false),
                R("messageAr", "الرسالة العربية", "Arabic Message", "text", false),
                R("data.certificateNo", "رقم الشهادة", "Certificate Number", "integer", true),
                R("data.date", "تاريخ الشهادة", "Certificate Date", "text", true),
                R("data.status", "حالة الشهادة", "Certificate Status", "text", false)
            ]),

        new(
            MohEntityCode,
            "MOH_DEATH_CERTIFICATE_CIVIL_ID",
            "خدمة الاستعلام عن شهادة الوفاة بالرقم المدني",
            "Death Certificate Inquiry Using Civil ID",
            "استعلام بيانات شهادة الوفاة باستخدام الرقم المدني وفق عقد UAT الرسمي المقدم من المالك.",
            "Retrieve death certificate details using a civil ID from the owner-supplied official UAT contract.",
            "https://moh-uat.api-non-prod.cait.gov.kw/api/v1",
            "/inquiry/civil-id",
            "POST",
            "application/json",
            "/auth/login",
            "application/json",
            "accessToken",
            "username",
            "password",
            null,
            "string",
            false,
            [F("civilId", "الرقم المدني", "Civil ID", "text", true, true)],
            [
                R("deathDate", "تاريخ الوفاة", "Death Date", "text", true),
                R("deathDateString", "تاريخ الوفاة النصي", "Death Date Text", "text", true),
                R("registrationDate", "تاريخ التسجيل", "Registration Date", "text", true),
                R("certificateRegistrationNumber", "رقم تسجيل الشهادة", "Certificate Registration Number", "text", true),
                R("status", "الحالة", "Status", "text", false),
                R("civilId", "الرقم المدني", "Civil ID", "text", true)
            ]),

        new(
            MohEntityCode,
            "MOH_DEATH_CERTIFICATE_PASSPORT",
            "خدمة الاستعلام عن شهادة الوفاة بالجواز",
            "Death Certificate Inquiry Using Passport",
            "استعلام بيانات شهادة الوفاة باستخدام رقم الجواز والجنسية وفق عقد UAT الرسمي المقدم من المالك.",
            "Retrieve death certificate details using passport number and nationality from the owner-supplied official UAT contract.",
            "https://moh-uat.api-non-prod.cait.gov.kw/api/v1",
            "/inquiry/passport",
            "POST",
            "application/json",
            "/auth/login",
            "application/json",
            "accessToken",
            "username",
            "password",
            null,
            "string",
            false,
            [
                F("passport", "رقم الجواز", "Passport Number", "text", true, true),
                F("nationality", "رمز الجنسية", "Nationality Code", "integer", true, false)
            ],
            [
                R("nationalityCode", "رمز الجنسية", "Nationality Code", "text", false),
                R("nationalityDescriptionEn", "وصف الجنسية بالإنجليزية", "Nationality Description (EN)", "text", false),
                R("nationalityDescriptionAr", "وصف الجنسية بالعربية", "Nationality Description (AR)", "text", false),
                R("deathDate", "تاريخ الوفاة", "Death Date", "text", true),
                R("deathDateString", "تاريخ الوفاة النصي", "Death Date Text", "text", true),
                R("registrationDate", "تاريخ التسجيل", "Registration Date", "text", true),
                R("certificateRegistrationNumber", "رقم تسجيل الشهادة", "Certificate Registration Number", "text", true),
                R("passportNumber", "رقم الجواز", "Passport Number", "text", true)
            ]),

        new(
            MohEntityCode,
            "MOH_MEDICAL_LICENSE_INSTITUTION",
            "خدمة الاستعلام عن بيانات مؤسسة الترخيص الطبي",
            "Medical License - Institution Details Inquiry Service",
            "استعلام بيانات المؤسسة باستخدام رقم PACI ورقم الترخيص وفق عقد UAT الرسمي المقدم من المالك.",
            "Retrieve medical-license institution details using PACI and license numbers from the owner-supplied official UAT contract.",
            "https://moh-uat.api-non-prod.cait.gov.kw/licenseservice/api/v1",
            "/institutions",
            "POST",
            "application/json",
            "/authenticate",
            "application/json",
            "accessToken",
            "userName",
            "password",
            null,
            "string",
            false,
            [
                F("institutionPaciNo", "رقم PACI للمؤسسة", "Institution PACI Number", "text", true, true),
                F("licenseNoEst", "رقم الترخيص", "License Number", "text", true, true)
            ],
            [
                R("errMsgEn", "رسالة الخطأ بالإنجليزية", "Error Message (EN)", "text", false),
                R("errMsgAr", "رسالة الخطأ بالعربية", "Error Message (AR)", "text", false),
                R("error", "حالة الخطأ", "Error State", "boolean", false),
                R("data[0].workplaceNameEn", "اسم جهة العمل بالإنجليزية", "Workplace Name (EN)", "text", false),
                R("data[0].workplaceNameAr", "اسم جهة العمل بالعربية", "Workplace Name (AR)", "text", false),
                R("data[0].workPlaceTypeCode", "رمز نوع جهة العمل", "Workplace Type Code", "text", false),
                R("data[0].workPlaceType", "نوع جهة العمل", "Workplace Type", "text", false),
                R("data[0].licenseNoEst", "رقم الترخيص", "License Number", "text", true),
                R("data[0].governorateNameEn", "المحافظة", "Governorate", "text", false),
                R("data[0].districtNameEn", "المنطقة", "District", "text", false),
                R("data[0].institutionPaciNo", "رقم PACI للمؤسسة", "Institution PACI Number", "text", true)
            ]),

        new(
            CscEntityCode,
            "CSC_EMPLOYEE_DATA",
            "خدمة بيانات الموظف",
            "Employee All Data Service",
            "استرجاع البيانات الأساسية للموظف وبيانات الإجازات وفق عقد CSC UAT الرسمي المقدم من المالك.",
            "Retrieve employee basic data and vacation data from the owner-supplied official CSC UAT contract.",
            "https://csc-uat.api-non-prod.cait.gov.kw",
            "/Mvcsc/EmployeeProfile/empAllData",
            "GET",
            "application/json",
            "/csc/token/generate",
            "application/json",
            "response.token",
            "username",
            "password",
            "civilId",
            "integer",
            false,
            [F("civil", "الرقم المدني", "Civil ID", "integer", true, true)],
            [
                R("response.empPersonalData.realCivilId", "الرقم المدني", "Civil ID", "text", true),
                R("response.empPersonalData.employeeFullName", "اسم الموظف", "Employee Full Name", "text", true),
                R("response.empPersonalData.birthDate", "تاريخ الميلاد", "Birth Date", "text", true),
                R("response.empPersonalData.mobileNo", "رقم الهاتف", "Mobile Number", "text", true),
                R("response.empPersonalData.jobName", "المسمى الوظيفي", "Job Name", "text", false),
                R("response.empPersonalData.statusName", "حالة الموظف", "Employee Status", "text", false),
                R("response.empPersonalData.email", "البريد الإلكتروني", "Email", "text", true),
                R("response.empVacDTO.vacPeriodicBalance", "رصيد الإجازة الدورية", "Periodic Vacation Balance", "number", false),
                R("response.empVacDTO.vacEmergencyBalance", "رصيد الإجازة الطارئة", "Emergency Vacation Balance", "number", false),
                R("response.responseMsgAr", "رسالة الاستجابة بالعربية", "Response Message (AR)", "text", false),
                R("response.responseMsgEn", "رسالة الاستجابة بالإنجليزية", "Response Message (EN)", "text", false),
                R("response.responseStatusCode", "رمز الاستجابة", "Response Status Code", "integer", false)
            ]),

        new(
            CscEntityCode,
            "CSC_EMPLOYEE_FINANCIAL_DATA",
            "خدمة البيانات المالية للموظف",
            "Employee Financial Data Service",
            "استرجاع البيانات المالية للموظف وفق عقد CSC UAT الرسمي المقدم من المالك.",
            "Retrieve employee financial data from the owner-supplied official CSC UAT contract.",
            "https://csc-uat.api-non-prod.cait.gov.kw",
            "/Mvcsc/v1/EmployeeProfile/financial",
            "GET",
            "application/json",
            "/csc/token/generate",
            "application/json",
            "response.token",
            "username",
            "password",
            "civilId",
            "integer",
            false,
            [
                F("civil", "الرقم المدني", "Civil ID", "integer", true, true),
                F("yearCode", "كود السنة", "Year Code", "integer", false, false),
                F("monthCode", "كود الشهر", "Month Code", "integer", false, false)
            ],
            [
                R("status.type", "نوع الحالة", "Status Type", "text", false),
                R("status.code", "رمز الحالة", "Status Code", "integer", false),
                R("response.status", "حالة الاستجابة", "Response Status", "integer", false),
                R("response.msg", "رسالة الاستجابة", "Response Message", "text", false),
                R("response.empFinicialDataList[0].realCivilID", "الرقم المدني", "Civil ID", "integer", true),
                R("response.empFinicialDataList[0].accountCheckNo", "رقم الحساب", "Account Number", "text", true),
                R("response.empFinicialDataList[0].actualSalary", "الراتب الفعلي", "Actual Salary", "number", true),
                R("response.empFinicialDataList[0].bankCode", "رمز البنك", "Bank Code", "integer", false),
                R("response.empFinicialDataList[0].bankName", "اسم البنك", "Bank Name", "text", false),
                R("response.empFinicialDataList[0].totalMertis", "إجمالي الاستحقاقات", "Total Merits", "number", true),
                R("response.empFinicialDataList[0].totalDeducts", "إجمالي الاستقطاعات", "Total Deductions", "number", true),
                R("response.empFinicialDataList[0].salYear", "سنة الراتب", "Salary Year", "integer", false),
                R("response.empFinicialDataList[0].salMonth", "شهر الراتب", "Salary Month", "integer", false)
            ]),

        new(
            CscEntityCode,
            "CSC_EMPLOYEE_SALARY_DETAILS",
            "خدمة تفاصيل راتب الموظف",
            "Employee Salary Details Service",
            "استرجاع تفاصيل راتب الموظف وفق عقد CSC UAT الرسمي المقدم من المالك.",
            "Retrieve employee salary details from the owner-supplied official CSC UAT contract.",
            "https://csc-uat.api-non-prod.cait.gov.kw/csc",
            "/v1/creditBank/empSalDets",
            "GET",
            "application/json",
            "/token/generate",
            "application/json",
            "response.token",
            "username",
            "password",
            "civilId",
            "integer",
            false,
            [
                F("civilId", "الرقم المدني", "Civil ID", "integer", true, true),
                F("nextMonth", "الشهر التالي", "Next Month", "integer", true, false)
            ],
            [
                R("response.status", "حالة الاستجابة", "Response Status", "integer", false),
                R("response.msg", "رسالة الاستجابة", "Response Message", "text", false),
                R("response.empSalDetsLst[0].fcode", "رمز البند", "Item Code", "integer", false),
                R("response.empSalDetsLst[0].fvalue", "قيمة البند", "Item Value", "text", true),
                R("response.empSalDetsLst[0].civilID", "الرقم المدني", "Civil ID", "text", true),
                R("response.empSalDetsLst[0].fdesc", "وصف البند", "Item Description", "text", false),
                R("response.empSalDetsLst[0].ftype", "نوع البند", "Item Type", "text", false)
            ])
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await ValidateCanonicalEnvironmentsAsync(cancellationToken);

        var moh = await EnsureEntityAsync(
            MohEntityCode,
            "وزارة الصحة",
            "Ministry of Health",
            12,
            cancellationToken);

        var csc = await db.CatalogEntities.SingleOrDefaultAsync(entity => entity.Code == CscEntityCode, cancellationToken)
            ?? throw new InvalidOperationException("CSC green-catalog entity must exist before official CSC contract seeding.");

        foreach (var definition in Definitions)
        {
            var entity = definition.EntityCode == MohEntityCode ? moh : csc;
            await EnsureOfficialServiceAsync(entity, definition, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ValidateCanonicalEnvironmentsAsync(CancellationToken cancellationToken)
    {
        var environments = await db.CatalogEnvironments
            .Where(environment => environment.Id == CatalogEnvironmentCodes.UatId || environment.Id == CatalogEnvironmentCodes.ProductionId)
            .ToListAsync(cancellationToken);
        var uat = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.UatId);
        var production = environments.SingleOrDefault(environment => environment.Id == CatalogEnvironmentCodes.ProductionId);
        if (uat is null || production is null
            || !string.Equals(uat.Code, CatalogEnvironmentCodes.Uat, StringComparison.Ordinal)
            || !string.Equals(production.Code, CatalogEnvironmentCodes.Production, StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical UAT and Production metadata environments are required before official agency contract seeding.");
    }

    private async Task<CatalogEntity> EnsureEntityAsync(
        string code,
        string nameAr,
        string nameEn,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        var matches = await db.CatalogEntities.Where(entity => entity.Code == code).Take(2).ToListAsync(cancellationToken);
        if (matches.Count > 1)
            throw new InvalidOperationException($"Catalog entity code '{code}' is ambiguous; official agency seeding stopped.");
        if (matches.Count == 1)
            return matches[0];

        var entity = new CatalogEntity
        {
            Id = StableGuid($"entity:{code}"),
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            Logo = string.Empty,
            Active = true,
            DisplayOrder = displayOrder,
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        db.CatalogEntities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task EnsureOfficialServiceAsync(
        CatalogEntity entity,
        ContractDefinition definition,
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
                return; // Preserve owner-used/edited service metadata; never overwrite it automatically.
            definitionKey = current.DefinitionKey;
            current.IsCurrent = false;
            current.Active = false;
            current.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            definitionKey = StableGuid($"definition:{definition.EntityCode}:{definition.Code}");
        }

        var service = BuildService(entity.Id, definitionKey, definition);
        db.CatalogServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        await AddAuthProfileAsync(service, definition, cancellationToken);
    }

    private static bool IsSafeCatalogPlaceholder(CatalogService service) =>
        service.Version == 1
        && service.FirstUsedAtUtc is null
        && service.EnvironmentConfigs.Count == 0
        && service.Fields.Count == 0
        && service.ResultMappings.Count == 0;

    private CatalogService BuildService(Guid entityId, Guid definitionKey, ContractDefinition definition)
    {
        var serviceId = StableGuid($"service:v2:{definition.EntityCode}:{definition.Code}");
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

        service.EnvironmentConfigs.Add(BuildUatConfig(serviceId, definition));
        service.EnvironmentConfigs.Add(BuildProductionConfig(serviceId, definition));

        var order = 10;
        foreach (var field in definition.Fields)
        {
            service.Fields.Add(new ServiceFieldDefinition
            {
                Id = StableGuid($"field:v2:{definition.EntityCode}:{definition.Code}:{field.Key}"),
                ServiceId = serviceId,
                Key = field.Key,
                LabelAr = field.LabelAr,
                LabelEn = field.LabelEn,
                FieldType = field.FieldType,
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
                Id = StableGuid($"result:v2:{definition.EntityCode}:{definition.Code}:{mapping.SourcePath}"),
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

    private static ServiceEnvironmentConfig BuildUatConfig(Guid serviceId, ContractDefinition definition) => new()
    {
        Id = StableGuid($"config:uat:v2:{definition.EntityCode}:{definition.Code}"),
        ServiceId = serviceId,
        EnvironmentId = CatalogEnvironmentCodes.UatId,
        BaseUrl = definition.UatBaseUrl,
        RelativePath = definition.RelativePath,
        HttpMethod = definition.HttpMethod,
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

    private static ServiceEnvironmentConfig BuildProductionConfig(Guid serviceId, ContractDefinition definition) => new()
    {
        Id = StableGuid($"config:production:v2:{definition.EntityCode}:{definition.Code}"),
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
    };

    private async Task AddAuthProfileAsync(
        CatalogService service,
        ContractDefinition definition,
        CancellationToken cancellationToken)
    {
        var profileId = StableGuid($"auth:uat:v2:{definition.EntityCode}:{definition.Code}");
        var profile = new AuthProfile
        {
            Id = profileId,
            OwnerServiceId = service.Id,
            OwnerEnvironmentId = CatalogEnvironmentCodes.UatId,
            Name = $"{definition.EntityCode} {definition.Code} UAT token authentication",
            AuthType = AuthProfileType.TokenEndpoint,
            IsEnabled = false,
            Version = 1,
            CreatedBy = "P17OfficialAgencyContractSeed",
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };
        profile.Bindings.Add(new AuthProfileBinding
        {
            Id = StableGuid($"auth-binding:uat:v2:{definition.EntityCode}:{definition.Code}"),
            AuthProfileId = profile.Id,
            ServiceId = service.Id,
            EnvironmentId = CatalogEnvironmentCodes.UatId,
            IsShared = false,
            DecisionBy = "P17OfficialAgencyContractSeed",
            DecisionReason = $"Independent official UAT authentication scope for {definition.EntityCode} {definition.Code}; no credential material is seeded.",
            DecisionAtUtc = clock.UtcNow
        });
        db.AuthProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        var uat = service.EnvironmentConfigs.Single(config => config.EnvironmentId == CatalogEnvironmentCodes.UatId);
        uat.AuthProfileId = profile.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildTokenMetadata(ContractDefinition definition)
    {
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TokenEndpointContractMetadata.PathKey] = definition.TokenPath,
            [TokenEndpointContractMetadata.RequestContentTypeKey] = definition.TokenContentType,
            [TokenEndpointContractMetadata.ResponsePathKey] = definition.TokenResponsePath,
            [TokenEndpointContractMetadata.UsernameFieldKey] = definition.TokenUsernameField,
            [TokenEndpointContractMetadata.PasswordFieldKey] = definition.TokenPasswordField
        };
        if (!string.IsNullOrWhiteSpace(definition.TokenAuxiliaryField))
        {
            items[TokenEndpointContractMetadata.GehaFieldKey] = definition.TokenAuxiliaryField;
            items[TokenEndpointContractMetadata.GehaValueTypeKey] = definition.TokenAuxiliaryValueType;
        }
        if (definition.TokenApiKeyRequired)
            items[TokenEndpointContractMetadata.ApiKeyRequiredKey] = true;
        return JsonSerializer.Serialize(items);
    }

    private static Guid StableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("GSIP:OFFICIAL-AGENCY-CONTRACT:" + key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static FieldSeed F(string key, string labelAr, string labelEn, string type, bool required, bool sensitive) =>
        new(key, labelAr, labelEn, type, required, sensitive);

    private static ResultSeed R(string path, string labelAr, string labelEn, string type, bool sensitive) =>
        new(path, labelAr, labelEn, type, sensitive);

    private sealed record ContractDefinition(
        string EntityCode,
        string Code,
        string NameAr,
        string NameEn,
        string DescriptionAr,
        string DescriptionEn,
        string UatBaseUrl,
        string RelativePath,
        string HttpMethod,
        string ContentType,
        string TokenPath,
        string TokenContentType,
        string TokenResponsePath,
        string TokenUsernameField,
        string TokenPasswordField,
        string? TokenAuxiliaryField,
        string TokenAuxiliaryValueType,
        bool TokenApiKeyRequired,
        IReadOnlyList<FieldSeed> Fields,
        IReadOnlyList<ResultSeed> ResultMappings);

    private sealed record FieldSeed(
        string Key,
        string LabelAr,
        string LabelEn,
        string FieldType,
        bool Required,
        bool Sensitive);

    private sealed record ResultSeed(
        string SourcePath,
        string LabelAr,
        string LabelEn,
        string ResultType,
        bool Sensitive);
}
