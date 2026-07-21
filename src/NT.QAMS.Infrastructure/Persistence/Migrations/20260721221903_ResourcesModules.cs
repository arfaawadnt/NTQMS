using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResourcesModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competency_record",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validity_months = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    authorized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competency_record", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    calibration_interval_days = table.Column<int>(type: "integer", nullable: false),
                    grace_period_days = table.Column<int>(type: "integer", nullable: false),
                    last_calibration_at = table.Column<DateOnly>(type: "date", nullable: true),
                    next_calibration_due = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_assignment",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_assignment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assessment_result",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    assessor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_result", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_result_competency_record_competency_id",
                        column: x => x.competency_id,
                        principalSchema: "qams",
                        principalTable: "competency_record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calibration_record",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at = table.Column<DateOnly>(type: "date", nullable: false),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    result = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certificate_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calibration_record", x => x.id);
                    table.ForeignKey(
                        name: "fk_calibration_record_equipment_item_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "qams",
                        principalTable: "equipment_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_record",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at = table.Column<DateOnly>(type: "date", nullable: false),
                    work_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_record", x => x.id);
                    table.ForeignKey(
                        name: "fk_maintenance_record_equipment_item_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "qams",
                        principalTable: "equipment_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_result_competency_id",
                schema: "qams",
                table: "assessment_result",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_calibration_record_equipment_id",
                schema: "qams",
                table: "calibration_record",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_competency_record_tenant_id_status",
                schema: "qams",
                table: "competency_record",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_competency_record_tenant_id_trainee_id",
                schema: "qams",
                table: "competency_record",
                columns: new[] { "tenant_id", "trainee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_item_tenant_id_code",
                schema: "qams",
                table: "equipment_item",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_item_tenant_id_serial_number",
                schema: "qams",
                table: "equipment_item",
                columns: new[] { "tenant_id", "serial_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_item_tenant_id_status",
                schema: "qams",
                table: "equipment_item",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_record_equipment_id",
                schema: "qams",
                table: "maintenance_record",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_assignment_tenant_id_trainee_id_completed",
                schema: "qams",
                table: "training_assignment",
                columns: new[] { "tenant_id", "trainee_id", "completed" });

            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql("""
                ALTER TABLE qams.equipment_item ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.equipment_item
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.competency_record ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.competency_record
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.training_assignment ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.training_assignment
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_result",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "calibration_record",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "maintenance_record",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "training_assignment",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "competency_record",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "equipment_item",
                schema: "qams");
        }
    }
}
