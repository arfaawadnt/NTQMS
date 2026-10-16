using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrecisionReproducibility : Migration
    {
        // Reproducibility: a manufacturer reproducibility claim, a site label per
        // replicate and the multi-site results. The design value domain widens
        // to the three multi-site layouts (derived from PrecisionDesign). On
        // Down, the original three-value domain is restored NOT VALID: a signed-off
        // multi-site study is frozen (frozen_immutability) and cannot be updated,
        // so existing rows are deliberately not re-validated against it.
        private const string AllDesigns =
            "'RunsOnly', 'DaysOnly', 'DaysAndRuns', 'SitesAndRuns', 'SitesAndDays', 'SitesDaysAndRuns'";
        private const string SingleSiteDesigns = "'RunsOnly', 'DaysOnly', 'DaysAndRuns'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                ALTER TABLE qams.precision_study DROP CONSTRAINT IF EXISTS ck_precision_study_design_domain;
                ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_design_domain
                    CHECK (design IS NULL OR design IN ({AllDesigns})) NOT VALID;
                ALTER TABLE qams.precision_study VALIDATE CONSTRAINT ck_precision_study_design_domain;
            ");

            migrationBuilder.AddColumn<decimal>(
                name: "between_site_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "between_site_sd",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "claimed_reproducibility_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "meets_reproducibility_claim",
                schema: "qams",
                table: "precision_study",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_cv_lower_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_cv_upper_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_df",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_sd",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reproducibility_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "within_lab_consistent_with_reproducibility_claim",
                schema: "qams",
                table: "precision_study",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "site_label",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                ALTER TABLE qams.precision_study DROP CONSTRAINT IF EXISTS ck_precision_study_design_domain;
                ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_design_domain
                    CHECK (design IS NULL OR design IN ({SingleSiteDesigns})) NOT VALID;
            ");

            migrationBuilder.DropColumn(
                name: "between_site_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "between_site_sd",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "claimed_reproducibility_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "meets_reproducibility_claim",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_cv_lower_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_cv_upper_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_df",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_sd",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "reproducibility_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "within_lab_consistent_with_reproducibility_claim",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "site_label",
                schema: "qams",
                table: "precision_measurement");
        }
    }
}
