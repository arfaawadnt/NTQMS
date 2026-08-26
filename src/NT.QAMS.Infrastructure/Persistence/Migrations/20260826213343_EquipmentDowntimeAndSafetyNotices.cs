using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentDowntimeAndSafetyNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_downtime",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_downtime", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_equipment_downtime_equipment_item_tenant_id_equipment_id",
                        columns: x => new { x.tenant_id, x.equipment_id },
                        principalSchema: "qams",
                        principalTable: "equipment_item",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment_safety_notice",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    required_action_by = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actioned_on = table.Column<DateOnly>(type: "date", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_safety_notice", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_equipment_safety_notice_equipment_item_tenant_id_equipment_",
                        columns: x => new { x.tenant_id, x.equipment_id },
                        principalSchema: "qams",
                        principalTable: "equipment_item",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_downtime_tenant_id_equipment_id",
                schema: "qams",
                table: "equipment_downtime",
                columns: new[] { "tenant_id", "equipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_safety_notice_tenant_id_equipment_id",
                schema: "qams",
                table: "equipment_safety_notice",
                columns: new[] { "tenant_id", "equipment_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on both owned-child tables.
            foreach (var t in new[] { "equipment_downtime", "equipment_safety_notice" })
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
                ALTER TABLE qams.equipment_downtime ADD CONSTRAINT ck_equipment_downtime_category_domain
                  CHECK (category IN ('Breakdown','AwaitingParts','ScheduledMaintenance','Other')) NOT VALID;
                ALTER TABLE qams.equipment_downtime VALIDATE CONSTRAINT ck_equipment_downtime_category_domain;

                ALTER TABLE qams.equipment_safety_notice ADD CONSTRAINT ck_equipment_safety_notice_type_domain
                  CHECK (type IN ('Recall','FieldSafetyNotice','HazardAlert')) NOT VALID;
                ALTER TABLE qams.equipment_safety_notice VALIDATE CONSTRAINT ck_equipment_safety_notice_type_domain;

                ALTER TABLE qams.equipment_safety_notice ADD CONSTRAINT ck_equipment_safety_notice_severity_domain
                  CHECK (severity IN ('Low','Medium','High')) NOT VALID;
                ALTER TABLE qams.equipment_safety_notice VALIDATE CONSTRAINT ck_equipment_safety_notice_severity_domain;

                ALTER TABLE qams.equipment_safety_notice ADD CONSTRAINT ck_equipment_safety_notice_status_domain
                  CHECK (status IN ('Open','Actioned','Closed')) NOT VALID;
                ALTER TABLE qams.equipment_safety_notice VALIDATE CONSTRAINT ck_equipment_safety_notice_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_downtime",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "equipment_safety_notice",
                schema: "qams");
        }
    }
}
