using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_mail_settings",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    from_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    reply_to = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    brand_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    footer_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_mail_settings", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_mail_settings_tenant",
                schema: "qams",
                table: "tenant_mail_settings",
                column: "tenant_id",
                unique: true);

            // Mandatory FORCE RLS + tenant_isolation policy (EF does not generate it).
            migrationBuilder.Sql("""
                ALTER TABLE qams.tenant_mail_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.tenant_mail_settings FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.tenant_mail_settings;
                CREATE POLICY tenant_isolation ON qams.tenant_mail_settings
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');
                """);

            // Brand colour, when present, is a #RRGGBB hex value (the command validator also enforces this).
            migrationBuilder.Sql("""
                ALTER TABLE qams.tenant_mail_settings
                  ADD CONSTRAINT ck_tenant_mail_settings_brand_color
                  CHECK (brand_color IS NULL OR brand_color ~ '^#[0-9A-Fa-f]{6}$') NOT VALID;
                ALTER TABLE qams.tenant_mail_settings VALIDATE CONSTRAINT ck_tenant_mail_settings_brand_color;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_mail_settings",
                schema: "qams");
        }
    }
}
