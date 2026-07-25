using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrecisionStudies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "precision_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    claimed_repeatability_cv_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    claimed_within_lab_cv_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    grand_mean = table.Column<decimal>(type: "numeric", nullable: true),
                    repeatability_sd = table.Column<decimal>(type: "numeric", nullable: true),
                    repeatability_cv_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    between_run_sd = table.Column<decimal>(type: "numeric", nullable: true),
                    between_run_cv_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    within_lab_sd = table.Column<decimal>(type: "numeric", nullable: true),
                    within_lab_cv_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    meets_repeatability_claim = table.Column<bool>(type: "boolean", nullable: true),
                    meets_within_lab_claim = table.Column<bool>(type: "boolean", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precision_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "precision_measurement",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precision_measurement", x => x.id);
                    table.ForeignKey(
                        name: "fk_precision_measurement_precision_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "precision_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_precision_measurement_study_id",
                schema: "qams",
                table: "precision_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_precision_study_tenant_id_state",
                schema: "qams",
                table: "precision_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_precision_study_tenant_id_study_ref",
                schema: "qams",
                table: "precision_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.precision_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.precision_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "precision_measurement",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "precision_study",
                schema: "qams");
        }
    }
}
