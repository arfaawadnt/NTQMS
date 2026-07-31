using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RolePrivilegeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                schema: "qams",
                table: "user_account",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                schema: "qams",
                table: "user_account",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "role",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    default_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_access",
                schema: "qams",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_branch_access", x => new { x.user_id, x.branch_id });
                    table.ForeignKey(
                        name: "fk_user_branch_access_user_account_user_id",
                        column: x => x.user_id,
                        principalSchema: "qams",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_department_access",
                schema: "qams",
                columns: table => new
                {
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_department_access", x => new { x.user_id, x.department_id });
                    table.ForeignKey(
                        name: "fk_user_department_access_user_account_user_id",
                        column: x => x.user_id,
                        principalSchema: "qams",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "qams",
                columns: table => new
                {
                    permission_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permission", x => new { x.role_id, x.permission_key });
                    table.ForeignKey(
                        name: "fk_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "qams",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_account_role_id",
                schema: "qams",
                table: "user_account",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_tenant_id_normalized_name",
                schema: "qams",
                table: "role",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            // Layer-2 isolation on the aggregate root. The three child tables
            // (role_permission, user_branch_access, user_department_access) carry
            // no tenant_id and follow the owned-table precedent (capa_action,
            // rca_record): they are reachable only through their RLS-protected
            // parent in every application path.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.role ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.role FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.role;
                CREATE POLICY tenant_isolation ON qams.role
                USING (
                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                    OR current_setting('app.bypass_rls', true) = 'on'
                )
                WITH CHECK (
                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                    OR current_setting('app.bypass_rls', true) = 'on'
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permission",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "user_branch_access",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "user_department_access",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "role",
                schema: "qams");

            migrationBuilder.DropIndex(
                name: "ix_user_account_role_id",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "role_id",
                schema: "qams",
                table: "user_account");
        }
    }
}
