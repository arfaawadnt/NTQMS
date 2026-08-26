using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInfectionControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_exposure",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    inserted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_device_exposure", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "hai_case",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    onset_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organism = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("pk_hai_case", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_exposure_tenant_id_device_type_status",
                schema: "qams",
                table: "device_exposure",
                columns: new[] { "tenant_id", "device_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_device_exposure_tenant_id_inserted_at_utc",
                schema: "qams",
                table: "device_exposure",
                columns: new[] { "tenant_id", "inserted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_hai_case_tenant_id_case_ref",
                schema: "qams",
                table: "hai_case",
                columns: new[] { "tenant_id", "case_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hai_case_tenant_id_onset_date_utc",
                schema: "qams",
                table: "hai_case",
                columns: new[] { "tenant_id", "onset_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_hai_case_tenant_id_type_status",
                schema: "qams",
                table: "hai_case",
                columns: new[] { "tenant_id", "type", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy on both tables.
            migrationBuilder.Sql("""
                ALTER TABLE qams.device_exposure ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.device_exposure FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.device_exposure;
                CREATE POLICY tenant_isolation ON qams.device_exposure
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');

                ALTER TABLE qams.hai_case ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.hai_case FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.hai_case;
                CREATE POLICY tenant_isolation ON qams.hai_case
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');
                """);

            // Value domains, derived from the C# enums (never guessed).
            migrationBuilder.Sql("""
                ALTER TABLE qams.device_exposure ADD CONSTRAINT ck_device_exposure_device_type_domain
                  CHECK (device_type IN ('CentralLine','UrinaryCatheter','Ventilator')) NOT VALID;
                ALTER TABLE qams.device_exposure VALIDATE CONSTRAINT ck_device_exposure_device_type_domain;

                ALTER TABLE qams.device_exposure ADD CONSTRAINT ck_device_exposure_status_domain
                  CHECK (status IN ('InPlace','Removed')) NOT VALID;
                ALTER TABLE qams.device_exposure VALIDATE CONSTRAINT ck_device_exposure_status_domain;

                ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_type_domain
                  CHECK (type IN ('Clabsi','Cauti','Vap','Ssi')) NOT VALID;
                ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_type_domain;

                ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
                ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_exposure",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "hai_case",
                schema: "qams");
        }
    }
}
