using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSIP.Infrastructure.Setup.Migrations;

public partial class RequestHistoryFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DepartmentCode", table: "AspNetUsers", type: "nvarchar(100)", maxLength: 100, nullable: true);

        migrationBuilder.CreateTable(
            name: "RequestExecutionRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntityCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ServiceVersion = table.Column<int>(type: "int", nullable: false),
                EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnvironmentCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                MaskedInputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ProtectedStructuredResult = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                ProtectedRawResponse = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                LifecycleStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                OutcomeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                Attempts = table.Column<int>(type: "int", nullable: false),
                ResultSuppressedByBound = table.Column<bool>(type: "bit", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RetainUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RequestExecutionRecord", x => x.Id);
                table.ForeignKey(name: "FK_RequestExecutionRecord_AspNetUsers_UserReferenceId", column: x => x.UserReferenceId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_RequestId", table: "RequestExecutionRecord", column: "RequestId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_UserReferenceId", table: "RequestExecutionRecord", column: "UserReferenceId");
        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_ActorUserId_StartedAtUtc", table: "RequestExecutionRecord", columns: new[] { "ActorUserId", "StartedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_DepartmentCode_StartedAtUtc", table: "RequestExecutionRecord", columns: new[] { "DepartmentCode", "StartedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_ServiceId_StartedAtUtc", table: "RequestExecutionRecord", columns: new[] { "ServiceId", "StartedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_RequestExecutionRecord_RetainUntilUtc_LifecycleStatus", table: "RequestExecutionRecord", columns: new[] { "RetainUntilUtc", "LifecycleStatus" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RequestExecutionRecord");
        migrationBuilder.DropColumn(name: "DepartmentCode", table: "AspNetUsers");
    }
}
