using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GSIP.Infrastructure.Setup.Migrations;

[DbContext(typeof(GsipDbContext))]
[Migration("20260909213000_AuditTrailTamperEvidence")]
public partial class AuditTrailTamperEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "SequenceNumber", table: "AuthenticationAuditEvents", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetType", table: "AuthenticationAuditEvents", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetId", table: "AuthenticationAuditEvents", type: "nvarchar(160)", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RequestId", table: "AuthenticationAuditEvents", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EntityCode", table: "AuthenticationAuditEvents", type: "nvarchar(40)", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ServiceCode", table: "AuthenticationAuditEvents", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Source", table: "AuthenticationAuditEvents", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Device", table: "AuthenticationAuditEvents", type: "nvarchar(160)", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>(name: "MetadataJson", table: "AuthenticationAuditEvents", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PreviousHash", table: "AuthenticationAuditEvents", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RecordHash", table: "AuthenticationAuditEvents", type: "char(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "RetainUntilUtc", table: "AuthenticationAuditEvents", type: "datetimeoffset", nullable: true);

        migrationBuilder.CreateIndex(
            name: "UX_AuthenticationAuditEvents_SequenceNumber",
            table: "AuthenticationAuditEvents",
            column: "SequenceNumber",
            unique: true,
            filter: "[SequenceNumber] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_AuthenticationAuditEvents_ServiceCode_OccurredAtUtc",
            table: "AuthenticationAuditEvents",
            columns: new[] { "ServiceCode", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_AuthenticationAuditEvents_RetainUntilUtc_SequenceNumber",
            table: "AuthenticationAuditEvents",
            columns: new[] { "RetainUntilUtc", "SequenceNumber" });

        migrationBuilder.CreateTable(
            name: "AuditChainState",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                LastSequence = table.Column<long>(type: "bigint", nullable: false),
                LastHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                CheckpointSequence = table.Column<long>(type: "bigint", nullable: false),
                CheckpointHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                LastVerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastIntegrityStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditChainState", x => x.Id));

        // AuditChainState is intentionally maintained through bounded, parameterized SQL in the
        // canonical audit runtime rather than through the EF model. Seed it with fixed migration
        // SQL so migration generation does not require a duplicate DbSet/entity mapping.
        migrationBuilder.Sql(
            """
            INSERT INTO [dbo].[AuditChainState]
                ([Id], [LastSequence], [LastHash], [CheckpointSequence], [CheckpointHash], [LastIntegrityStatus])
            VALUES
                (1, 0, '', 0, '', N'Unknown');
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_AuthenticationAuditEvents_AppendOnly]
            ON [dbo].[AuthenticationAuditEvents]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted WHERE SequenceNumber IS NOT NULL)
                   AND ISNULL(TRY_CAST(SESSION_CONTEXT(N'GSIP_AUDIT_RETENTION') AS int), 0) <> 1
                BEGIN
                    THROW 51011, 'Canonical audit records are append-only.', 1;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuthenticationAuditEvents_AppendOnly];");
        migrationBuilder.DropTable(name: "AuditChainState");
        migrationBuilder.DropIndex(name: "UX_AuthenticationAuditEvents_SequenceNumber", table: "AuthenticationAuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuthenticationAuditEvents_ServiceCode_OccurredAtUtc", table: "AuthenticationAuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuthenticationAuditEvents_RetainUntilUtc_SequenceNumber", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "SequenceNumber", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "TargetType", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "TargetId", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "RequestId", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "EntityCode", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "ServiceCode", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "Source", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "Device", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "MetadataJson", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "PreviousHash", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "RecordHash", table: "AuthenticationAuditEvents");
        migrationBuilder.DropColumn(name: "RetainUntilUtc", table: "AuthenticationAuditEvents");
    }
}
