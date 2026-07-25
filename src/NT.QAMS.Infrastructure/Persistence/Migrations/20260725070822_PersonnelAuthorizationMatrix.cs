using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelAuthorizationMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "test_authorization",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suspension_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_authorization", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_test_authorization_competency_record_id",
                schema: "qams",
                table: "test_authorization",
                column: "competency_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_authorization_tenant_id_status",
                schema: "qams",
                table: "test_authorization",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_test_authorization_tenant_id_test_catalog_item_id",
                schema: "qams",
                table: "test_authorization",
                columns: new[] { "tenant_id", "test_catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_test_authorization_tenant_id_user_id",
                schema: "qams",
                table: "test_authorization",
                columns: new[] { "tenant_id", "user_id" });
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.test_authorization ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.test_authorization
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "test_authorization",
                schema: "qams");
        }
    }
}
