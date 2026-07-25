using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SigmaAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sigma_assessment",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allowable_total_error_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    bias_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    cv_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sigma_value = table.Column<decimal>(type: "numeric", nullable: false),
                    grade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sigma_assessment", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sigma_assessment_tenant_id_assessment_ref",
                schema: "qams",
                table: "sigma_assessment",
                columns: new[] { "tenant_id", "assessment_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sigma_assessment_tenant_id_state",
                schema: "qams",
                table: "sigma_assessment",
                columns: new[] { "tenant_id", "state" });
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.sigma_assessment ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.sigma_assessment
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sigma_assessment",
                schema: "qams");
        }
    }
}
