using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MetrologicalTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intermediate_check",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    performed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    reference_standard_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intermediate_check", x => x.id);
                    table.ForeignKey(
                        name: "fk_intermediate_check_equipment_item_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "qams",
                        principalTable: "equipment_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reference_standard",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    standard_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    certificate_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    traceable_to = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certified_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    uncertainty_statement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quarantine_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_standard", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intermediate_check_equipment_id",
                schema: "qams",
                table: "intermediate_check",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_reference_standard_tenant_id_standard_ref",
                schema: "qams",
                table: "reference_standard",
                columns: new[] { "tenant_id", "standard_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reference_standard_tenant_id_status",
                schema: "qams",
                table: "reference_standard",
                columns: new[] { "tenant_id", "status" });

            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.reference_standard ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.reference_standard
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intermediate_check",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "reference_standard",
                schema: "qams");
        }
    }
}
