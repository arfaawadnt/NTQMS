using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentReadAndUnderstand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audience_scope",
                schema: "qams",
                table: "controlled_document",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "AllStaff");

            migrationBuilder.AddColumn<bool>(
                name: "requires_acknowledgement",
                schema: "qams",
                table: "controlled_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "document_audience_department",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_audience_department", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_document_audience_department_controlled_document_tenant_id_",
                        columns: x => new { x.tenant_id, x.document_id },
                        principalSchema: "qams",
                        principalTable: "controlled_document",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_audience_department_tenant_id_document_id",
                schema: "qams",
                table: "document_audience_department",
                columns: new[] { "tenant_id", "document_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on the new tenant table.
            migrationBuilder.Sql("""
                ALTER TABLE qams.document_audience_department ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.document_audience_department FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.document_audience_department;
                CREATE POLICY tenant_isolation ON qams.document_audience_department
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');
                """);

            // Value domain for the audience scope, derived from the DocumentAudienceScope enum.
            migrationBuilder.Sql("""
                ALTER TABLE qams.controlled_document ADD CONSTRAINT ck_controlled_document_audience_scope_domain
                  CHECK (audience_scope IN ('AllStaff','ByDepartment')) NOT VALID;
                ALTER TABLE qams.controlled_document VALIDATE CONSTRAINT ck_controlled_document_audience_scope_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_audience_department",
                schema: "qams");

            migrationBuilder.DropColumn(
                name: "audience_scope",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropColumn(
                name: "requires_acknowledgement",
                schema: "qams",
                table: "controlled_document");
        }
    }
}
