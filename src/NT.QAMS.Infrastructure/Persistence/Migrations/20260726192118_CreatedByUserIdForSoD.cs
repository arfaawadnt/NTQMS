using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatedByUserIdForSoD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "work_task",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "validation_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "user_account",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "uncertainty_budget",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "training_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "test_catalog_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "test_authorization",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "saas",
                table: "tenant",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "supplier_evaluation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "supplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "sla_definition",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "sigma_assessment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "risk_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "reference_standard",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "reference_interval_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "quality_objective",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "qc_run",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "qc_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "pt_plan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "pt_enrollment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "precision_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "outlier_screening",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "notification_rule",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "notification_dispatch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "nonconformance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "monitoring_point",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "method_comparison_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "management_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "lov_entry",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "lot_comparison_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "linearity_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "interference_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "interested_party",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "instrument_comparability_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "file_reference",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "feedback_entry",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "escalation_timer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "equipment_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "detection_limit_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "department",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "controlled_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "context_issue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "conflict_declaration",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "complaint",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "competency_record",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "change_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "carryover_study",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "branch",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "audit_trail_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "audit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "qams",
                table: "archive_entry",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "work_task");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "validation_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "uncertainty_budget");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "training_assignment");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "test_catalog_item");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "test_authorization");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "saas",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "supplier_evaluation");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "sla_definition");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "risk_item");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "reference_standard");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "reference_interval_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "quality_objective");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "qc_run");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "qc_profile");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "pt_plan");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "pt_enrollment");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "outlier_screening");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "notification_rule");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "notification_dispatch");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "monitoring_point");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "method_comparison_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "lov_entry");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "lot_comparison_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "linearity_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "interference_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "interested_party");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "instrument_comparability_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "file_reference");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "feedback_entry");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "escalation_timer");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "equipment_item");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "detection_limit_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "department");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "context_issue");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "conflict_declaration");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "complaint");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "competency_record");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "carryover_study");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "branch");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "audit_trail_review");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "audit");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "qams",
                table: "archive_entry");
        }
    }
}
