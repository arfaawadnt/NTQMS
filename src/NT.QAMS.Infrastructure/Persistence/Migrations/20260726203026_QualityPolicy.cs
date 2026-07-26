using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quality_policy",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    statement = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_policy", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quality_policy_tenant_id_policy_ref",
                schema: "qams",
                table: "quality_policy",
                columns: new[] { "tenant_id", "policy_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_policy_tenant_id_status",
                schema: "qams",
                table: "quality_policy",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_quality_policy_tenant_id_version",
                schema: "qams",
                table: "quality_policy",
                columns: new[] { "tenant_id", "version" },
                unique: true);

            // F-01 parity: a new ITenantScoped table must carry the same active,
            // FORCE-d Row-Level Security as every other tenant table — otherwise it
            // would be a hole in the Layer-2 isolation. Mirrors ActivateForcedTenantRls.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.quality_policy ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.quality_policy FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.quality_policy;
                CREATE POLICY tenant_isolation ON qams.quality_policy
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
                name: "quality_policy",
                schema: "qams");
        }
    }
}
