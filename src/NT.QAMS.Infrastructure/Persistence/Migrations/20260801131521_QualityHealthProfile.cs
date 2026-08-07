using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityHealthProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quality_health_profile",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_health_profile", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "quality_health_weight",
                schema: "qams",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_health_weight", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_quality_health_weight_profile",
                        columns: x => new { x.tenant_id, x.profile_id },
                        principalSchema: "qams",
                        principalTable: "quality_health_profile",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quality_health_profile_tenant",
                schema: "qams",
                table: "quality_health_profile",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quality_health_weight_category",
                schema: "qams",
                table: "quality_health_weight",
                columns: new[] { "tenant_id", "profile_id", "category" },
                unique: true);

            // ── Tenant isolation (EF does not generate row-level security) ──────
            foreach (var table in new[] { "quality_health_profile", "quality_health_weight" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE qams.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.{table} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.{table};
                    CREATE POLICY tenant_isolation ON qams.{table}
                      FOR ALL
                      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on')
                      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on');
                    """);
            }

            // ── Value domains, derived from the C# enums, never guessed ─────────
            // QualityHealthCategory (9 members) and QualityHealthProfile's 0..100
            // weight bound. Both are NOT VALID then VALIDATE to avoid a long lock;
            // on a fresh table the validation is instant.
            migrationBuilder.Sql("""
                ALTER TABLE qams.quality_health_weight
                  ADD CONSTRAINT ck_quality_health_weight_category_domain
                  CHECK (category IN ('DocumentControl','NonconformanceCapa','Complaints',
                                      'InternalAudit','Equipment','Competency',
                                      'ProficiencyTesting','SupplierQuality','Risk')) NOT VALID;
                ALTER TABLE qams.quality_health_weight
                  VALIDATE CONSTRAINT ck_quality_health_weight_category_domain;

                ALTER TABLE qams.quality_health_weight
                  ADD CONSTRAINT ck_quality_health_weight_range
                  CHECK (weight >= 0 AND weight <= 100) NOT VALID;
                ALTER TABLE qams.quality_health_weight
                  VALIDATE CONSTRAINT ck_quality_health_weight_range;
                """);

            // ── Close the seeding gap for tenants that already exist ────────────
            // Role seeding is idempotent per role name, so tenants provisioned
            // before this release would never receive the new 'reports.manage' key
            // and no one could reach the weighting screen. Grant it to exactly the
            // system roles whose definitions now include it (TenantAdministrator
            // and QualityManager); tenant-authored roles are left untouched,
            // because their privileges are the customer's decision, not ours.
            migrationBuilder.Sql("""
                SET LOCAL app.bypass_rls = 'on';

                INSERT INTO qams.role_permission (tenant_id, role_id, permission_key)
                SELECT r.tenant_id, r.id, 'reports.manage'
                FROM qams.role r
                WHERE r.is_system = true
                  AND r.name IN ('Tenant Administrator', 'Quality Manager')
                  AND NOT EXISTS (
                    SELECT 1 FROM qams.role_permission rp
                    WHERE rp.tenant_id = r.tenant_id
                      AND rp.role_id = r.id
                      AND rp.permission_key = 'reports.manage');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The grant is part of what Up() did, so Down() withdraws it. Bypass is
            // transaction-local and required: role_permission is a FORCE-RLS table,
            // so an unscoped DELETE would otherwise match nothing.
            migrationBuilder.Sql("""
                SET LOCAL app.bypass_rls = 'on';
                DELETE FROM qams.role_permission WHERE permission_key = 'reports.manage';
                """);

            // Policies and CHECK constraints drop with their tables.
            migrationBuilder.DropTable(
                name: "quality_health_weight",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "quality_health_profile",
                schema: "qams");
        }
    }
}
