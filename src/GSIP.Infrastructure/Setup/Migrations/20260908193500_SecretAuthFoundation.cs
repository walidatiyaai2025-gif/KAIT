using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GSIP.Infrastructure.Setup.Migrations;

[DbContext(typeof(GsipDbContext))]
[Migration("20260908193500_SecretAuthFoundation")]
public sealed class SecretAuthFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecretVaultEntries",
            columns: table => new
            {
                Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ProtectedPayload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Generation = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_SecretVaultEntries", x => x.Reference));

        migrationBuilder.CreateTable(
            name: "AuthProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerEnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                AuthType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthProfiles", x => x.Id);
                table.ForeignKey(
                    "FK_AuthProfiles_CatalogServices_OwnerServiceId",
                    x => x.OwnerServiceId,
                    "CatalogServices",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_AuthProfiles_CatalogEnvironments_OwnerEnvironmentId",
                    x => x.OwnerEnvironmentId,
                    "CatalogEnvironments",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuthProfileBindings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsShared = table.Column<bool>(type: "bit", nullable: false),
                DecisionBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                DecisionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthProfileBindings", x => x.Id);
                table.UniqueConstraint(
                    "AK_AuthProfileBindings_ServiceId_EnvironmentId_AuthProfileId",
                    x => new { x.ServiceId, x.EnvironmentId, x.AuthProfileId });
                table.ForeignKey(
                    "FK_AuthProfileBindings_AuthProfiles_AuthProfileId",
                    x => x.AuthProfileId,
                    "AuthProfiles",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_AuthProfileBindings_CatalogServices_ServiceId",
                    x => x.ServiceId,
                    "CatalogServices",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_AuthProfileBindings_CatalogEnvironments_EnvironmentId",
                    x => x.EnvironmentId,
                    "CatalogEnvironments",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuthProfileSecrets",
            columns: table => new
            {
                AuthProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SecretName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                SecretReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthProfileSecrets", x => new { x.AuthProfileId, x.SecretName });
                table.ForeignKey(
                    "FK_AuthProfileSecrets_AuthProfiles_AuthProfileId",
                    x => x.AuthProfileId,
                    "AuthProfiles",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_AuthProfileSecrets_SecretVaultEntries_SecretReference",
                    x => x.SecretReference,
                    "SecretVaultEntries",
                    "Reference",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfiles_OwnerEnvironmentId",
            table: "AuthProfiles",
            column: "OwnerEnvironmentId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfiles_OwnerServiceId_OwnerEnvironmentId_Name",
            table: "AuthProfiles",
            columns: new[] { "OwnerServiceId", "OwnerEnvironmentId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfileBindings_AuthProfileId",
            table: "AuthProfileBindings",
            column: "AuthProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfileBindings_EnvironmentId",
            table: "AuthProfileBindings",
            column: "EnvironmentId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfileBindings_ServiceId_EnvironmentId",
            table: "AuthProfileBindings",
            columns: new[] { "ServiceId", "EnvironmentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuthProfileSecrets_SecretReference",
            table: "AuthProfileSecrets",
            column: "SecretReference");

        migrationBuilder.CreateIndex(
            name: "IX_ServiceEnvironmentConfigs_ServiceId_EnvironmentId_AuthProfileId",
            table: "ServiceEnvironmentConfigs",
            columns: new[] { "ServiceId", "EnvironmentId", "AuthProfileId" });

        migrationBuilder.AddForeignKey(
            name: "FK_ServiceEnvironmentConfigs_AuthProfileBindings_ServiceId_EnvironmentId_AuthProfileId",
            table: "ServiceEnvironmentConfigs",
            columns: new[] { "ServiceId", "EnvironmentId", "AuthProfileId" },
            principalTable: "AuthProfileBindings",
            principalColumns: new[] { "ServiceId", "EnvironmentId", "AuthProfileId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ServiceEnvironmentConfigs_AuthProfileBindings_ServiceId_EnvironmentId_AuthProfileId",
            table: "ServiceEnvironmentConfigs");

        migrationBuilder.DropIndex(
            name: "IX_ServiceEnvironmentConfigs_ServiceId_EnvironmentId_AuthProfileId",
            table: "ServiceEnvironmentConfigs");

        migrationBuilder.DropTable("AuthProfileSecrets");
        migrationBuilder.DropTable("AuthProfileBindings");
        migrationBuilder.DropTable("SecretVaultEntries");
        migrationBuilder.DropTable("AuthProfiles");
    }
}
