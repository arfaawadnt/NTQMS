using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMortalityReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complication_case",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    preventable = table.Column<bool>(type: "boolean", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complication_case", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "mortality_review",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    patient_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    death_date_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    primary_diagnosis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classification = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    first_reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    classification_findings = table.Column<string>(type: "text", nullable: true),
                    second_reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    second_review_notes = table.Column<string>(type: "text", nullable: true),
                    second_reviewer_concurs = table.Column<bool>(type: "boolean", nullable: true),
                    committee_learnings = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortality_review", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_complication_case_tenant_id_case_ref",
                schema: "qams",
                table: "complication_case",
                columns: new[] { "tenant_id", "case_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_complication_case_tenant_id_occurred_date_utc",
                schema: "qams",
                table: "complication_case",
                columns: new[] { "tenant_id", "occurred_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_complication_case_tenant_id_type_status",
                schema: "qams",
                table: "complication_case",
                columns: new[] { "tenant_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_mortality_review_tenant_id_death_date_utc",
                schema: "qams",
                table: "mortality_review",
                columns: new[] { "tenant_id", "death_date_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_mortality_review_tenant_id_review_ref",
                schema: "qams",
                table: "mortality_review",
                columns: new[] { "tenant_id", "review_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mortality_review_tenant_id_status",
                schema: "qams",
                table: "mortality_review",
                columns: new[] { "tenant_id", "status" });

            // Mandatory FORCE RLS + tenant_isolation policy on both tables.
            foreach (var t in new[] { "mortality_review", "complication_case" })
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

            // Value domains, derived from the C# enums (never guessed). Classification is nullable
            // until the death has been peer-reviewed.
            migrationBuilder.Sql("""
                ALTER TABLE qams.mortality_review ADD CONSTRAINT ck_mortality_review_classification_domain
                  CHECK (classification IS NULL OR classification IN ('Expected','Unexpected','PotentiallyPreventable','Preventable')) NOT VALID;
                ALTER TABLE qams.mortality_review VALIDATE CONSTRAINT ck_mortality_review_classification_domain;

                ALTER TABLE qams.mortality_review ADD CONSTRAINT ck_mortality_review_status_domain
                  CHECK (status IN ('Reported','Classified','SecondReviewed','CommitteeDiscussed','Closed')) NOT VALID;
                ALTER TABLE qams.mortality_review VALIDATE CONSTRAINT ck_mortality_review_status_domain;

                ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_type_domain
                  CHECK (type IN ('ReturnToTheatre','UnplannedIcuAdmission','UnplannedReadmission','HospitalAcquiredCondition','Other')) NOT VALID;
                ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_type_domain;

                ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_severity_domain
                  CHECK (severity IN ('Minor','Moderate','Severe','LifeThreatening')) NOT VALID;
                ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_severity_domain;

                ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
                ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "complication_case",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "mortality_review",
                schema: "qams");
        }
    }
}
