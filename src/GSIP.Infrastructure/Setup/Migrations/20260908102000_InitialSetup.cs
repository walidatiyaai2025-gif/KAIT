using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GSIP.Infrastructure.Setup.Migrations;

[DbContext(typeof(GsipDbContext))]
[Migration("20260908102000_InitialSetup")]
public sealed class InitialSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SystemSetup",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OrganizationNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OrganizationNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PrimaryColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SessionTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                LockoutMinutes = table.Column<int>(type: "int", nullable: false),
                MaxFailedAccessAttempts = table.Column<int>(type: "int", nullable: false),
                RequireMfaForPrivilegedAccounts = table.Column<bool>(type: "bit", nullable: false),
                DefaultEnvironment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                IntegrationTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                ValidateServerCertificate = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SystemSetup", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BootstrapAdministrators",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Username = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                NormalizedUsername = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BootstrapAdministrators", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ServiceEnvironmentPlaceholders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntityCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ServiceCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Environment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ServiceEnvironmentPlaceholders", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BootstrapAdministrators_NormalizedUsername",
            table: "BootstrapAdministrators",
            column: "NormalizedUsername",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ServiceEnvironmentPlaceholders_EntityCode_ServiceCode_Environment",
            table: "ServiceEnvironmentPlaceholders",
            columns: new[] { "EntityCode", "ServiceCode", "Environment" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BootstrapAdministrators");
        migrationBuilder.DropTable(name: "ServiceEnvironmentPlaceholders");
        migrationBuilder.DropTable(name: "SystemSetup");
    }
}
