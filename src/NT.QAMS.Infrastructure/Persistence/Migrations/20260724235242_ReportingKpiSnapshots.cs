using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingKpiSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "read");

            migrationBuilder.CreateTable(
                name: "kpi_snapshot",
                schema: "read",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    open_ncs = table.Column<int>(type: "integer", nullable: false),
                    overdue_capa_actions = table.Column<int>(type: "integer", nullable: false),
                    open_complaints = table.Column<int>(type: "integer", nullable: false),
                    audits_in_progress = table.Column<int>(type: "integer", nullable: false),
                    equipment_out_of_service = table.Column<int>(type: "integer", nullable: false),
                    high_residual_risks = table.Column<int>(type: "integer", nullable: false),
                    overdue_tasks = table.Column<int>(type: "integer", nullable: false),
                    pt_unsatisfactory = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_snapshot", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kpi_snapshot_tenant_id_date",
                schema: "read",
                table: "kpi_snapshot",
                columns: new[] { "tenant_id", "date" },
                unique: true);

            // Layer-2 isolation: RLS on the read-model table as well.
            migrationBuilder.Sql(@"
                ALTER TABLE read.kpi_snapshot ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON read.kpi_snapshot
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_snapshot",
                schema: "read");
        }
    }
}
