using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Complaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complaint",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    complainant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    complainant_contact = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    confidential = table.Column<bool>(type: "boolean", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    logged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    logged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validation_verdict = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    investigation_outcome = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    linked_nc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complaint", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_complaint_tenant_id_complaint_ref",
                schema: "qams",
                table: "complaint",
                columns: new[] { "tenant_id", "complaint_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_complaint_tenant_id_status",
                schema: "qams",
                table: "complaint",
                columns: new[] { "tenant_id", "status" });

            // Layer-2 isolation: RLS on the new tenant-scoped root.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.complaint ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON qams.complaint
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "complaint",
                schema: "qams");
        }
    }
}
