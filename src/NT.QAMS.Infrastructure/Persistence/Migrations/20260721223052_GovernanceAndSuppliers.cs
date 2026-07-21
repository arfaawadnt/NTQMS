using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceAndSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_request",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    impact_analysis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    implementation_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_request", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "management_review",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    review_date = table.Column<DateOnly>(type: "date", nullable: false),
                    participants = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minutes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_management_review", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "risk_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    likelihood = table.Column<int>(type: "integer", nullable: false),
                    impact = table.Column<int>(type: "integer", nullable: false),
                    rpn = table.Column<int>(type: "integer", nullable: false),
                    residual_likelihood = table.Column<int>(type: "integer", nullable: true),
                    residual_impact = table.Column<int>(type: "integer", nullable: true),
                    residual_rpn = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    registered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    suspension_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_evaluation",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    criteria_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    weighted_total = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    evaluated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_evaluation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "review_decision",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_decision", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_decision_management_review_review_id",
                        column: x => x.review_id,
                        principalSchema: "qams",
                        principalTable: "management_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mitigation_action",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    risk_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mitigation_action", x => x.id);
                    table.ForeignKey(
                        name: "fk_mitigation_action_risk_item_risk_id",
                        column: x => x.risk_id,
                        principalSchema: "qams",
                        principalTable: "risk_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_certificate",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_certificate", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_certificate_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "qams",
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_request_tenant_id_change_ref",
                schema: "qams",
                table: "change_request",
                columns: new[] { "tenant_id", "change_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_management_review_tenant_id_review_ref",
                schema: "qams",
                table: "management_review",
                columns: new[] { "tenant_id", "review_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mitigation_action_risk_id",
                schema: "qams",
                table: "mitigation_action",
                column: "risk_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_decision_review_id",
                schema: "qams",
                table: "review_decision",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_item_tenant_id_risk_ref",
                schema: "qams",
                table: "risk_item",
                columns: new[] { "tenant_id", "risk_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_item_tenant_id_status",
                schema: "qams",
                table: "risk_item",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_tenant_id_status",
                schema: "qams",
                table: "supplier",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_tenant_id_supplier_ref",
                schema: "qams",
                table: "supplier",
                columns: new[] { "tenant_id", "supplier_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certificate_supplier_id",
                schema: "qams",
                table: "supplier_certificate",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_evaluation_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_evaluation",
                columns: new[] { "tenant_id", "supplier_id" });
                    // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(""""""
                ALTER TABLE qams.risk_item ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.risk_item
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.change_request ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.change_request
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.management_review ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.management_review
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.supplier ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.supplier
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.supplier_evaluation ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.supplier_evaluation
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                """""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_request",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "mitigation_action",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "review_decision",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "supplier_certificate",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "supplier_evaluation",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "risk_item",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "management_review",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "supplier",
                schema: "qams");
        }
    }
}
