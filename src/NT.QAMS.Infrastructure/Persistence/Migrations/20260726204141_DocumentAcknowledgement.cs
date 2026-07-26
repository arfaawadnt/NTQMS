using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_acknowledgement",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    version_label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_acknowledgement", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_acknowledgement_tenant_id_document_id_version_labe",
                schema: "qams",
                table: "document_acknowledgement",
                columns: new[] { "tenant_id", "document_id", "version_label", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_acknowledgement_tenant_id_user_id",
                schema: "qams",
                table: "document_acknowledgement",
                columns: new[] { "tenant_id", "user_id" });

            // F-01 parity: a new ITenantScoped table must carry the same active,
            // FORCE-d Row-Level Security as every other tenant table.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.document_acknowledgement ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.document_acknowledgement FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.document_acknowledgement;
                CREATE POLICY tenant_isolation ON qams.document_acknowledgement
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
                name: "document_acknowledgement",
                schema: "qams");
        }
    }
}
