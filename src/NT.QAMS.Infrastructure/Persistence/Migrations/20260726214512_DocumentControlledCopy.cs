using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentControlledCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_controlled_copy",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    version_label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    copy_number = table.Column<int>(type: "integer", nullable: false),
                    holder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_controlled_copy", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_controlled_copy_tenant_id_document_id_copy_number",
                schema: "qams",
                table: "document_controlled_copy",
                columns: new[] { "tenant_id", "document_id", "copy_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_controlled_copy_tenant_id_status",
                schema: "qams",
                table: "document_controlled_copy",
                columns: new[] { "tenant_id", "status" });

            // F-01 parity: a new ITenantScoped table must carry the same active,
            // FORCE-d Row-Level Security as every other tenant table.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.document_controlled_copy ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.document_controlled_copy FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.document_controlled_copy;
                CREATE POLICY tenant_isolation ON qams.document_controlled_copy
                USING (
                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                    OR current_setting('app.bypass_rls', true) = 'on'
                )
                WITH CHECK (
                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                    OR current_setting('app.bypass_rls', true) = 'on'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_controlled_copy",
                schema: "qams");
        }
    }
}
