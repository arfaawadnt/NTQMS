using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImpartialityAndOrgContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conflict_declaration",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conflict_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    declarant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    related_party = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    declared_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    risk_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    mitigation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    closure_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_declaration", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "context_issue",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    impact = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    linked_risk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_context_issue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interested_party",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    needs_and_expectations = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    relevant_requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    reviewed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interested_party", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_declaration_tenant_id_conflict_ref",
                schema: "qams",
                table: "conflict_declaration",
                columns: new[] { "tenant_id", "conflict_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conflict_declaration_tenant_id_status",
                schema: "qams",
                table: "conflict_declaration",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_context_issue_tenant_id_issue_ref",
                schema: "qams",
                table: "context_issue",
                columns: new[] { "tenant_id", "issue_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_interested_party_tenant_id_party_ref",
                schema: "qams",
                table: "interested_party",
                columns: new[] { "tenant_id", "party_ref" },
                unique: true);
            // Layer-2 isolation: RLS on the new tenant-scoped roots.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.conflict_declaration ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.conflict_declaration
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.interested_party ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.interested_party
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                ALTER TABLE qams.context_issue ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.context_issue
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conflict_declaration",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "context_issue",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "interested_party",
                schema: "qams");
        }
    }
}
