using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practitioner",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    practitioner_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    specialty = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    appointed_until = table.Column<DateOnly>(type: "date", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practitioner", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "practitioner_licence",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    verification_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practitioner_licence", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_practitioner_licence_practitioner_tenant_id_practitioner_id",
                        columns: x => new { x.tenant_id, x.practitioner_id },
                        principalSchema: "qams",
                        principalTable: "practitioner",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "practitioner_privilege",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_until = table.Column<DateOnly>(type: "date", nullable: true),
                    denial_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    practitioner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practitioner_privilege", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_practitioner_privilege_practitioner_tenant_id_practitioner_",
                        columns: x => new { x.tenant_id, x.practitioner_id },
                        principalSchema: "qams",
                        principalTable: "practitioner",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_tenant_id_practitioner_ref",
                schema: "qams",
                table: "practitioner",
                columns: new[] { "tenant_id", "practitioner_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_tenant_id_specialty",
                schema: "qams",
                table: "practitioner",
                columns: new[] { "tenant_id", "specialty" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_tenant_id_status",
                schema: "qams",
                table: "practitioner",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_licence_tenant_id_practitioner_id",
                schema: "qams",
                table: "practitioner_licence",
                columns: new[] { "tenant_id", "practitioner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioner_privilege_tenant_id_practitioner_id",
                schema: "qams",
                table: "practitioner_privilege",
                columns: new[] { "tenant_id", "practitioner_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on all three tables (incl. owned children).
            foreach (var t in new[] { "practitioner", "practitioner_licence", "practitioner_privilege" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE qams.{t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.{t} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.{t};
                    CREATE POLICY tenant_isolation ON qams.{t}
                      FOR ALL
                      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on')
                      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on');
                    """);
            }

            // Value domains, derived from the C# enums (never guessed).
            migrationBuilder.Sql("""
                ALTER TABLE qams.practitioner ADD CONSTRAINT ck_practitioner_status_domain
                  CHECK (status IN ('Pending','Credentialed','Suspended')) NOT VALID;
                ALTER TABLE qams.practitioner VALIDATE CONSTRAINT ck_practitioner_status_domain;

                ALTER TABLE qams.practitioner_licence ADD CONSTRAINT ck_practitioner_licence_type_domain
                  CHECK (type IN ('MedicalLicence','NursingLicence','BoardCertification','Bls','Acls','Other')) NOT VALID;
                ALTER TABLE qams.practitioner_licence VALIDATE CONSTRAINT ck_practitioner_licence_type_domain;

                ALTER TABLE qams.practitioner_licence ADD CONSTRAINT ck_practitioner_licence_verification_domain
                  CHECK (verification_status IN ('Pending','Verified')) NOT VALID;
                ALTER TABLE qams.practitioner_licence VALIDATE CONSTRAINT ck_practitioner_licence_verification_domain;

                ALTER TABLE qams.practitioner_privilege ADD CONSTRAINT ck_practitioner_privilege_status_domain
                  CHECK (status IN ('Requested','Granted','Denied','Expired')) NOT VALID;
                ALTER TABLE qams.practitioner_privilege VALIDATE CONSTRAINT ck_practitioner_privilege_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practitioner_licence",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "practitioner_privilege",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "practitioner",
                schema: "qams");
        }
    }
}
