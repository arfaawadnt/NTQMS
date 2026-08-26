using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierContractsAndCars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_outsourced_clinical_service",
                schema: "qams",
                table: "supplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "service_scope",
                schema: "qams",
                table: "supplier",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_car",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    raised_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    response_on = table.Column<DateOnly>(type: "date", nullable: true),
                    effective = table.Column<bool>(type: "boolean", nullable: true),
                    closure_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_car", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_supplier_car_supplier_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "qams",
                        principalTable: "supplier",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contract",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sla_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    termination_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_contract", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_supplier_contract_supplier_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "qams",
                        principalTable: "supplier",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_car_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_car",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_contract_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_contract",
                columns: new[] { "tenant_id", "supplier_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on both owned-child tables.
            foreach (var t in new[] { "supplier_contract", "supplier_car" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE qams.{t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.{t} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.{t};
                    CREATE POLICY tenant_isolation ON qams.{t}
                      FOR ALL
                      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on')
                      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on');
                    """);
            }

            // Value domains, derived from the C# enums (never guessed).
            migrationBuilder.Sql("""
                ALTER TABLE qams.supplier_contract ADD CONSTRAINT ck_supplier_contract_status_domain
                  CHECK (status IN ('Active','Terminated')) NOT VALID;
                ALTER TABLE qams.supplier_contract VALIDATE CONSTRAINT ck_supplier_contract_status_domain;

                ALTER TABLE qams.supplier_car ADD CONSTRAINT ck_supplier_car_status_domain
                  CHECK (status IN ('Open','ResponseReceived','Closed')) NOT VALID;
                ALTER TABLE qams.supplier_car VALIDATE CONSTRAINT ck_supplier_car_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_car",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "supplier_contract",
                schema: "qams");

            migrationBuilder.DropColumn(
                name: "is_outsourced_clinical_service",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "service_scope",
                schema: "qams",
                table: "supplier");
        }
    }
}
