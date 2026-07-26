using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserAccessReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_access_review",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accounts_reviewed = table.Column<int>(type: "integer", nullable: true),
                    changes_required = table.Column<bool>(type: "boolean", nullable: true),
                    conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_access_review", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_access_review_tenant_id_review_ref",
                schema: "qams",
                table: "user_access_review",
                columns: new[] { "tenant_id", "review_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_access_review_tenant_id_status",
                schema: "qams",
                table: "user_access_review",
                columns: new[] { "tenant_id", "status" });

            // F-01 parity: a new ITenantScoped table must carry the same active,
            // FORCE-d Row-Level Security as every other tenant table.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.user_access_review ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.user_access_review FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.user_access_review;
                CREATE POLICY tenant_isolation ON qams.user_access_review
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
                name: "user_access_review",
                schema: "qams");
        }
    }
}
