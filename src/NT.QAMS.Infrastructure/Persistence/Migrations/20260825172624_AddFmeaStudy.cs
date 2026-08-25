using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFmeaStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fmea_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fmea_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    process_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_fmea_study", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "fmea_failure_mode",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_step = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    failure_mode_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    effect = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cause = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    occurrence = table.Column<int>(type: "integer", nullable: false),
                    detection = table.Column<int>(type: "integer", nullable: false),
                    rpn = table.Column<int>(type: "integer", nullable: false),
                    recommended_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    action_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    residual_severity = table.Column<int>(type: "integer", nullable: true),
                    residual_occurrence = table.Column<int>(type: "integer", nullable: true),
                    residual_detection = table.Column<int>(type: "integer", nullable: true),
                    residual_rpn = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fmea_study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fmea_failure_mode", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_fmea_failure_mode_fmea_study_tenant_id_fmea_study_id",
                        columns: x => new { x.tenant_id, x.fmea_study_id },
                        principalSchema: "qams",
                        principalTable: "fmea_study",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fmea_failure_mode_tenant_id_fmea_study_id",
                schema: "qams",
                table: "fmea_failure_mode",
                columns: new[] { "tenant_id", "fmea_study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fmea_study_tenant_id_fmea_ref",
                schema: "qams",
                table: "fmea_study",
                columns: new[] { "tenant_id", "fmea_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fmea_study_tenant_id_status",
                schema: "qams",
                table: "fmea_study",
                columns: new[] { "tenant_id", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy on both tenant tables.
            foreach (var t in new[] { "fmea_study", "fmea_failure_mode" })
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
                ALTER TABLE qams.fmea_study ADD CONSTRAINT ck_fmea_study_type_domain
                  CHECK (type IN ('Fmea','Hfmea')) NOT VALID;
                ALTER TABLE qams.fmea_study VALIDATE CONSTRAINT ck_fmea_study_type_domain;

                ALTER TABLE qams.fmea_study ADD CONSTRAINT ck_fmea_study_status_domain
                  CHECK (status IN ('Draft','Active','Closed')) NOT VALID;
                ALTER TABLE qams.fmea_study VALIDATE CONSTRAINT ck_fmea_study_status_domain;

                ALTER TABLE qams.fmea_failure_mode ADD CONSTRAINT ck_fmea_failure_mode_status_domain
                  CHECK (status IN ('Open','Actioned')) NOT VALID;
                ALTER TABLE qams.fmea_failure_mode VALIDATE CONSTRAINT ck_fmea_failure_mode_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fmea_failure_mode",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "fmea_study",
                schema: "qams");
        }
    }
}
