using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrecisionEp05Ep15Gaps : Migration
    {
        // EP05/EP15 gap closure. Beyond the EF-generated columns this adds what
        // EF cannot model: the value domain of the resolved design, the shape of
        // a study series, and the two cross-aggregate references to the source
        // precision study as tenant-composite FKs (M-08 idiom: NOT VALID ->
        // VALIDATE, ON DELETE RESTRICT). precision_study, sigma_assessment and
        // uncertainty_budget carry frozen_immutability triggers; ADD COLUMN with
        // a constant default is catalogue-only in PostgreSQL 11+, so no frozen
        // row is rewritten. app.bypass_rls per the standing rule (Trap 1).
        private static readonly (string Table, string Name, string Columns)[] SourceForeignKeys =
        [
            ("uncertainty_component", "fk_unc_component_precision_study_tenant", "tenant_id, source_precision_study_id"),
            ("sigma_assessment", "fk_sigma_assessment_precision_study_tenant", "tenant_id, source_precision_study_id"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL app.bypass_rls = 'on';");

            migrationBuilder.AddColumn<Guid>(
                name: "source_precision_study_id",
                schema: "qams",
                table: "uncertainty_component",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_precision_study_id",
                schema: "qams",
                table: "sigma_assessment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_precision_study_ref",
                schema: "qams",
                table: "sigma_assessment",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "between_day_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "between_day_sd",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "design",
                schema: "qams",
                table: "precision_study",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "repeatability_cv_lower_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "repeatability_cv_upper_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "repeatability_df",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "repeatability_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "series_level_count",
                schema: "qams",
                table: "precision_study",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "series_ref",
                schema: "qams",
                table: "precision_study",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "within_lab_cv_lower_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "within_lab_cv_upper_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "within_lab_df",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "within_lab_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calibrator_lot",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "day_label",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instrument",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "measured_on",
                schema: "qams",
                table: "precision_measurement",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operator",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reagent_lot",
                schema: "qams",
                table: "precision_measurement",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_unc_component_tenant_source_precision_study",
                schema: "qams",
                table: "uncertainty_component",
                columns: new[] { "tenant_id", "source_precision_study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sigma_assessment_tenant_source_precision_study",
                schema: "qams",
                table: "sigma_assessment",
                columns: new[] { "tenant_id", "source_precision_study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_precision_study_tenant_id_series_ref",
                schema: "qams",
                table: "precision_study",
                columns: new[] { "tenant_id", "series_ref" });

            migrationBuilder.Sql(@"
                ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_design_domain
                    CHECK (design IS NULL OR design IN ('RunsOnly', 'DaysOnly', 'DaysAndRuns')) NOT VALID;
                ALTER TABLE qams.precision_study VALIDATE CONSTRAINT ck_precision_study_design_domain;
                ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_series_shape
                    CHECK ((series_ref IS NULL AND series_level_count = 1)
                        OR (series_ref IS NOT NULL AND series_level_count BETWEEN 2 AND 10)) NOT VALID;
                ALTER TABLE qams.precision_study VALIDATE CONSTRAINT ck_precision_study_series_shape;
            ");

            foreach (var (table, name, columns) in SourceForeignKeys)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE qams.{table} ADD CONSTRAINT {name}
                        FOREIGN KEY ({columns}) REFERENCES qams.precision_study (tenant_id, id)
                        ON DELETE RESTRICT NOT VALID;
                    ALTER TABLE qams.{table} VALIDATE CONSTRAINT {name};
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL app.bypass_rls = 'on';");
            foreach (var (table, name, _) in SourceForeignKeys)
            {
                migrationBuilder.Sql($"ALTER TABLE qams.{table} DROP CONSTRAINT IF EXISTS {name};");
            }

            migrationBuilder.Sql(@"
                ALTER TABLE qams.precision_study DROP CONSTRAINT IF EXISTS ck_precision_study_series_shape;
                ALTER TABLE qams.precision_study DROP CONSTRAINT IF EXISTS ck_precision_study_design_domain;
            ");

            migrationBuilder.DropIndex(
                name: "ix_unc_component_tenant_source_precision_study",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropIndex(
                name: "ix_sigma_assessment_tenant_source_precision_study",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropIndex(
                name: "ix_precision_study_tenant_id_series_ref",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "source_precision_study_id",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropColumn(
                name: "source_precision_study_id",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropColumn(
                name: "source_precision_study_ref",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropColumn(
                name: "between_day_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "between_day_sd",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "design",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "repeatability_cv_lower_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "repeatability_cv_upper_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "repeatability_df",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "repeatability_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "series_level_count",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "series_ref",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "within_lab_cv_lower_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "within_lab_cv_upper_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "within_lab_df",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "within_lab_verification_limit_cv_pct",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "calibrator_lot",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "day_label",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "instrument",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "measured_on",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "operator",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "reagent_lot",
                schema: "qams",
                table: "precision_measurement");
        }
    }
}
