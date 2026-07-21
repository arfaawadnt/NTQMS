using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_ref",
                schema: "qams",
                table: "nonconformance",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lead_auditor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_checklist_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_clause = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_checklist_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_checklist_item_audit_audit_id",
                        column: x => x.audit_id,
                        principalSchema: "qams",
                        principalTable: "audit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_finding",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    nc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_finding", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_finding_audit_audit_id",
                        column: x => x.audit_id,
                        principalSchema: "qams",
                        principalTable: "audit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_tenant_id_audit_ref",
                schema: "qams",
                table: "audit",
                columns: new[] { "tenant_id", "audit_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_tenant_id_status",
                schema: "qams",
                table: "audit",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_checklist_item_audit_id",
                schema: "qams",
                table: "audit_checklist_item",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_finding_audit_id",
                schema: "qams",
                table: "audit_finding",
                column: "audit_id");

            // Layer-2 isolation: RLS (see v0.2 note on the dormant-until-role-split model).
            migrationBuilder.Sql("""
                ALTER TABLE qams.audit ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.audit
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_checklist_item",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "audit_finding",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "audit",
                schema: "qams");

            migrationBuilder.DropColumn(
                name: "source_ref",
                schema: "qams",
                table: "nonconformance");
        }
    }
}
