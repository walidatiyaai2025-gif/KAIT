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

        foreach (var role in GsipRoles.SeedRoles)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object?[] { role.Id, role.Name, role.Name.ToUpperInvariant(), null });
        }

        var seededAt = new DateTimeOffset(2026, 9, 8, 15, 10, 0, TimeSpan.Zero);
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
                migrationBuilder.InsertData(
                    table: "RolePermissions",
                    columns: new[] { "RoleId", "PermissionKey", "IsAllowed", "UpdatedAtUtc" },
                    values: new object[] { role.Key, permission, true, seededAt });
            }
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "RoleServicePermissions");

        foreach (var role in GsipRoles.SeedRoles)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: role.Id);
        }
    }
}
