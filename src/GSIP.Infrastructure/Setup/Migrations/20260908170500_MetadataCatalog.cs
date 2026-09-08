using GSIP.Domain.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GSIP.Infrastructure.Setup.Migrations;

[DbContext(typeof(GsipDbContext))]
[Migration("20260908170500_MetadataCatalog")]
public sealed class MetadataCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CatalogEntities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Logo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Active = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CatalogEntities", x => x.Id));

        migrationBuilder.CreateTable(
            name: "CatalogEnvironments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Active = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CatalogEnvironments", x => x.Id));

        migrationBuilder.CreateTable(
            name: "CatalogServices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DefinitionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Active = table.Column<bool>(type: "bit", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                FirstUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogServices", x => x.Id);
                table.ForeignKey("FK_CatalogServices_CatalogEntities_EntityId", x => x.EntityId, "CatalogEntities", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ServiceEnvironmentConfigs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BaseUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                HttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                NonSecretHeadersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                TlsPolicy = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ValidateServerCertificate = table.Column<bool>(type: "bit", nullable: false),
                ProxyUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                HealthPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                HealthMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Active = table.Column<bool>(type: "bit", nullable: false),
                LastTestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastTestStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                AuthProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceEnvironmentConfigs", x => x.Id);
                table.ForeignKey("FK_ServiceEnvironmentConfigs_CatalogServices_ServiceId", x => x.ServiceId, "CatalogServices", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ServiceEnvironmentConfigs_CatalogEnvironments_EnvironmentId", x => x.EnvironmentId, "CatalogEnvironments", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ServiceFieldDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                LabelAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                LabelEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FieldType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Required = table.Column<bool>(type: "bit", nullable: false),
                Regex = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Minimum = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                Maximum = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                MinLength = table.Column<int>(type: "int", nullable: true),
                MaxLength = table.Column<int>(type: "int", nullable: true),
                OptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                Sensitive = table.Column<bool>(type: "bit", nullable: false),
                Masking = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceFieldDefinitions", x => x.Id);
                table.ForeignKey("FK_ServiceFieldDefinitions_CatalogServices_ServiceId", x => x.ServiceId, "CatalogServices", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ResultMappingDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourcePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                LabelAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                LabelEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ResultType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Formatter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Sensitive = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ResultMappingDefinitions", x => x.Id);
                table.ForeignKey("FK_ResultMappingDefinitions_CatalogServices_ServiceId", x => x.ServiceId, "CatalogServices", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_CatalogEntities_Code", "CatalogEntities", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_CatalogEnvironments_Code", "CatalogEnvironments", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_CatalogServices_DefinitionKey_Version", "CatalogServices", new[] { "DefinitionKey", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_CatalogServices_EntityId_Code_Current", "CatalogServices", new[] { "EntityId", "Code" }, unique: true, filter: "[IsCurrent] = 1");
        migrationBuilder.CreateIndex("IX_ServiceEnvironmentConfigs_ServiceId_EnvironmentId", "ServiceEnvironmentConfigs", new[] { "ServiceId", "EnvironmentId" }, unique: true);
        migrationBuilder.CreateIndex("IX_ServiceEnvironmentConfigs_EnvironmentId", "ServiceEnvironmentConfigs", "EnvironmentId");
        migrationBuilder.CreateIndex("IX_ServiceFieldDefinitions_ServiceId_Key", "ServiceFieldDefinitions", new[] { "ServiceId", "Key" }, unique: true);
        migrationBuilder.CreateIndex("IX_ResultMappingDefinitions_ServiceId_DisplayOrder", "ResultMappingDefinitions", new[] { "ServiceId", "DisplayOrder" });

        migrationBuilder.Sql($"INSERT INTO [CatalogEnvironments] ([Id],[Code],[NameAr],[NameEn],[Active],[DisplayOrder]) VALUES ('{CatalogEnvironmentCodes.UatId:D}', N'UAT', N'اختبار قبول المستخدم', N'UAT', 1, 10);");
        migrationBuilder.Sql($"INSERT INTO [CatalogEnvironments] ([Id],[Code],[NameAr],[NameEn],[Active],[DisplayOrder]) VALUES ('{CatalogEnvironmentCodes.ProductionId:D}', N'Production', N'الإنتاج', N'Production', 1, 20);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ResultMappingDefinitions");
        migrationBuilder.DropTable("ServiceEnvironmentConfigs");
        migrationBuilder.DropTable("ServiceFieldDefinitions");
        migrationBuilder.DropTable("CatalogEnvironments");
        migrationBuilder.DropTable("CatalogServices");
        migrationBuilder.DropTable("CatalogEntities");
    }
}
