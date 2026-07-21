using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordsAndSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archive_entry",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archive_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    snapshot_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    retention_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    archived_on = table.Column<DateOnly>(type: "date", nullable: false),
                    retention_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    state = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    archived_by = table.Column<Guid>(type: "uuid", nullable: false),
                    disposal_authorized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archive_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "escalation_timer",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    next_step_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escalation_timer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_definition",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_hours = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_task",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_task", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archive_entry_tenant_id_source_module_source_ref",
                schema: "qams",
                table: "archive_entry",
                columns: new[] { "tenant_id", "source_module", "source_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_archive_entry_tenant_id_state",
                schema: "qams",
                table: "archive_entry",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_escalation_timer_next_step_at_utc",
                schema: "qams",
                table: "escalation_timer",
                column: "next_step_at_utc",
                filter: "active = true");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_timer_subject_ref",
                schema: "qams",
                table: "escalation_timer",
                column: "subject_ref");

            migrationBuilder.CreateIndex(
                name: "ix_sla_definition_tenant_id_module_severity",
                schema: "qams",
                table: "sla_definition",
                columns: new[] { "tenant_id", "module", "severity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_task_subject_ref",
                schema: "qams",
                table: "work_task",
                column: "subject_ref");

            migrationBuilder.CreateIndex(
                name: "ix_work_task_tenant_id_assignee_role_status",
                schema: "qams",
                table: "work_task",
                columns: new[] { "tenant_id", "assignee_role", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_task_tenant_id_assignee_user_id_status",
                schema: "qams",
                table: "work_task",
                columns: new[] { "tenant_id", "assignee_user_id", "status" });
        
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.archive_entry ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.archive_entry
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.sla_definition ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.sla_definition
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.work_task ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.work_task
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.escalation_timer ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.escalation_timer
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archive_entry",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "escalation_timer",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "sla_definition",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "work_task",
                schema: "qams");
        }
    }
}
