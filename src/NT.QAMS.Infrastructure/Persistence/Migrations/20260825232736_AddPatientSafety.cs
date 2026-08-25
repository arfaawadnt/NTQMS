using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_safety_event",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    harm_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_safety_event", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_patient_safety_event_tenant_id_event_ref",
                schema: "qams",
                table: "patient_safety_event",
                columns: new[] { "tenant_id", "event_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_safety_event_tenant_id_occurred_at_utc",
                schema: "qams",
                table: "patient_safety_event",
                columns: new[] { "tenant_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_patient_safety_event_tenant_id_type_status",
                schema: "qams",
                table: "patient_safety_event",
                columns: new[] { "tenant_id", "type", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy.
            migrationBuilder.Sql("""
                ALTER TABLE qams.patient_safety_event ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.patient_safety_event FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.patient_safety_event;
                CREATE POLICY tenant_isolation ON qams.patient_safety_event
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');
                """);

            // Value domains, derived from the C# enums (never guessed). Stage is nullable (falls have none).
            migrationBuilder.Sql("""
                ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_type_domain
                  CHECK (type IN ('Fall','PressureInjury')) NOT VALID;
                ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_type_domain;

                ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_harm_level_domain
                  CHECK (harm_level IN ('None','Minor','Moderate','Severe','Death')) NOT VALID;
                ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_harm_level_domain;

                ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_origin_domain
                  CHECK (origin IN ('PresentOnAdmission','HospitalAcquired')) NOT VALID;
                ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_origin_domain;

                ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_stage_domain
                  CHECK (stage IS NULL OR stage IN ('Stage1','Stage2','Stage3','Stage4','Unstageable','DeepTissueInjury')) NOT VALID;
                ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_stage_domain;

                ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
                ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_safety_event",
                schema: "qams");
        }
    }
}
