using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentOfCare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drill",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    drill_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    participant_count = table.Column<int>(type: "integer", nullable: true),
                    evaluation_score = table.Column<int>(type: "integer", nullable: true),
                    improvement_notes = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drill", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "safety_round",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    area = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    conducted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_safety_round", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "safety_round_finding",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    corrective_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    safety_round_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_safety_round_finding", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_safety_round_finding_safety_round_tenant_id_safety_round_id",
                        columns: x => new { x.tenant_id, x.safety_round_id },
                        principalSchema: "qams",
                        principalTable: "safety_round",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drill_tenant_id_drill_ref",
                schema: "qams",
                table: "drill",
                columns: new[] { "tenant_id", "drill_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_drill_tenant_id_scheduled_date",
                schema: "qams",
                table: "drill",
                columns: new[] { "tenant_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "ix_drill_tenant_id_type_status",
                schema: "qams",
                table: "drill",
                columns: new[] { "tenant_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_safety_round_tenant_id_round_ref",
                schema: "qams",
                table: "safety_round",
                columns: new[] { "tenant_id", "round_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_safety_round_tenant_id_scheduled_date",
                schema: "qams",
                table: "safety_round",
                columns: new[] { "tenant_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "ix_safety_round_tenant_id_type_status",
                schema: "qams",
                table: "safety_round",
                columns: new[] { "tenant_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_safety_round_finding_tenant_id_safety_round_id",
                schema: "qams",
                table: "safety_round_finding",
                columns: new[] { "tenant_id", "safety_round_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on all three tables (incl. owned findings).
            foreach (var t in new[] { "safety_round", "safety_round_finding", "drill" })
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

            // Value domains, derived from the C# enums (never guessed), plus the drill score range.
            migrationBuilder.Sql("""
                ALTER TABLE qams.safety_round ADD CONSTRAINT ck_safety_round_type_domain
                  CHECK (type IN ('FireSafety','InfectionControl','GeneralSafety','HazardousMaterials','Utilities','Security')) NOT VALID;
                ALTER TABLE qams.safety_round VALIDATE CONSTRAINT ck_safety_round_type_domain;

                ALTER TABLE qams.safety_round ADD CONSTRAINT ck_safety_round_status_domain
                  CHECK (status IN ('Scheduled','InProgress','Completed')) NOT VALID;
                ALTER TABLE qams.safety_round VALIDATE CONSTRAINT ck_safety_round_status_domain;

                ALTER TABLE qams.safety_round_finding ADD CONSTRAINT ck_safety_round_finding_severity_domain
                  CHECK (severity IN ('Low','Medium','High','Critical')) NOT VALID;
                ALTER TABLE qams.safety_round_finding VALIDATE CONSTRAINT ck_safety_round_finding_severity_domain;

                ALTER TABLE qams.safety_round_finding ADD CONSTRAINT ck_safety_round_finding_status_domain
                  CHECK (status IN ('Open','Resolved')) NOT VALID;
                ALTER TABLE qams.safety_round_finding VALIDATE CONSTRAINT ck_safety_round_finding_status_domain;

                ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_type_domain
                  CHECK (type IN ('Fire','Evacuation','CodeBlue','Disaster','Hazmat','ActiveShooter')) NOT VALID;
                ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_type_domain;

                ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_status_domain
                  CHECK (status IN ('Scheduled','Executed','Evaluated')) NOT VALID;
                ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_status_domain;

                ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_score_range
                  CHECK (evaluation_score IS NULL OR evaluation_score BETWEEN 0 AND 100) NOT VALID;
                ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_score_range;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drill",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "safety_round_finding",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "safety_round",
                schema: "qams");
        }
    }
}
