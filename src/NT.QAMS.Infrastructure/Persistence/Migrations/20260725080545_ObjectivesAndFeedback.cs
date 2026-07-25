using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ObjectivesAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedback_entry",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    feedback_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    satisfaction_score = table.Column<int>(type: "integer", nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    logged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    action_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feedback_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quality_objective",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    objective_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    metric = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_value = table.Column<decimal>(type: "numeric", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closure_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_objective", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objective_progress",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    measured_on = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    objective_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objective_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_objective_progress_quality_objective_objective_id",
                        column: x => x.objective_id,
                        principalSchema: "qams",
                        principalTable: "quality_objective",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feedback_entry_tenant_id_feedback_ref",
                schema: "qams",
                table: "feedback_entry",
                columns: new[] { "tenant_id", "feedback_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feedback_entry_tenant_id_status",
                schema: "qams",
                table: "feedback_entry",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_objective_progress_objective_id",
                schema: "qams",
                table: "objective_progress",
                column: "objective_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_objective_tenant_id_objective_ref",
                schema: "qams",
                table: "quality_objective",
                columns: new[] { "tenant_id", "objective_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_objective_tenant_id_status",
                schema: "qams",
                table: "quality_objective",
                columns: new[] { "tenant_id", "status" });
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.quality_objective ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.quality_objective
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.feedback_entry ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.feedback_entry
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback_entry",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "objective_progress",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "quality_objective",
                schema: "qams");
        }
    }
}
