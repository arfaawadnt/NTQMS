using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentOfCareNcSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M-22: widen the nonconformance source-type domain to admit
            // environment-of-care safety-round findings as an origin, so a round
            // finding can be handed off into the single corrective-action pipeline
            // ("one loop, many sources"). Derived from the NcSourceType enum. The
            // constraint is raw SQL (not EF-modelled), so it is replaced in place
            // here and restored in Down().
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
                ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain
                  CHECK (source_type IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest','Incident','EnvironmentOfCare')) NOT VALID;
                ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the source-type domain without 'EnvironmentOfCare'. Reversing
            // this fails if any nonconformance already uses that source; that is
            // correct — the rows would violate the narrowed constraint.
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
                ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain
                  CHECK (source_type IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest','Incident')) NOT VALID;
                ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
                """);
        }
    }
}
