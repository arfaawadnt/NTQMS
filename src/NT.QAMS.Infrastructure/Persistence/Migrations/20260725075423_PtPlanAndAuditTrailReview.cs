using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PtPlanAndAuditTrailReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_trail_review",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    events_reviewed = table.Column<int>(type: "integer", nullable: true),
                    field_changes_reviewed = table.Column<int>(type: "integer", nullable: true),
                    anomalies_found = table.Column<bool>(type: "boolean", nullable: true),
                    conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_trail_review", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pt_plan",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closure_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pt_plan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pt_plan_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheme = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    planned_cycles = table.Column<int>(type: "integer", nullable: false),
                    fulfilled_cycles = table.Column<int>(type: "integer", nullable: false),
                    last_enrollment_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pt_plan_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_pt_plan_item_pt_plan_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "qams",
                        principalTable: "pt_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_trail_review_tenant_id_review_ref",
                schema: "qams",
                table: "audit_trail_review",
                columns: new[] { "tenant_id", "review_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_trail_review_tenant_id_status",
                schema: "qams",
                table: "audit_trail_review",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pt_plan_tenant_id_plan_ref",
                schema: "qams",
                table: "pt_plan",
                columns: new[] { "tenant_id", "plan_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pt_plan_tenant_id_year",
                schema: "qams",
                table: "pt_plan",
                columns: new[] { "tenant_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pt_plan_item_plan_id",
                schema: "qams",
                table: "pt_plan_item",
                column: "plan_id");
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.pt_plan ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.pt_plan
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.audit_trail_review ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.audit_trail_review
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_trail_review",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "pt_plan_item",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "pt_plan",
                schema: "qams");
        }
    }
}
