using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                schema: "qams",
                table: "user_account",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until_utc",
                schema: "qams",
                table: "user_account",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "qams",
                table: "user_account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_secret",
                schema: "qams",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pin_hash",
                schema: "qams",
                table: "user_account",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_trail",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prev_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entry_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_trail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "electronic_signature",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_display = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_electronic_signature", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_event",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_event", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_trail_occurred_at_utc",
                schema: "audit",
                table: "audit_trail",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_trail_tenant_id_sequence",
                schema: "audit",
                table: "audit_trail",
                columns: new[] { "tenant_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_electronic_signature_subject_ref",
                schema: "audit",
                table: "electronic_signature",
                column: "subject_ref");

            migrationBuilder.CreateIndex(
                name: "ix_electronic_signature_tenant_id_signed_at_utc",
                schema: "audit",
                table: "electronic_signature",
                columns: new[] { "tenant_id", "signed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_security_event_occurred_at_utc",
                schema: "audit",
                table: "security_event",
                column: "occurred_at_utc");
        
            // Tamper protection: RLS on the audit ledgers, plus a guard function
            // rejecting UPDATE/DELETE so the trail is append-only at the engine
            // (in production the runtime role is granted INSERT/SELECT only; this
            // trigger is defence-in-depth for any role).
            migrationBuilder.Sql(@"
                ALTER TABLE audit.audit_trail ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON audit.audit_trail
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE audit.electronic_signature ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON audit.electronic_signature
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                CREATE OR REPLACE FUNCTION audit.reject_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit ledgers are append-only';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER audit_trail_append_only BEFORE UPDATE OR DELETE ON audit.audit_trail
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER signature_append_only BEFORE UPDATE OR DELETE ON audit.electronic_signature
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER security_event_append_only BEFORE UPDATE OR DELETE ON audit.security_event
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_trail",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "electronic_signature",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "security_event",
                schema: "audit");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "locked_until_utc",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "mfa_secret",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "pin_hash",
                schema: "qams",
                table: "user_account");
        }
    }
}
