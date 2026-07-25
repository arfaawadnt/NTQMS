using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnvironmentalMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitoring_point",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    point_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    parameter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    low_limit = table.Column<decimal>(type: "numeric", nullable: true),
                    high_limit = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_point", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "environmental_reading",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_limit = table.Column<bool>(type: "boolean", nullable: false),
                    remark = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    point_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_environmental_reading", x => x.id);
                    table.ForeignKey(
                        name: "fk_environmental_reading_monitoring_point_point_id",
                        column: x => x.point_id,
                        principalSchema: "qams",
                        principalTable: "monitoring_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_environmental_reading_point_id_recorded_at_utc",
                schema: "qams",
                table: "environmental_reading",
                columns: new[] { "point_id", "recorded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_point_tenant_id_point_ref",
                schema: "qams",
                table: "monitoring_point",
                columns: new[] { "tenant_id", "point_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_point_tenant_id_status",
                schema: "qams",
                table: "monitoring_point",
                columns: new[] { "tenant_id", "status" });
            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.monitoring_point ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.monitoring_point
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "environmental_reading",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "monitoring_point",
                schema: "qams");
        }
    }
}
