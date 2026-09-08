using GSIP.Application.Authorization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GSIP.Infrastructure.Setup.Migrations;

[DbContext(typeof(GsipDbContext))]
[Migration("20260908151000_RbacFoundation")]
public sealed class RbacFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionKey });
                table.ForeignKey(
                    name: "FK_RolePermissions_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RoleServicePermissions",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                PermissionKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleServicePermissions", x => new { x.RoleId, x.ServiceCode, x.PermissionKey });
                table.ForeignKey(
                    name: "FK_RoleServicePermissions_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionKey",
            table: "RolePermissions",
            column: "PermissionKey");
        migrationBuilder.CreateIndex(
            name: "IX_RoleServicePermissions_ServiceCode_PermissionKey",
            table: "RoleServicePermissions",
            columns: new[] { "ServiceCode", "PermissionKey" });

        // These are fixed product seed values, not runtime/user input. Raw SQL is used here because
        // the repository keeps hand-written migrations without generated designer target models;
        // EF InsertData requires that target model and fails before executing the migration.
        foreach (var role in GsipRoles.SeedRoles)
        {
            var escapedName = role.Name.Replace("'", "''", StringComparison.Ordinal);
            migrationBuilder.Sql(
                $"INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES ('{role.Id:D}', N'{escapedName}', N'{escapedName.ToUpperInvariant()}', NULL);");
        }

        const string seededAt = "2026-09-08T15:10:00+00:00";
        var seededPermissions = new Dictionary<Guid, IReadOnlyList<string>>
        {
            [Guid.Parse(GsipRoles.SystemAdministratorId)] = GsipPermissions.All,
            [Guid.Parse(GsipRoles.IntegrationManagerId)] =
            [
                GsipPermissions.EntitiesView,
                GsipPermissions.EntitiesManage,
                GsipPermissions.ServicesView,
                GsipPermissions.ServicesExecute,
                GsipPermissions.ServicesManage,
                GsipPermissions.ServiceSecretsManage,
                GsipPermissions.RequestsViewOwn,
                GsipPermissions.RequestsViewDepartment,
                GsipPermissions.RequestsViewAll,
                GsipPermissions.RequestsExport,
                GsipPermissions.SettingsManage,
                GsipPermissions.DiagnosticsRun
            ],
            [Guid.Parse(GsipRoles.ServiceOperatorId)] =
            [
                GsipPermissions.EntitiesView,
                GsipPermissions.ServicesView,
                GsipPermissions.ServicesExecute,
                GsipPermissions.RequestsViewOwn
            ],
            [Guid.Parse(GsipRoles.AuditorId)] =
            [
                GsipPermissions.EntitiesView,
                GsipPermissions.ServicesView,
                GsipPermissions.RequestsViewOwn,
                GsipPermissions.RequestsViewDepartment,
                GsipPermissions.RequestsViewAll,
                GsipPermissions.RequestsExport,
                GsipPermissions.AuditView,
                GsipPermissions.AuditExport,
                GsipPermissions.AuditViewSensitive
            ],
            [Guid.Parse(GsipRoles.ReadOnlyId)] =
            [
                GsipPermissions.EntitiesView,
                GsipPermissions.ServicesView,
                GsipPermissions.RequestsViewOwn
            ]
        };

        foreach (var role in seededPermissions)
        {
            foreach (var permission in role.Value)
            {
                var escapedPermission = permission.Replace("'", "''", StringComparison.Ordinal);
                migrationBuilder.Sql(
                    $"INSERT INTO [RolePermissions] ([RoleId], [PermissionKey], [IsAllowed], [UpdatedAtUtc]) VALUES ('{role.Key:D}', N'{escapedPermission}', 1, '{seededAt}');");
            }
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "RoleServicePermissions");

        foreach (var role in GsipRoles.SeedRoles)
        {
            migrationBuilder.Sql($"DELETE FROM [AspNetRoles] WHERE [Id] = '{role.Id:D}';");
        }
    }
}
