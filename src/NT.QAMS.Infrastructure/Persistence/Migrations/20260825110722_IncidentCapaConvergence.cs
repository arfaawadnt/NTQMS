using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncidentCapaConvergence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "corrective_action_nc_id",
                schema: "qams",
                table: "incident",
                type: "uuid",
                nullable: true);

            // Widen the nonconformance source-type domain to admit incidents as an origin
            // ("one loop, many sources"). Derived from the NcSourceType enum. The constraint
            // is raw SQL (not EF-modelled), so it is replaced in place here and restored in Down().
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
                ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain
                  CHECK (source_type IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest','Incident')) NOT VALID;
                ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the original source-type domain (without 'Incident'). Reversing this
            // fails if any nonconformance already uses the 'Incident' source; that is correct —
            // the rows would violate the narrowed constraint.
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
                ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain
                  CHECK (source_type IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest')) NOT VALID;
                ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
                """);

            migrationBuilder.DropColumn(
                name: "corrective_action_nc_id",
                schema: "qams",
                table: "incident");
        }
    }
}
