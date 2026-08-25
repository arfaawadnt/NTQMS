using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccreditationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_link",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    standard_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_link", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "standard_set",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    framework = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("pk_standard_set", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "standard_element",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    chapter_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    standard_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    element_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    compliance_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assessment_note = table.Column<string>(type: "text", nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assessed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    standard_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_standard_element", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_standard_element_standard_set_tenant_id_standard_set_id",
                        columns: x => new { x.tenant_id, x.standard_set_id },
                        principalSchema: "qams",
                        principalTable: "standard_set",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_link_tenant_id_element_id",
                schema: "qams",
                table: "evidence_link",
                columns: new[] { "tenant_id", "element_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_link_tenant_id_standard_set_id",
                schema: "qams",
                table: "evidence_link",
                columns: new[] { "tenant_id", "standard_set_id" });

            migrationBuilder.CreateIndex(
                name: "ux_standard_element_set_code",
                schema: "qams",
                table: "standard_element",
                columns: new[] { "tenant_id", "standard_set_id", "element_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_standard_set_tenant_id_status",
                schema: "qams",
                table: "standard_set",
                columns: new[] { "tenant_id", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy on all three tenant tables.
            foreach (var t in new[] { "standard_set", "standard_element", "evidence_link" })
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
                ALTER TABLE qams.standard_set ADD CONSTRAINT ck_standard_set_framework_domain
                  CHECK (framework IN ('GAHAR','JCI','ISO9001','ISO15189','Other')) NOT VALID;
                ALTER TABLE qams.standard_set VALIDATE CONSTRAINT ck_standard_set_framework_domain;

                ALTER TABLE qams.standard_set ADD CONSTRAINT ck_standard_set_status_domain
                  CHECK (status IN ('Draft','Active','Archived')) NOT VALID;
                ALTER TABLE qams.standard_set VALIDATE CONSTRAINT ck_standard_set_status_domain;

                ALTER TABLE qams.standard_element ADD CONSTRAINT ck_standard_element_compliance_status_domain
                  CHECK (compliance_status IN ('NotAssessed','Compliant','PartiallyCompliant','NonCompliant','NotApplicable')) NOT VALID;
                ALTER TABLE qams.standard_element VALIDATE CONSTRAINT ck_standard_element_compliance_status_domain;

                ALTER TABLE qams.evidence_link ADD CONSTRAINT ck_evidence_link_source_type_domain
                  CHECK (source_type IN ('Document','Incident','Nonconformance','Audit','Indicator','Training','Committee','Other')) NOT VALID;
                ALTER TABLE qams.evidence_link VALIDATE CONSTRAINT ck_evidence_link_source_type_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_link",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "standard_element",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "standard_set",
                schema: "qams");
        }
    }
}
