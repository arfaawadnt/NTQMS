using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_program",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("pk_audit_program", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "planned_audit",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    standard_chapter = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    planned_quarter = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_audit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    audit_program_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planned_audit", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_planned_audit_audit_program_tenant_id_audit_program_id",
                        columns: x => new { x.tenant_id, x.audit_program_id },
                        principalSchema: "qams",
                        principalTable: "audit_program",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_program_tenant_id_year",
                schema: "qams",
                table: "audit_program",
                columns: new[] { "tenant_id", "year" });

            migrationBuilder.CreateIndex(
                name: "ix_planned_audit_tenant_id_audit_program_id",
                schema: "qams",
                table: "planned_audit",
                columns: new[] { "tenant_id", "audit_program_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on both tenant tables.
            foreach (var t in new[] { "audit_program", "planned_audit" })
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
                ALTER TABLE qams.audit_program ADD CONSTRAINT ck_audit_program_status_domain
                  CHECK (status IN ('Draft','Active','Closed')) NOT VALID;
                ALTER TABLE qams.audit_program VALIDATE CONSTRAINT ck_audit_program_status_domain;

                ALTER TABLE qams.planned_audit ADD CONSTRAINT ck_planned_audit_priority_domain
                  CHECK (priority IN ('Low','Medium','High')) NOT VALID;
                ALTER TABLE qams.planned_audit VALIDATE CONSTRAINT ck_planned_audit_priority_domain;

                ALTER TABLE qams.planned_audit ADD CONSTRAINT ck_planned_audit_status_domain
                  CHECK (status IN ('Planned','Scheduled','Completed')) NOT VALID;
                ALTER TABLE qams.planned_audit VALIDATE CONSTRAINT ck_planned_audit_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planned_audit",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "audit_program",
                schema: "qams");
        }
    }
}
