using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticalComplianceModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carryover_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allowable_carryover_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mean_high = table.Column<decimal>(type: "numeric", nullable: true),
                    first_low = table.Column<decimal>(type: "numeric", nullable: true),
                    steady_low = table.Column<decimal>(type: "numeric", nullable: true),
                    carryover_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    passes = table.Column<bool>(type: "boolean", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carryover_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instrument_comparability_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_instrument = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allowable_bias_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instrument_count = table.Column<int>(type: "integer", nullable: true),
                    non_comparable_count = table.Column<int>(type: "integer", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_comparability_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interference_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allowable_bias_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    control_mean = table.Column<decimal>(type: "numeric", nullable: true),
                    interferent_count = table.Column<int>(type: "integer", nullable: true),
                    significant_count = table.Column<int>(type: "integer", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interference_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lot_comparison_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_lot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    new_lot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    allowable_bias_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pair_count = table.Column<int>(type: "integer", nullable: true),
                    mean_current = table.Column<decimal>(type: "numeric", nullable: true),
                    mean_new = table.Column<decimal>(type: "numeric", nullable: true),
                    mean_bias_pct = table.Column<decimal>(type: "numeric", nullable: true),
                    passes = table.Column<bool>(type: "boolean", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot_comparison_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outlier_screening",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    screening_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dataset = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    point_count = table.Column<int>(type: "integer", nullable: true),
                    mean = table.Column<decimal>(type: "numeric", nullable: true),
                    sd = table.Column<decimal>(type: "numeric", nullable: true),
                    median = table.Column<decimal>(type: "numeric", nullable: true),
                    q1 = table.Column<decimal>(type: "numeric", nullable: true),
                    q3 = table.Column<decimal>(type: "numeric", nullable: true),
                    tukey_lower = table.Column<decimal>(type: "numeric", nullable: true),
                    tukey_upper = table.Column<decimal>(type: "numeric", nullable: true),
                    outlier_count = table.Column<int>(type: "integer", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outlier_screening", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carryover_reading",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carryover_reading", x => x.id);
                    table.ForeignKey(
                        name: "fk_carryover_reading_carryover_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "carryover_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instrument_reading",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sample_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_reading", x => x.id);
                    table.ForeignKey(
                        name: "fk_instrument_reading_instrument_comparability_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "instrument_comparability_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interference_measurement",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_control = table.Column<bool>(type: "boolean", nullable: false),
                    interferent = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interference_measurement", x => x.id);
                    table.ForeignKey(
                        name: "fk_interference_measurement_interference_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "interference_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lot_sample_pair",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_lot_value = table.Column<decimal>(type: "numeric", nullable: false),
                    new_lot_value = table.Column<decimal>(type: "numeric", nullable: false),
                    sample_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot_sample_pair", x => x.id);
                    table.ForeignKey(
                        name: "fk_lot_sample_pair_lot_comparison_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "lot_comparison_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outlier_point",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    screening_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outlier_point", x => x.id);
                    table.ForeignKey(
                        name: "fk_outlier_point_outlier_screening_screening_id",
                        column: x => x.screening_id,
                        principalSchema: "qams",
                        principalTable: "outlier_screening",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_carryover_reading_study_id",
                schema: "qams",
                table: "carryover_reading",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_carryover_study_tenant_id_state",
                schema: "qams",
                table: "carryover_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_carryover_study_tenant_id_study_ref",
                schema: "qams",
                table: "carryover_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_comparability_study_tenant_id_state",
                schema: "qams",
                table: "instrument_comparability_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_comparability_study_tenant_id_study_ref",
                schema: "qams",
                table: "instrument_comparability_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_reading_study_id",
                schema: "qams",
                table: "instrument_reading",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_interference_measurement_study_id",
                schema: "qams",
                table: "interference_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_interference_study_tenant_id_state",
                schema: "qams",
                table: "interference_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_interference_study_tenant_id_study_ref",
                schema: "qams",
                table: "interference_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lot_comparison_study_tenant_id_state",
                schema: "qams",
                table: "lot_comparison_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_lot_comparison_study_tenant_id_study_ref",
                schema: "qams",
                table: "lot_comparison_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lot_sample_pair_study_id",
                schema: "qams",
                table: "lot_sample_pair",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_outlier_point_screening_id",
                schema: "qams",
                table: "outlier_point",
                column: "screening_id");

            migrationBuilder.CreateIndex(
                name: "ix_outlier_screening_tenant_id_screening_ref",
                schema: "qams",
                table: "outlier_screening",
                columns: new[] { "tenant_id", "screening_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outlier_screening_tenant_id_state",
                schema: "qams",
                table: "outlier_screening",
                columns: new[] { "tenant_id", "state" });

            // Layer-2 isolation: RLS on each new tenant-scoped root table.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.outlier_screening ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.outlier_screening
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                ALTER TABLE qams.carryover_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.carryover_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                ALTER TABLE qams.lot_comparison_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.lot_comparison_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                ALTER TABLE qams.interference_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.interference_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                ALTER TABLE qams.instrument_comparability_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.instrument_comparability_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carryover_reading",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "instrument_reading",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "interference_measurement",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "lot_sample_pair",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "outlier_point",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "carryover_study",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "instrument_comparability_study",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "interference_study",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "lot_comparison_study",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "outlier_screening",
                schema: "qams");
        }
    }
}
