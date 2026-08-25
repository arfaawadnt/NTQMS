using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_endpoint",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_message_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_endpoint", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "integration_message",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_detail = table.Column<string>(type: "text", nullable: true),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_message", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "patient_stay",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    encounter_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    discharged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_stay", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_endpoint_tenant_id_status",
                schema: "qams",
                table: "integration_endpoint",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_message_tenant_id_endpoint_id_status",
                schema: "qams",
                table: "integration_message",
                columns: new[] { "tenant_id", "endpoint_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_integration_message_dedup",
                schema: "qams",
                table: "integration_message",
                columns: new[] { "tenant_id", "endpoint_id", "dedup_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_stay_tenant_id_status",
                schema: "qams",
                table: "patient_stay",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_patient_stay_encounter",
                schema: "qams",
                table: "patient_stay",
                columns: new[] { "tenant_id", "encounter_ref" },
                unique: true);

            // Mandatory FORCE RLS + tenant_isolation policy on every tenant table.
            foreach (var t in new[] { "integration_endpoint", "integration_message", "patient_stay" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE qams.{t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.{t} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.{t};
                    CREATE POLICY tenant_isolation ON qams.{t}
                      FOR ALL
                      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on')
                      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on');
                    """);
            }

            // Value domains, derived from the C# enums (never guessed).
            migrationBuilder.Sql("""
                ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_system_domain
                  CHECK (system IN ('His','Lis','Pharmacy','Hr','Other')) NOT VALID;
                ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_system_domain;

                ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_protocol_domain
                  CHECK (protocol IN ('Hl7V2','FhirR4','FileExtract','DbExtract')) NOT VALID;
                ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_protocol_domain;

                ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_status_domain
                  CHECK (status IN ('Active','Suspended')) NOT VALID;
                ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_status_domain;

                ALTER TABLE qams.integration_message ADD CONSTRAINT ck_integration_message_status_domain
                  CHECK (status IN ('Received','Processed','Failed')) NOT VALID;
                ALTER TABLE qams.integration_message VALIDATE CONSTRAINT ck_integration_message_status_domain;

                ALTER TABLE qams.patient_stay ADD CONSTRAINT ck_patient_stay_status_domain
                  CHECK (status IN ('Admitted','Discharged')) NOT VALID;
                ALTER TABLE qams.patient_stay VALIDATE CONSTRAINT ck_patient_stay_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_endpoint",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "integration_message",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "patient_stay",
                schema: "qams");
        }
    }
}
