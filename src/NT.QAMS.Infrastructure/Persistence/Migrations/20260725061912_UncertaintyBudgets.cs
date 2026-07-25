using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UncertaintyBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uncertainty_budget",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    analyte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    method = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coverage_factor = table.Column<decimal>(type: "numeric", nullable: false),
                    target_expanded_uncertainty = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    combined_standard_uncertainty = table.Column<decimal>(type: "numeric", nullable: true),
                    expanded_uncertainty = table.Column<decimal>(type: "numeric", nullable: true),
                    meets_target = table.Column<bool>(type: "boolean", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uncertainty_budget", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uncertainty_component",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    relative_standard_uncertainty = table.Column<decimal>(type: "numeric", nullable: false),
                    source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    budget_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_uncertainty_component", x => x.id);
                    table.ForeignKey(
                        name: "fk_uncertainty_component_uncertainty_budget_budget_id",
                        column: x => x.budget_id,
                        principalSchema: "qams",
                        principalTable: "uncertainty_budget",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_uncertainty_budget_tenant_id_budget_ref",
                schema: "qams",
                table: "uncertainty_budget",
                columns: new[] { "tenant_id", "budget_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_uncertainty_budget_tenant_id_status",
                schema: "qams",
                table: "uncertainty_budget",
                columns: new[] { "tenant_id", "status" });

            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.uncertainty_budget ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.uncertainty_budget
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");

            migrationBuilder.CreateIndex(
                name: "ix_uncertainty_component_budget_id",
                schema: "qams",
                table: "uncertainty_component",
                column: "budget_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uncertainty_component",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "uncertainty_budget",
                schema: "qams");
        }
    }
}
