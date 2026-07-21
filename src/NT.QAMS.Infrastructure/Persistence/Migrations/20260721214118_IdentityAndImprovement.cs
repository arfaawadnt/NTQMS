using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAndImprovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nonconformance",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nc_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    likelihood = table.Column<int>(type: "integer", nullable: false),
                    rpn = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    raised_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nonconformance", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ref_counter",
                schema: "qams",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ref_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ref_counter", x => new { x.tenant_id, x.ref_type, x.year });
                });

            migrationBuilder.CreateTable(
                name: "user_account",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "capa_action",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nc_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_action", x => x.id);
                    table.ForeignKey(
                        name: "fk_capa_action_nonconformance_nc_id",
                        column: x => x.nc_id,
                        principalSchema: "qams",
                        principalTable: "nonconformance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rca_record",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    analysis = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    investigator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nc_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rca_record", x => x.id);
                    table.ForeignKey(
                        name: "fk_rca_record_nonconformance_nc_id",
                        column: x => x.nc_id,
                        principalSchema: "qams",
                        principalTable: "nonconformance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capa_action_nc_id",
                schema: "qams",
                table: "capa_action",
                column: "nc_id");

            migrationBuilder.CreateIndex(
                name: "ix_nonconformance_tenant_id_nc_ref",
                schema: "qams",
                table: "nonconformance",
                columns: new[] { "tenant_id", "nc_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nonconformance_tenant_id_status",
                schema: "qams",
                table: "nonconformance",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rca_record_nc_id",
                schema: "qams",
                table: "rca_record",
                column: "nc_id");

            // Layer-2 isolation: PostgreSQL row-level security on tenant-scoped
            // business tables. Dormant while the connection role owns the tables
            // (Phase 0/1 single-role deployment); becomes enforcing the moment the
            // runtime role is split from the owner role, without schema changes.
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.nonconformance
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_user_account_tenant_id_email",
                schema: "qams",
                table: "user_account",
                columns: new[] { "tenant_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capa_action",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "rca_record",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "ref_counter",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "user_account",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "nonconformance",
                schema: "qams");
        }
    }
}
