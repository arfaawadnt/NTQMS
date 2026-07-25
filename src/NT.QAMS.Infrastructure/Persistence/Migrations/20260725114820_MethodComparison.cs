using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MethodComparison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "method_comparison_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    test_method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pair_count = table.Column<int>(type: "integer", nullable: true),
                    pearson_r = table.Column<decimal>(type: "numeric", nullable: true),
                    deming_slope = table.Column<decimal>(type: "numeric", nullable: true),
                    deming_intercept = table.Column<decimal>(type: "numeric", nullable: true),
                    passing_bablok_slope = table.Column<decimal>(type: "numeric", nullable: true),
                    passing_bablok_intercept = table.Column<decimal>(type: "numeric", nullable: true),
                    mean_bias = table.Column<decimal>(type: "numeric", nullable: true),
                    bias_sd = table.Column<decimal>(type: "numeric", nullable: true),
                    limit_of_agreement_lower = table.Column<decimal>(type: "numeric", nullable: true),
                    limit_of_agreement_upper = table.Column<decimal>(type: "numeric", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_method_comparison_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "measurement_pair",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_value = table.Column<decimal>(type: "numeric", nullable: false),
                    test_value = table.Column<decimal>(type: "numeric", nullable: false),
                    sample_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurement_pair", x => x.id);
                    table.ForeignKey(
                        name: "fk_measurement_pair_method_comparison_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "method_comparison_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_pair_study_id",
                schema: "qams",
                table: "measurement_pair",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_method_comparison_study_tenant_id_state",
                schema: "qams",
                table: "method_comparison_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_method_comparison_study_tenant_id_study_ref",
                schema: "qams",
                table: "method_comparison_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.method_comparison_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.method_comparison_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "measurement_pair",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "method_comparison_study",
                schema: "qams");
        }
    }
}
