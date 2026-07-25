using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinearityStudies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "linearity_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    method = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    allowable_deviation_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slope = table.Column<decimal>(type: "numeric", nullable: true),
                    intercept = table.Column<decimal>(type: "numeric", nullable: true),
                    correlation_r = table.Column<decimal>(type: "numeric", nullable: true),
                    is_linear = table.Column<bool>(type: "boolean", nullable: true),
                    amr_low = table.Column<decimal>(type: "numeric", nullable: true),
                    amr_high = table.Column<decimal>(type: "numeric", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linearity_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linearity_measurement",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_value = table.Column<decimal>(type: "numeric", nullable: false),
                    measured_value = table.Column<decimal>(type: "numeric", nullable: false),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linearity_measurement", x => x.id);
                    table.ForeignKey(
                        name: "fk_linearity_measurement_linearity_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "linearity_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_linearity_measurement_study_id",
                schema: "qams",
                table: "linearity_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_linearity_study_tenant_id_state",
                schema: "qams",
                table: "linearity_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_linearity_study_tenant_id_study_ref",
                schema: "qams",
                table: "linearity_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.linearity_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.linearity_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "linearity_measurement",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "linearity_study",
                schema: "qams");
        }
    }
}
