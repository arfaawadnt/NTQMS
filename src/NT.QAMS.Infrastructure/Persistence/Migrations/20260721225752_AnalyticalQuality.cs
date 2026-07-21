using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticalQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pt_enrollment",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pt_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheme = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    analyte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cycle = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submitted_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    assigned_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    standard_deviation = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    z_score = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    performance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pt_enrollment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qc_profile",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analyte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    instrument = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    control_lot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_mean = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    target_sd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qc_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qc_run",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    z_score = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    outcome = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    violated_rules = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(150)", maxLength: 150, nullable: false),
                    measured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    troubleshooting_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qc_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "validation_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    protocol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_allowable_error = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mean_bias = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    cv = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_validation_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "validation_replicate",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    measured = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reference = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_validation_replicate", x => x.id);
                    table.ForeignKey(
                        name: "fk_validation_replicate_validation_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "validation_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pt_enrollment_tenant_id_pt_ref",
                schema: "qams",
                table: "pt_enrollment",
                columns: new[] { "tenant_id", "pt_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_qc_profile_tenant_id_analyte_instrument_control_lot",
                schema: "qams",
                table: "qc_profile",
                columns: new[] { "tenant_id", "analyte", "instrument", "control_lot" });

            migrationBuilder.CreateIndex(
                name: "ix_qc_run_tenant_id_profile_id_measured_at_utc",
                schema: "qams",
                table: "qc_run",
                columns: new[] { "tenant_id", "profile_id", "measured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_validation_replicate_study_id",
                schema: "qams",
                table: "validation_replicate",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_study_tenant_id_study_ref",
                schema: "qams",
                table: "validation_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);
        
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.qc_profile ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.qc_profile
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.qc_run ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.qc_run
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.validation_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.validation_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.pt_enrollment ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.pt_enrollment
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pt_enrollment",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "qc_profile",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "qc_run",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "validation_replicate",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "validation_study",
                schema: "qams");
        }
    }
}
