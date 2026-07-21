using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrgAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lov_entry",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_fr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lov_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_dispatch",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    email_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    error = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_by_recipient = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_dispatch", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_rule",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recipient_roles = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    subject_template = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_template = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_rule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_catalog_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    test_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    methodology = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    turnaround_hours = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_catalog_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "department",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_department", x => x.id);
                    table.ForeignKey(
                        name: "fk_department_branch_branch_id",
                        column: x => x.branch_id,
                        principalSchema: "qams",
                        principalTable: "branch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branch_tenant_id_code",
                schema: "qams",
                table: "branch",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_department_branch_id",
                schema: "qams",
                table: "department",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_department_tenant_id_branch_id_code",
                schema: "qams",
                table: "department",
                columns: new[] { "tenant_id", "branch_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lov_entry_tenant_id_category_code",
                schema: "qams",
                table: "lov_entry",
                columns: new[] { "tenant_id", "category", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_dispatch_source_event_id",
                schema: "qams",
                table: "notification_dispatch",
                column: "source_event_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_dispatch_tenant_id_recipient_user_id_read_by_r",
                schema: "qams",
                table: "notification_dispatch",
                columns: new[] { "tenant_id", "recipient_user_id", "read_by_recipient" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_rule_tenant_id_event_key",
                schema: "qams",
                table: "notification_rule",
                columns: new[] { "tenant_id", "event_key" });

            migrationBuilder.CreateIndex(
                name: "ix_test_catalog_item_tenant_id_test_code",
                schema: "qams",
                table: "test_catalog_item",
                columns: new[] { "tenant_id", "test_code" },
                unique: true);
        
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.branch ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.branch
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.department ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.department
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.test_catalog_item ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.test_catalog_item
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.lov_entry ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.lov_entry
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.notification_rule ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.notification_rule
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.notification_dispatch ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.notification_dispatch
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "lov_entry",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "notification_dispatch",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "notification_rule",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "test_catalog_item",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "branch",
                schema: "qams");
        }
    }
}
