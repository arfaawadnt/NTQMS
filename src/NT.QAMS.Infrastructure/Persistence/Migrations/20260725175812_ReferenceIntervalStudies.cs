using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceIntervalStudies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reference_interval_study",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    population = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    claimed_lower = table.Column<decimal>(type: "numeric", nullable: false),
                    claimed_upper = table.Column<decimal>(type: "numeric", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sample_count = table.Column<int>(type: "integer", nullable: true),
                    outside_count = table.Column<int>(type: "integer", nullable: true),
                    allowed_outside = table.Column<int>(type: "integer", nullable: true),
                    verdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    signed_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_off_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_interval_study", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reference_sample",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    study_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_sample", x => x.id);
                    table.ForeignKey(
                        name: "fk_reference_sample_reference_interval_study_study_id",
                        column: x => x.study_id,
                        principalSchema: "qams",
                        principalTable: "reference_interval_study",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reference_interval_study_tenant_id_state",
                schema: "qams",
                table: "reference_interval_study",
                columns: new[] { "tenant_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_reference_interval_study_tenant_id_study_ref",
                schema: "qams",
                table: "reference_interval_study",
                columns: new[] { "tenant_id", "study_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_sample_study_id",
                schema: "qams",
                table: "reference_sample",
                column: "study_id");
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.reference_interval_study ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.reference_interval_study
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reference_sample",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "reference_interval_study",
                schema: "qams");
        }
    }
}
