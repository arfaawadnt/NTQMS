using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quality_indicator",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicator_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    numerator = table.Column<string>(type: "text", nullable: false),
                    denominator = table.Column<string>(type: "text", nullable: false),
                    inclusions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    exclusions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rate_factor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    warning_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    action_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_indicator", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "indicator_measurement",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<DateOnly>(type: "date", nullable: false),
                    numerator = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    denominator = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    indicator_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_indicator_measurement", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_indicator_measurement_quality_indicator_tenant_id_indicator",
                        columns: x => new { x.tenant_id, x.indicator_id },
                        principalSchema: "qams",
                        principalTable: "quality_indicator",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_indicator_measurement_tenant_id_indicator_id_period",
                schema: "qams",
                table: "indicator_measurement",
                columns: new[] { "tenant_id", "indicator_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_indicator_tenant_id_code",
                schema: "qams",
                table: "quality_indicator",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_indicator_tenant_id_status",
                schema: "qams",
                table: "quality_indicator",
                columns: new[] { "tenant_id", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy on both tables (EF does not generate it).
            foreach (var t in new[] { "quality_indicator", "indicator_measurement" })
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
                ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_frequency_domain
                  CHECK (frequency IN ('Weekly','Monthly','Quarterly','Annually')) NOT VALID;
                ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_frequency_domain;

                ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_direction_domain
                  CHECK (direction IN ('HigherIsBetter','LowerIsBetter')) NOT VALID;
                ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_direction_domain;

                ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_status_domain
                  CHECK (status IN ('Active','Retired')) NOT VALID;
                ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_status_domain;

                ALTER TABLE qams.indicator_measurement ADD CONSTRAINT ck_indicator_measurement_status_domain
                  CHECK (status IN ('InTarget','Warning','Breached')) NOT VALID;
                ALTER TABLE qams.indicator_measurement VALIDATE CONSTRAINT ck_indicator_measurement_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "indicator_measurement",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "quality_indicator",
                schema: "qams");
        }
    }
}
