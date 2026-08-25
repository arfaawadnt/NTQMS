using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    harm_grade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_sentinel = table.Column<bool>(type: "boolean", nullable: false),
                    sentinel_declared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    anonymous_reference_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    investigator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    investigation_summary = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    closure_summary = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incident", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "incident_contributing_factor",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incident_contributing_factor", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_incident_contributing_factor_incident_tenant_id_incident_id",
                        columns: x => new { x.tenant_id, x.incident_id },
                        principalSchema: "qams",
                        principalTable: "incident",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_timeline_entry",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incident_timeline_entry", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_incident_timeline_entry_incident_tenant_id_incident_id",
                        columns: x => new { x.tenant_id, x.incident_id },
                        principalSchema: "qams",
                        principalTable: "incident",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_tenant_id_anonymous_reference_hash",
                schema: "qams",
                table: "incident",
                columns: new[] { "tenant_id", "anonymous_reference_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_tenant_id_incident_ref",
                schema: "qams",
                table: "incident",
                columns: new[] { "tenant_id", "incident_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incident_tenant_id_status",
                schema: "qams",
                table: "incident",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_contributing_factor_tenant_id_incident_id",
                schema: "qams",
                table: "incident_contributing_factor",
                columns: new[] { "tenant_id", "incident_id" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_timeline_entry_tenant_id_incident_id",
                schema: "qams",
                table: "incident_timeline_entry",
                columns: new[] { "tenant_id", "incident_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on every tenant table,
            // including the owned children (EF does not generate this).
            foreach (var t in new[] { "incident", "incident_contributing_factor", "incident_timeline_entry" })
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

            // Value domains, derived from the C# enums (never guessed). NOT VALID then
            // VALIDATE avoids a long exclusive lock on an empty table but keeps the pattern.
            migrationBuilder.Sql("""
                ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_status_domain
                  CHECK (status IN ('Reported','Triaged','UnderInvestigation','PendingReview','Closed','Rejected')) NOT VALID;
                ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_status_domain;

                ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_category_domain
                  CHECK (category IN ('Medication','Fall','Procedural','Transfusion','Device','Laboratory','Security','Documentation','Other')) NOT VALID;
                ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_category_domain;

                ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_harm_grade_domain
                  CHECK (harm_grade IN ('NearMiss','NoHarm','Minor','Moderate','Severe','Death')) NOT VALID;
                ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_harm_grade_domain;

                ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_channel_domain
                  CHECK (channel IN ('Web','Mobile','Kiosk','Phone','Paper')) NOT VALID;
                ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_channel_domain;

                ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_anonymous_reference_hash
                  CHECK (anonymous_reference_hash IS NULL OR anonymous_reference_hash ~ '^[0-9a-f]{64}$') NOT VALID;
                ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_anonymous_reference_hash;

                ALTER TABLE qams.incident_contributing_factor ADD CONSTRAINT ck_incident_contributing_factor_category_domain
                  CHECK (category IN ('People','Process','Equipment','Environment','Materials','Management','Other')) NOT VALID;
                ALTER TABLE qams.incident_contributing_factor VALIDATE CONSTRAINT ck_incident_contributing_factor_category_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_contributing_factor",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "incident_timeline_entry",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "incident",
                schema: "qams");
        }
    }
}
