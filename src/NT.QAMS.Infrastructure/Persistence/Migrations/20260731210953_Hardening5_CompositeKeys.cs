using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening5_CompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessment_result_competency_record_tenant",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_checklist_item_audit_tenant",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_finding_audit_tenant",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropForeignKey(
                name: "fk_calibration_record_equipment_item_tenant",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropForeignKey(
                name: "fk_capa_action_nonconformance_tenant",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropForeignKey(
                name: "fk_carryover_reading_carryover_study_tenant",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_department_branch_branch_id",
                schema: "qams",
                table: "department");

            migrationBuilder.DropForeignKey(
                name: "fk_detection_measurement_detection_limit_study_tenant",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_document_version_controlled_document_tenant",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropForeignKey(
                name: "fk_environmental_reading_monitoring_point_tenant",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_instrument_reading_instrument_comparability_study_tenant",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_interference_measurement_interference_study_tenant",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_intermediate_check_equipment_item_tenant",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropForeignKey(
                name: "fk_linearity_measurement_linearity_study_tenant",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_lot_sample_pair_lot_comparison_study_tenant",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropForeignKey(
                name: "fk_maintenance_record_equipment_item_tenant",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropForeignKey(
                name: "fk_measurement_pair_method_comparison_study_tenant",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropForeignKey(
                name: "fk_mitigation_action_risk_item_tenant",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropForeignKey(
                name: "fk_objective_progress_quality_objective_tenant",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropForeignKey(
                name: "fk_outlier_point_outlier_screening_tenant",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropForeignKey(
                name: "fk_precision_measurement_precision_study_tenant",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_pt_plan_item_pt_plan_tenant",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropForeignKey(
                name: "fk_rca_record_nonconformance_tenant",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropForeignKey(
                name: "fk_reference_sample_reference_interval_study_tenant",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropForeignKey(
                name: "fk_review_decision_management_review_tenant",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permission_role_tenant",
                schema: "qams",
                table: "role_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_supplier_certificate_supplier_tenant",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropForeignKey(
                name: "fk_uncertainty_component_uncertainty_budget_tenant",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropForeignKey(
                name: "fk_validation_replicate_validation_study_tenant",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_work_task",
                schema: "qams",
                table: "work_task");

            migrationBuilder.DropPrimaryKey(
                name: "pk_validation_study",
                schema: "qams",
                table: "validation_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_validation_replicate",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropIndex(
                name: "ix_validation_replicate_study_id",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_department_access",
                schema: "qams",
                table: "user_department_access");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_branch_access",
                schema: "qams",
                table: "user_branch_access");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_access_review",
                schema: "qams",
                table: "user_access_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_uncertainty_component",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropIndex(
                name: "ix_uncertainty_component_budget_id",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropPrimaryKey(
                name: "pk_uncertainty_budget",
                schema: "qams",
                table: "uncertainty_budget");

            migrationBuilder.DropPrimaryKey(
                name: "pk_training_assignment",
                schema: "qams",
                table: "training_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_test_catalog_item",
                schema: "qams",
                table: "test_catalog_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_test_authorization",
                schema: "qams",
                table: "test_authorization");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier_evaluation",
                schema: "qams",
                table: "supplier_evaluation");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier_certificate",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropIndex(
                name: "ix_supplier_certificate_supplier_id",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sla_definition",
                schema: "qams",
                table: "sla_definition");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sigma_assessment",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permission",
                schema: "qams",
                table: "role_permission");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role",
                schema: "qams",
                table: "role");

            migrationBuilder.DropPrimaryKey(
                name: "pk_risk_item",
                schema: "qams",
                table: "risk_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_review_decision",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropIndex(
                name: "ix_review_decision_review_id",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_standard",
                schema: "qams",
                table: "reference_standard");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_sample",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropIndex(
                name: "ix_reference_sample_study_id",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_interval_study",
                schema: "qams",
                table: "reference_interval_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_rca_record",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropIndex(
                name: "ix_rca_record_nc_id",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_quality_policy",
                schema: "qams",
                table: "quality_policy");

            migrationBuilder.DropPrimaryKey(
                name: "pk_quality_objective",
                schema: "qams",
                table: "quality_objective");

            migrationBuilder.DropPrimaryKey(
                name: "pk_qc_run",
                schema: "qams",
                table: "qc_run");

            migrationBuilder.DropPrimaryKey(
                name: "pk_qc_profile",
                schema: "qams",
                table: "qc_profile");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_plan_item",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropIndex(
                name: "ix_pt_plan_item_plan_id",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_plan",
                schema: "qams",
                table: "pt_plan");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_enrollment",
                schema: "qams",
                table: "pt_enrollment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_precision_study",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_precision_measurement",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropIndex(
                name: "ix_precision_measurement_study_id",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outlier_screening",
                schema: "qams",
                table: "outlier_screening");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outlier_point",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropIndex(
                name: "ix_outlier_point_screening_id",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropPrimaryKey(
                name: "pk_objective_progress",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropIndex(
                name: "ix_objective_progress_objective_id",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notification_rule",
                schema: "qams",
                table: "notification_rule");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notification_dispatch",
                schema: "qams",
                table: "notification_dispatch");

            migrationBuilder.DropPrimaryKey(
                name: "pk_nonconformance",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropPrimaryKey(
                name: "pk_monitoring_point",
                schema: "qams",
                table: "monitoring_point");

            migrationBuilder.DropPrimaryKey(
                name: "pk_mitigation_action",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropIndex(
                name: "ix_mitigation_action_risk_id",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropPrimaryKey(
                name: "pk_method_comparison_study",
                schema: "qams",
                table: "method_comparison_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_measurement_pair",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropIndex(
                name: "ix_measurement_pair_study_id",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropPrimaryKey(
                name: "pk_management_review",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_maintenance_record",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_record_equipment_id",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lov_entry",
                schema: "qams",
                table: "lov_entry");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lot_sample_pair",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropIndex(
                name: "ix_lot_sample_pair_study_id",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lot_comparison_study",
                schema: "qams",
                table: "lot_comparison_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_linearity_study",
                schema: "qams",
                table: "linearity_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_linearity_measurement",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropIndex(
                name: "ix_linearity_measurement_study_id",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_kpi_snapshot",
                schema: "read",
                table: "kpi_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "pk_intermediate_check",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropIndex(
                name: "ix_intermediate_check_equipment_id",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interference_study",
                schema: "qams",
                table: "interference_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interference_measurement",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropIndex(
                name: "ix_interference_measurement_study_id",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interested_party",
                schema: "qams",
                table: "interested_party");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instrument_reading",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropIndex(
                name: "ix_instrument_reading_study_id",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instrument_comparability_study",
                schema: "qams",
                table: "instrument_comparability_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_file_reference",
                schema: "qams",
                table: "file_reference");

            migrationBuilder.DropPrimaryKey(
                name: "pk_feedback_entry",
                schema: "qams",
                table: "feedback_entry");

            migrationBuilder.DropPrimaryKey(
                name: "pk_escalation_timer",
                schema: "qams",
                table: "escalation_timer");

            migrationBuilder.DropPrimaryKey(
                name: "pk_equipment_item",
                schema: "qams",
                table: "equipment_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_environmental_reading",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_electronic_signature",
                schema: "audit",
                table: "electronic_signature");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_version",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropIndex(
                name: "ix_document_version_document_id",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_controlled_copy",
                schema: "qams",
                table: "document_controlled_copy");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_acknowledgement",
                schema: "qams",
                table: "document_acknowledgement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_detection_measurement",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropIndex(
                name: "ix_detection_measurement_study_id",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_detection_limit_study",
                schema: "qams",
                table: "detection_limit_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_department",
                schema: "qams",
                table: "department");

            migrationBuilder.DropIndex(
                name: "ix_department_branch_id",
                schema: "qams",
                table: "department");

            migrationBuilder.DropPrimaryKey(
                name: "pk_controlled_document",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropPrimaryKey(
                name: "pk_context_issue",
                schema: "qams",
                table: "context_issue");

            migrationBuilder.DropPrimaryKey(
                name: "pk_conflict_declaration",
                schema: "qams",
                table: "conflict_declaration");

            migrationBuilder.DropPrimaryKey(
                name: "pk_complaint",
                schema: "qams",
                table: "complaint");

            migrationBuilder.DropPrimaryKey(
                name: "pk_competency_record",
                schema: "qams",
                table: "competency_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_change_request",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropPrimaryKey(
                name: "pk_carryover_study",
                schema: "qams",
                table: "carryover_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_carryover_reading",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropIndex(
                name: "ix_carryover_reading_study_id",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_capa_action",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropIndex(
                name: "ix_capa_action_nc_id",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropPrimaryKey(
                name: "pk_calibration_record",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropIndex(
                name: "ix_calibration_record_equipment_id",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_branch",
                schema: "qams",
                table: "branch");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_trail_review",
                schema: "qams",
                table: "audit_trail_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_trail",
                schema: "audit",
                table: "audit_trail");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_finding",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropIndex(
                name: "ix_audit_finding_audit_id",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_checklist_item",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropIndex(
                name: "ix_audit_checklist_item_audit_id",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit",
                schema: "qams",
                table: "audit");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assessment_result",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropIndex(
                name: "ix_assessment_result_competency_id",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropPrimaryKey(
                name: "pk_archive_entry",
                schema: "qams",
                table: "archive_entry");

            migrationBuilder.AddPrimaryKey(
                name: "pk_work_task",
                schema: "qams",
                table: "work_task",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_validation_study",
                schema: "qams",
                table: "validation_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_validation_replicate",
                schema: "qams",
                table: "validation_replicate",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_department_access",
                schema: "qams",
                table: "user_department_access",
                columns: new[] { "tenant_id", "user_id", "department_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_branch_access",
                schema: "qams",
                table: "user_branch_access",
                columns: new[] { "tenant_id", "user_id", "branch_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_access_review",
                schema: "qams",
                table: "user_access_review",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_uncertainty_component",
                schema: "qams",
                table: "uncertainty_component",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_uncertainty_budget",
                schema: "qams",
                table: "uncertainty_budget",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_training_assignment",
                schema: "qams",
                table: "training_assignment",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_test_catalog_item",
                schema: "qams",
                table: "test_catalog_item",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_test_authorization",
                schema: "qams",
                table: "test_authorization",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier_evaluation",
                schema: "qams",
                table: "supplier_evaluation",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier_certificate",
                schema: "qams",
                table: "supplier_certificate",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier",
                schema: "qams",
                table: "supplier",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_sla_definition",
                schema: "qams",
                table: "sla_definition",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_sigma_assessment",
                schema: "qams",
                table: "sigma_assessment",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permission",
                schema: "qams",
                table: "role_permission",
                columns: new[] { "tenant_id", "role_id", "permission_key" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_role",
                schema: "qams",
                table: "role",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_risk_item",
                schema: "qams",
                table: "risk_item",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_review_decision",
                schema: "qams",
                table: "review_decision",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_standard",
                schema: "qams",
                table: "reference_standard",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_sample",
                schema: "qams",
                table: "reference_sample",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_interval_study",
                schema: "qams",
                table: "reference_interval_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_rca_record",
                schema: "qams",
                table: "rca_record",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_quality_policy",
                schema: "qams",
                table: "quality_policy",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_quality_objective",
                schema: "qams",
                table: "quality_objective",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_qc_run",
                schema: "qams",
                table: "qc_run",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_qc_profile",
                schema: "qams",
                table: "qc_profile",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_plan_item",
                schema: "qams",
                table: "pt_plan_item",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_plan",
                schema: "qams",
                table: "pt_plan",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_enrollment",
                schema: "qams",
                table: "pt_enrollment",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_precision_study",
                schema: "qams",
                table: "precision_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_precision_measurement",
                schema: "qams",
                table: "precision_measurement",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_outlier_screening",
                schema: "qams",
                table: "outlier_screening",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_outlier_point",
                schema: "qams",
                table: "outlier_point",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_objective_progress",
                schema: "qams",
                table: "objective_progress",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_notification_rule",
                schema: "qams",
                table: "notification_rule",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_notification_dispatch",
                schema: "qams",
                table: "notification_dispatch",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_nonconformance",
                schema: "qams",
                table: "nonconformance",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_monitoring_point",
                schema: "qams",
                table: "monitoring_point",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_mitigation_action",
                schema: "qams",
                table: "mitigation_action",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_method_comparison_study",
                schema: "qams",
                table: "method_comparison_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_measurement_pair",
                schema: "qams",
                table: "measurement_pair",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_management_review",
                schema: "qams",
                table: "management_review",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_maintenance_record",
                schema: "qams",
                table: "maintenance_record",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_lov_entry",
                schema: "qams",
                table: "lov_entry",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_lot_sample_pair",
                schema: "qams",
                table: "lot_sample_pair",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_lot_comparison_study",
                schema: "qams",
                table: "lot_comparison_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_linearity_study",
                schema: "qams",
                table: "linearity_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_linearity_measurement",
                schema: "qams",
                table: "linearity_measurement",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_kpi_snapshot",
                schema: "read",
                table: "kpi_snapshot",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_intermediate_check",
                schema: "qams",
                table: "intermediate_check",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_interference_study",
                schema: "qams",
                table: "interference_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_interference_measurement",
                schema: "qams",
                table: "interference_measurement",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_interested_party",
                schema: "qams",
                table: "interested_party",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_instrument_reading",
                schema: "qams",
                table: "instrument_reading",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_instrument_comparability_study",
                schema: "qams",
                table: "instrument_comparability_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_file_reference",
                schema: "qams",
                table: "file_reference",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_feedback_entry",
                schema: "qams",
                table: "feedback_entry",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_escalation_timer",
                schema: "qams",
                table: "escalation_timer",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_equipment_item",
                schema: "qams",
                table: "equipment_item",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_environmental_reading",
                schema: "qams",
                table: "environmental_reading",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_electronic_signature",
                schema: "audit",
                table: "electronic_signature",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_version",
                schema: "qams",
                table: "document_version",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_controlled_copy",
                schema: "qams",
                table: "document_controlled_copy",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_acknowledgement",
                schema: "qams",
                table: "document_acknowledgement",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_detection_measurement",
                schema: "qams",
                table: "detection_measurement",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_detection_limit_study",
                schema: "qams",
                table: "detection_limit_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_department",
                schema: "qams",
                table: "department",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_controlled_document",
                schema: "qams",
                table: "controlled_document",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_context_issue",
                schema: "qams",
                table: "context_issue",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_conflict_declaration",
                schema: "qams",
                table: "conflict_declaration",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_complaint",
                schema: "qams",
                table: "complaint",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_competency_record",
                schema: "qams",
                table: "competency_record",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_change_request",
                schema: "qams",
                table: "change_request",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_carryover_study",
                schema: "qams",
                table: "carryover_study",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_carryover_reading",
                schema: "qams",
                table: "carryover_reading",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_capa_action",
                schema: "qams",
                table: "capa_action",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_calibration_record",
                schema: "qams",
                table: "calibration_record",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_branch",
                schema: "qams",
                table: "branch",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_trail_review",
                schema: "qams",
                table: "audit_trail_review",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_trail",
                schema: "audit",
                table: "audit_trail",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_finding",
                schema: "qams",
                table: "audit_finding",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_checklist_item",
                schema: "qams",
                table: "audit_checklist_item",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit",
                schema: "qams",
                table: "audit",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_assessment_result",
                schema: "qams",
                table: "assessment_result",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_archive_entry",
                schema: "qams",
                table: "archive_entry",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_validation_replicate_tenant_id_study_id",
                schema: "qams",
                table: "validation_replicate",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_department_access_user_id",
                schema: "qams",
                table: "user_department_access",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_branch_access_user_id",
                schema: "qams",
                table: "user_branch_access",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_uncertainty_component_tenant_id_budget_id",
                schema: "qams",
                table: "uncertainty_component",
                columns: new[] { "tenant_id", "budget_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certificate_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_certificate",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_review_decision_tenant_id_review_id",
                schema: "qams",
                table: "review_decision",
                columns: new[] { "tenant_id", "review_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reference_sample_tenant_id_study_id",
                schema: "qams",
                table: "reference_sample",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rca_record_tenant_id_nc_id",
                schema: "qams",
                table: "rca_record",
                columns: new[] { "tenant_id", "nc_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pt_plan_item_tenant_id_plan_id",
                schema: "qams",
                table: "pt_plan_item",
                columns: new[] { "tenant_id", "plan_id" });

            migrationBuilder.CreateIndex(
                name: "ix_precision_measurement_tenant_id_study_id",
                schema: "qams",
                table: "precision_measurement",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outlier_point_tenant_id_screening_id",
                schema: "qams",
                table: "outlier_point",
                columns: new[] { "tenant_id", "screening_id" });

            migrationBuilder.CreateIndex(
                name: "ix_objective_progress_tenant_id_objective_id",
                schema: "qams",
                table: "objective_progress",
                columns: new[] { "tenant_id", "objective_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mitigation_action_tenant_id_risk_id",
                schema: "qams",
                table: "mitigation_action",
                columns: new[] { "tenant_id", "risk_id" });

            migrationBuilder.CreateIndex(
                name: "ix_measurement_pair_tenant_id_study_id",
                schema: "qams",
                table: "measurement_pair",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_record_tenant_id_equipment_id",
                schema: "qams",
                table: "maintenance_record",
                columns: new[] { "tenant_id", "equipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lot_sample_pair_tenant_id_study_id",
                schema: "qams",
                table: "lot_sample_pair",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_linearity_measurement_tenant_id_study_id",
                schema: "qams",
                table: "linearity_measurement",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_intermediate_check_tenant_id_equipment_id",
                schema: "qams",
                table: "intermediate_check",
                columns: new[] { "tenant_id", "equipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_interference_measurement_tenant_id_study_id",
                schema: "qams",
                table: "interference_measurement",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_reading_tenant_id_study_id",
                schema: "qams",
                table: "instrument_reading",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_environmental_reading_tenant_id_point_id",
                schema: "qams",
                table: "environmental_reading",
                columns: new[] { "tenant_id", "point_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_version_tenant_id_document_id",
                schema: "qams",
                table: "document_version",
                columns: new[] { "tenant_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_detection_measurement_tenant_id_study_id",
                schema: "qams",
                table: "detection_measurement",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_carryover_reading_tenant_id_study_id",
                schema: "qams",
                table: "carryover_reading",
                columns: new[] { "tenant_id", "study_id" });

            migrationBuilder.CreateIndex(
                name: "ix_capa_action_tenant_id_nc_id",
                schema: "qams",
                table: "capa_action",
                columns: new[] { "tenant_id", "nc_id" });

            migrationBuilder.CreateIndex(
                name: "ix_calibration_record_tenant_id_equipment_id",
                schema: "qams",
                table: "calibration_record",
                columns: new[] { "tenant_id", "equipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_finding_tenant_id_audit_id",
                schema: "qams",
                table: "audit_finding",
                columns: new[] { "tenant_id", "audit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_checklist_item_tenant_id_audit_id",
                schema: "qams",
                table: "audit_checklist_item",
                columns: new[] { "tenant_id", "audit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_result_tenant_id_competency_id",
                schema: "qams",
                table: "assessment_result",
                columns: new[] { "tenant_id", "competency_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_assessment_result_competency_record_tenant_id_competency_id",
                schema: "qams",
                table: "assessment_result",
                columns: new[] { "tenant_id", "competency_id" },
                principalSchema: "qams",
                principalTable: "competency_record",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_checklist_item_audit_tenant_id_audit_id",
                schema: "qams",
                table: "audit_checklist_item",
                columns: new[] { "tenant_id", "audit_id" },
                principalSchema: "qams",
                principalTable: "audit",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_finding_audit_tenant_id_audit_id",
                schema: "qams",
                table: "audit_finding",
                columns: new[] { "tenant_id", "audit_id" },
                principalSchema: "qams",
                principalTable: "audit",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_calibration_record_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "calibration_record",
                columns: new[] { "tenant_id", "equipment_id" },
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_capa_action_nonconformance_tenant_id_nc_id",
                schema: "qams",
                table: "capa_action",
                columns: new[] { "tenant_id", "nc_id" },
                principalSchema: "qams",
                principalTable: "nonconformance",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_carryover_reading_carryover_study_tenant_id_study_id",
                schema: "qams",
                table: "carryover_reading",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "carryover_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_department_branch_tenant_id_branch_id",
                schema: "qams",
                table: "department",
                columns: new[] { "tenant_id", "branch_id" },
                principalSchema: "qams",
                principalTable: "branch",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_detection_measurement_detection_limit_study_tenant_id_study",
                schema: "qams",
                table: "detection_measurement",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "detection_limit_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_document_version_controlled_document_tenant_id_document_id",
                schema: "qams",
                table: "document_version",
                columns: new[] { "tenant_id", "document_id" },
                principalSchema: "qams",
                principalTable: "controlled_document",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_environmental_reading_monitoring_point_tenant_id_point_id",
                schema: "qams",
                table: "environmental_reading",
                columns: new[] { "tenant_id", "point_id" },
                principalSchema: "qams",
                principalTable: "monitoring_point",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_instrument_reading_instrument_comparability_study_tenant_id",
                schema: "qams",
                table: "instrument_reading",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "instrument_comparability_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_interference_measurement_interference_study_tenant_id_study",
                schema: "qams",
                table: "interference_measurement",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "interference_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_intermediate_check_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "intermediate_check",
                columns: new[] { "tenant_id", "equipment_id" },
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_linearity_measurement_linearity_study_tenant_id_study_id",
                schema: "qams",
                table: "linearity_measurement",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "linearity_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lot_sample_pair_lot_comparison_study_tenant_id_study_id",
                schema: "qams",
                table: "lot_sample_pair",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "lot_comparison_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_maintenance_record_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "maintenance_record",
                columns: new[] { "tenant_id", "equipment_id" },
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_measurement_pair_method_comparison_study_tenant_id_study_id",
                schema: "qams",
                table: "measurement_pair",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "method_comparison_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_mitigation_action_risk_item_tenant_id_risk_id",
                schema: "qams",
                table: "mitigation_action",
                columns: new[] { "tenant_id", "risk_id" },
                principalSchema: "qams",
                principalTable: "risk_item",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_objective_progress_quality_objective_tenant_id_objective_id",
                schema: "qams",
                table: "objective_progress",
                columns: new[] { "tenant_id", "objective_id" },
                principalSchema: "qams",
                principalTable: "quality_objective",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_outlier_point_outlier_screening_tenant_id_screening_id",
                schema: "qams",
                table: "outlier_point",
                columns: new[] { "tenant_id", "screening_id" },
                principalSchema: "qams",
                principalTable: "outlier_screening",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_precision_measurement_precision_study_tenant_id_study_id",
                schema: "qams",
                table: "precision_measurement",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "precision_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pt_plan_item_pt_plan_tenant_id_plan_id",
                schema: "qams",
                table: "pt_plan_item",
                columns: new[] { "tenant_id", "plan_id" },
                principalSchema: "qams",
                principalTable: "pt_plan",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_rca_record_nonconformance_tenant_id_nc_id",
                schema: "qams",
                table: "rca_record",
                columns: new[] { "tenant_id", "nc_id" },
                principalSchema: "qams",
                principalTable: "nonconformance",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ref_sample_ri_study_tenant",
                schema: "qams",
                table: "reference_sample",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "reference_interval_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_review_decision_management_review_tenant_id_review_id",
                schema: "qams",
                table: "review_decision",
                columns: new[] { "tenant_id", "review_id" },
                principalSchema: "qams",
                principalTable: "management_review",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permission_role_tenant_id_role_id",
                schema: "qams",
                table: "role_permission",
                columns: new[] { "tenant_id", "role_id" },
                principalSchema: "qams",
                principalTable: "role",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_supplier_certificate_supplier_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_certificate",
                columns: new[] { "tenant_id", "supplier_id" },
                principalSchema: "qams",
                principalTable: "supplier",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_unc_component_unc_budget_tenant",
                schema: "qams",
                table: "uncertainty_component",
                columns: new[] { "tenant_id", "budget_id" },
                principalSchema: "qams",
                principalTable: "uncertainty_budget",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_validation_replicate_validation_study_tenant_id_study_id",
                schema: "qams",
                table: "validation_replicate",
                columns: new[] { "tenant_id", "study_id" },
                principalSchema: "qams",
                principalTable: "validation_study",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessment_result_competency_record_tenant_id_competency_id",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_checklist_item_audit_tenant_id_audit_id",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_finding_audit_tenant_id_audit_id",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropForeignKey(
                name: "fk_calibration_record_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropForeignKey(
                name: "fk_capa_action_nonconformance_tenant_id_nc_id",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropForeignKey(
                name: "fk_carryover_reading_carryover_study_tenant_id_study_id",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_department_branch_tenant_id_branch_id",
                schema: "qams",
                table: "department");

            migrationBuilder.DropForeignKey(
                name: "fk_detection_measurement_detection_limit_study_tenant_id_study",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_document_version_controlled_document_tenant_id_document_id",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropForeignKey(
                name: "fk_environmental_reading_monitoring_point_tenant_id_point_id",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_instrument_reading_instrument_comparability_study_tenant_id",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropForeignKey(
                name: "fk_interference_measurement_interference_study_tenant_id_study",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_intermediate_check_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropForeignKey(
                name: "fk_linearity_measurement_linearity_study_tenant_id_study_id",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_lot_sample_pair_lot_comparison_study_tenant_id_study_id",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropForeignKey(
                name: "fk_maintenance_record_equipment_item_tenant_id_equipment_id",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropForeignKey(
                name: "fk_measurement_pair_method_comparison_study_tenant_id_study_id",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropForeignKey(
                name: "fk_mitigation_action_risk_item_tenant_id_risk_id",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropForeignKey(
                name: "fk_objective_progress_quality_objective_tenant_id_objective_id",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropForeignKey(
                name: "fk_outlier_point_outlier_screening_tenant_id_screening_id",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropForeignKey(
                name: "fk_precision_measurement_precision_study_tenant_id_study_id",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_pt_plan_item_pt_plan_tenant_id_plan_id",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropForeignKey(
                name: "fk_rca_record_nonconformance_tenant_id_nc_id",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropForeignKey(
                name: "fk_ref_sample_ri_study_tenant",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropForeignKey(
                name: "fk_review_decision_management_review_tenant_id_review_id",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permission_role_tenant_id_role_id",
                schema: "qams",
                table: "role_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_supplier_certificate_supplier_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropForeignKey(
                name: "fk_unc_component_unc_budget_tenant",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropForeignKey(
                name: "fk_validation_replicate_validation_study_tenant_id_study_id",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_work_task",
                schema: "qams",
                table: "work_task");

            migrationBuilder.DropPrimaryKey(
                name: "pk_validation_study",
                schema: "qams",
                table: "validation_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_validation_replicate",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropIndex(
                name: "ix_validation_replicate_tenant_id_study_id",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_department_access",
                schema: "qams",
                table: "user_department_access");

            migrationBuilder.DropIndex(
                name: "ix_user_department_access_user_id",
                schema: "qams",
                table: "user_department_access");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_branch_access",
                schema: "qams",
                table: "user_branch_access");

            migrationBuilder.DropIndex(
                name: "ix_user_branch_access_user_id",
                schema: "qams",
                table: "user_branch_access");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_access_review",
                schema: "qams",
                table: "user_access_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_uncertainty_component",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropIndex(
                name: "ix_uncertainty_component_tenant_id_budget_id",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropPrimaryKey(
                name: "pk_uncertainty_budget",
                schema: "qams",
                table: "uncertainty_budget");

            migrationBuilder.DropPrimaryKey(
                name: "pk_training_assignment",
                schema: "qams",
                table: "training_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_test_catalog_item",
                schema: "qams",
                table: "test_catalog_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_test_authorization",
                schema: "qams",
                table: "test_authorization");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier_evaluation",
                schema: "qams",
                table: "supplier_evaluation");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier_certificate",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropIndex(
                name: "ix_supplier_certificate_tenant_id_supplier_id",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sla_definition",
                schema: "qams",
                table: "sla_definition");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sigma_assessment",
                schema: "qams",
                table: "sigma_assessment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permission",
                schema: "qams",
                table: "role_permission");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role",
                schema: "qams",
                table: "role");

            migrationBuilder.DropPrimaryKey(
                name: "pk_risk_item",
                schema: "qams",
                table: "risk_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_review_decision",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropIndex(
                name: "ix_review_decision_tenant_id_review_id",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_standard",
                schema: "qams",
                table: "reference_standard");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_sample",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropIndex(
                name: "ix_reference_sample_tenant_id_study_id",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropPrimaryKey(
                name: "pk_reference_interval_study",
                schema: "qams",
                table: "reference_interval_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_rca_record",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropIndex(
                name: "ix_rca_record_tenant_id_nc_id",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_quality_policy",
                schema: "qams",
                table: "quality_policy");

            migrationBuilder.DropPrimaryKey(
                name: "pk_quality_objective",
                schema: "qams",
                table: "quality_objective");

            migrationBuilder.DropPrimaryKey(
                name: "pk_qc_run",
                schema: "qams",
                table: "qc_run");

            migrationBuilder.DropPrimaryKey(
                name: "pk_qc_profile",
                schema: "qams",
                table: "qc_profile");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_plan_item",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropIndex(
                name: "ix_pt_plan_item_tenant_id_plan_id",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_plan",
                schema: "qams",
                table: "pt_plan");

            migrationBuilder.DropPrimaryKey(
                name: "pk_pt_enrollment",
                schema: "qams",
                table: "pt_enrollment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_precision_study",
                schema: "qams",
                table: "precision_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_precision_measurement",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropIndex(
                name: "ix_precision_measurement_tenant_id_study_id",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outlier_screening",
                schema: "qams",
                table: "outlier_screening");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outlier_point",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropIndex(
                name: "ix_outlier_point_tenant_id_screening_id",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropPrimaryKey(
                name: "pk_objective_progress",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropIndex(
                name: "ix_objective_progress_tenant_id_objective_id",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notification_rule",
                schema: "qams",
                table: "notification_rule");

            migrationBuilder.DropPrimaryKey(
                name: "pk_notification_dispatch",
                schema: "qams",
                table: "notification_dispatch");

            migrationBuilder.DropPrimaryKey(
                name: "pk_nonconformance",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropPrimaryKey(
                name: "pk_monitoring_point",
                schema: "qams",
                table: "monitoring_point");

            migrationBuilder.DropPrimaryKey(
                name: "pk_mitigation_action",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropIndex(
                name: "ix_mitigation_action_tenant_id_risk_id",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropPrimaryKey(
                name: "pk_method_comparison_study",
                schema: "qams",
                table: "method_comparison_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_measurement_pair",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropIndex(
                name: "ix_measurement_pair_tenant_id_study_id",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropPrimaryKey(
                name: "pk_management_review",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_maintenance_record",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_record_tenant_id_equipment_id",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lov_entry",
                schema: "qams",
                table: "lov_entry");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lot_sample_pair",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropIndex(
                name: "ix_lot_sample_pair_tenant_id_study_id",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lot_comparison_study",
                schema: "qams",
                table: "lot_comparison_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_linearity_study",
                schema: "qams",
                table: "linearity_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_linearity_measurement",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropIndex(
                name: "ix_linearity_measurement_tenant_id_study_id",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_kpi_snapshot",
                schema: "read",
                table: "kpi_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "pk_intermediate_check",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropIndex(
                name: "ix_intermediate_check_tenant_id_equipment_id",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interference_study",
                schema: "qams",
                table: "interference_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interference_measurement",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropIndex(
                name: "ix_interference_measurement_tenant_id_study_id",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_interested_party",
                schema: "qams",
                table: "interested_party");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instrument_reading",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropIndex(
                name: "ix_instrument_reading_tenant_id_study_id",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instrument_comparability_study",
                schema: "qams",
                table: "instrument_comparability_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_file_reference",
                schema: "qams",
                table: "file_reference");

            migrationBuilder.DropPrimaryKey(
                name: "pk_feedback_entry",
                schema: "qams",
                table: "feedback_entry");

            migrationBuilder.DropPrimaryKey(
                name: "pk_escalation_timer",
                schema: "qams",
                table: "escalation_timer");

            migrationBuilder.DropPrimaryKey(
                name: "pk_equipment_item",
                schema: "qams",
                table: "equipment_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_environmental_reading",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropIndex(
                name: "ix_environmental_reading_tenant_id_point_id",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_electronic_signature",
                schema: "audit",
                table: "electronic_signature");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_version",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropIndex(
                name: "ix_document_version_tenant_id_document_id",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_controlled_copy",
                schema: "qams",
                table: "document_controlled_copy");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_acknowledgement",
                schema: "qams",
                table: "document_acknowledgement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_detection_measurement",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropIndex(
                name: "ix_detection_measurement_tenant_id_study_id",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropPrimaryKey(
                name: "pk_detection_limit_study",
                schema: "qams",
                table: "detection_limit_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_department",
                schema: "qams",
                table: "department");

            migrationBuilder.DropPrimaryKey(
                name: "pk_controlled_document",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropPrimaryKey(
                name: "pk_context_issue",
                schema: "qams",
                table: "context_issue");

            migrationBuilder.DropPrimaryKey(
                name: "pk_conflict_declaration",
                schema: "qams",
                table: "conflict_declaration");

            migrationBuilder.DropPrimaryKey(
                name: "pk_complaint",
                schema: "qams",
                table: "complaint");

            migrationBuilder.DropPrimaryKey(
                name: "pk_competency_record",
                schema: "qams",
                table: "competency_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_change_request",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropPrimaryKey(
                name: "pk_carryover_study",
                schema: "qams",
                table: "carryover_study");

            migrationBuilder.DropPrimaryKey(
                name: "pk_carryover_reading",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropIndex(
                name: "ix_carryover_reading_tenant_id_study_id",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropPrimaryKey(
                name: "pk_capa_action",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropIndex(
                name: "ix_capa_action_tenant_id_nc_id",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropPrimaryKey(
                name: "pk_calibration_record",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropIndex(
                name: "ix_calibration_record_tenant_id_equipment_id",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropPrimaryKey(
                name: "pk_branch",
                schema: "qams",
                table: "branch");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_trail_review",
                schema: "qams",
                table: "audit_trail_review");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_trail",
                schema: "audit",
                table: "audit_trail");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_finding",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropIndex(
                name: "ix_audit_finding_tenant_id_audit_id",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit_checklist_item",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropIndex(
                name: "ix_audit_checklist_item_tenant_id_audit_id",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropPrimaryKey(
                name: "pk_audit",
                schema: "qams",
                table: "audit");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assessment_result",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropIndex(
                name: "ix_assessment_result_tenant_id_competency_id",
                schema: "qams",
                table: "assessment_result");

            migrationBuilder.DropPrimaryKey(
                name: "pk_archive_entry",
                schema: "qams",
                table: "archive_entry");

            migrationBuilder.AddPrimaryKey(
                name: "pk_work_task",
                schema: "qams",
                table: "work_task",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_validation_study",
                schema: "qams",
                table: "validation_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_validation_replicate",
                schema: "qams",
                table: "validation_replicate",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_department_access",
                schema: "qams",
                table: "user_department_access",
                columns: new[] { "user_id", "department_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_branch_access",
                schema: "qams",
                table: "user_branch_access",
                columns: new[] { "user_id", "branch_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_access_review",
                schema: "qams",
                table: "user_access_review",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_uncertainty_component",
                schema: "qams",
                table: "uncertainty_component",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_uncertainty_budget",
                schema: "qams",
                table: "uncertainty_budget",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_training_assignment",
                schema: "qams",
                table: "training_assignment",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_test_catalog_item",
                schema: "qams",
                table: "test_catalog_item",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_test_authorization",
                schema: "qams",
                table: "test_authorization",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier_evaluation",
                schema: "qams",
                table: "supplier_evaluation",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier_certificate",
                schema: "qams",
                table: "supplier_certificate",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier",
                schema: "qams",
                table: "supplier",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sla_definition",
                schema: "qams",
                table: "sla_definition",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sigma_assessment",
                schema: "qams",
                table: "sigma_assessment",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permission",
                schema: "qams",
                table: "role_permission",
                columns: new[] { "role_id", "permission_key" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_role",
                schema: "qams",
                table: "role",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_risk_item",
                schema: "qams",
                table: "risk_item",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_review_decision",
                schema: "qams",
                table: "review_decision",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_standard",
                schema: "qams",
                table: "reference_standard",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_sample",
                schema: "qams",
                table: "reference_sample",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_reference_interval_study",
                schema: "qams",
                table: "reference_interval_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_rca_record",
                schema: "qams",
                table: "rca_record",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_quality_policy",
                schema: "qams",
                table: "quality_policy",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_quality_objective",
                schema: "qams",
                table: "quality_objective",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_qc_run",
                schema: "qams",
                table: "qc_run",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_qc_profile",
                schema: "qams",
                table: "qc_profile",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_plan_item",
                schema: "qams",
                table: "pt_plan_item",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_plan",
                schema: "qams",
                table: "pt_plan",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_pt_enrollment",
                schema: "qams",
                table: "pt_enrollment",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_precision_study",
                schema: "qams",
                table: "precision_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_precision_measurement",
                schema: "qams",
                table: "precision_measurement",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_outlier_screening",
                schema: "qams",
                table: "outlier_screening",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_outlier_point",
                schema: "qams",
                table: "outlier_point",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_objective_progress",
                schema: "qams",
                table: "objective_progress",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notification_rule",
                schema: "qams",
                table: "notification_rule",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_notification_dispatch",
                schema: "qams",
                table: "notification_dispatch",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_nonconformance",
                schema: "qams",
                table: "nonconformance",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_monitoring_point",
                schema: "qams",
                table: "monitoring_point",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_mitigation_action",
                schema: "qams",
                table: "mitigation_action",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_method_comparison_study",
                schema: "qams",
                table: "method_comparison_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_measurement_pair",
                schema: "qams",
                table: "measurement_pair",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_management_review",
                schema: "qams",
                table: "management_review",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_maintenance_record",
                schema: "qams",
                table: "maintenance_record",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lov_entry",
                schema: "qams",
                table: "lov_entry",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lot_sample_pair",
                schema: "qams",
                table: "lot_sample_pair",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lot_comparison_study",
                schema: "qams",
                table: "lot_comparison_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_linearity_study",
                schema: "qams",
                table: "linearity_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_linearity_measurement",
                schema: "qams",
                table: "linearity_measurement",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_kpi_snapshot",
                schema: "read",
                table: "kpi_snapshot",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_intermediate_check",
                schema: "qams",
                table: "intermediate_check",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_interference_study",
                schema: "qams",
                table: "interference_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_interference_measurement",
                schema: "qams",
                table: "interference_measurement",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_interested_party",
                schema: "qams",
                table: "interested_party",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_instrument_reading",
                schema: "qams",
                table: "instrument_reading",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_instrument_comparability_study",
                schema: "qams",
                table: "instrument_comparability_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_file_reference",
                schema: "qams",
                table: "file_reference",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_feedback_entry",
                schema: "qams",
                table: "feedback_entry",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_escalation_timer",
                schema: "qams",
                table: "escalation_timer",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_equipment_item",
                schema: "qams",
                table: "equipment_item",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_environmental_reading",
                schema: "qams",
                table: "environmental_reading",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_electronic_signature",
                schema: "audit",
                table: "electronic_signature",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_version",
                schema: "qams",
                table: "document_version",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_controlled_copy",
                schema: "qams",
                table: "document_controlled_copy",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_acknowledgement",
                schema: "qams",
                table: "document_acknowledgement",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_detection_measurement",
                schema: "qams",
                table: "detection_measurement",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_detection_limit_study",
                schema: "qams",
                table: "detection_limit_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_department",
                schema: "qams",
                table: "department",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_controlled_document",
                schema: "qams",
                table: "controlled_document",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_context_issue",
                schema: "qams",
                table: "context_issue",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_conflict_declaration",
                schema: "qams",
                table: "conflict_declaration",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_complaint",
                schema: "qams",
                table: "complaint",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_competency_record",
                schema: "qams",
                table: "competency_record",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_change_request",
                schema: "qams",
                table: "change_request",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_carryover_study",
                schema: "qams",
                table: "carryover_study",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_carryover_reading",
                schema: "qams",
                table: "carryover_reading",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_capa_action",
                schema: "qams",
                table: "capa_action",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_calibration_record",
                schema: "qams",
                table: "calibration_record",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_branch",
                schema: "qams",
                table: "branch",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_trail_review",
                schema: "qams",
                table: "audit_trail_review",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_trail",
                schema: "audit",
                table: "audit_trail",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_finding",
                schema: "qams",
                table: "audit_finding",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit_checklist_item",
                schema: "qams",
                table: "audit_checklist_item",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_audit",
                schema: "qams",
                table: "audit",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assessment_result",
                schema: "qams",
                table: "assessment_result",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_archive_entry",
                schema: "qams",
                table: "archive_entry",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_replicate_study_id",
                schema: "qams",
                table: "validation_replicate",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_uncertainty_component_budget_id",
                schema: "qams",
                table: "uncertainty_component",
                column: "budget_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certificate_supplier_id",
                schema: "qams",
                table: "supplier_certificate",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_decision_review_id",
                schema: "qams",
                table: "review_decision",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "ix_reference_sample_study_id",
                schema: "qams",
                table: "reference_sample",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_rca_record_nc_id",
                schema: "qams",
                table: "rca_record",
                column: "nc_id");

            migrationBuilder.CreateIndex(
                name: "ix_pt_plan_item_plan_id",
                schema: "qams",
                table: "pt_plan_item",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_precision_measurement_study_id",
                schema: "qams",
                table: "precision_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_outlier_point_screening_id",
                schema: "qams",
                table: "outlier_point",
                column: "screening_id");

            migrationBuilder.CreateIndex(
                name: "ix_objective_progress_objective_id",
                schema: "qams",
                table: "objective_progress",
                column: "objective_id");

            migrationBuilder.CreateIndex(
                name: "ix_mitigation_action_risk_id",
                schema: "qams",
                table: "mitigation_action",
                column: "risk_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_pair_study_id",
                schema: "qams",
                table: "measurement_pair",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_record_equipment_id",
                schema: "qams",
                table: "maintenance_record",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_sample_pair_study_id",
                schema: "qams",
                table: "lot_sample_pair",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_linearity_measurement_study_id",
                schema: "qams",
                table: "linearity_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_intermediate_check_equipment_id",
                schema: "qams",
                table: "intermediate_check",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_interference_measurement_study_id",
                schema: "qams",
                table: "interference_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_reading_study_id",
                schema: "qams",
                table: "instrument_reading",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_version_document_id",
                schema: "qams",
                table: "document_version",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_detection_measurement_study_id",
                schema: "qams",
                table: "detection_measurement",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_department_branch_id",
                schema: "qams",
                table: "department",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_carryover_reading_study_id",
                schema: "qams",
                table: "carryover_reading",
                column: "study_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_action_nc_id",
                schema: "qams",
                table: "capa_action",
                column: "nc_id");

            migrationBuilder.CreateIndex(
                name: "ix_calibration_record_equipment_id",
                schema: "qams",
                table: "calibration_record",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_finding_audit_id",
                schema: "qams",
                table: "audit_finding",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_checklist_item_audit_id",
                schema: "qams",
                table: "audit_checklist_item",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_result_competency_id",
                schema: "qams",
                table: "assessment_result",
                column: "competency_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assessment_result_competency_record_tenant",
                schema: "qams",
                table: "assessment_result",
                column: "competency_id",
                principalSchema: "qams",
                principalTable: "competency_record",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_checklist_item_audit_tenant",
                schema: "qams",
                table: "audit_checklist_item",
                column: "audit_id",
                principalSchema: "qams",
                principalTable: "audit",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_finding_audit_tenant",
                schema: "qams",
                table: "audit_finding",
                column: "audit_id",
                principalSchema: "qams",
                principalTable: "audit",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_calibration_record_equipment_item_tenant",
                schema: "qams",
                table: "calibration_record",
                column: "equipment_id",
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_capa_action_nonconformance_tenant",
                schema: "qams",
                table: "capa_action",
                column: "nc_id",
                principalSchema: "qams",
                principalTable: "nonconformance",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_carryover_reading_carryover_study_tenant",
                schema: "qams",
                table: "carryover_reading",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "carryover_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_department_branch_branch_id",
                schema: "qams",
                table: "department",
                column: "branch_id",
                principalSchema: "qams",
                principalTable: "branch",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_detection_measurement_detection_limit_study_tenant",
                schema: "qams",
                table: "detection_measurement",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "detection_limit_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_document_version_controlled_document_tenant",
                schema: "qams",
                table: "document_version",
                column: "document_id",
                principalSchema: "qams",
                principalTable: "controlled_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_environmental_reading_monitoring_point_tenant",
                schema: "qams",
                table: "environmental_reading",
                column: "point_id",
                principalSchema: "qams",
                principalTable: "monitoring_point",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_instrument_reading_instrument_comparability_study_tenant",
                schema: "qams",
                table: "instrument_reading",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "instrument_comparability_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_interference_measurement_interference_study_tenant",
                schema: "qams",
                table: "interference_measurement",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "interference_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_intermediate_check_equipment_item_tenant",
                schema: "qams",
                table: "intermediate_check",
                column: "equipment_id",
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_linearity_measurement_linearity_study_tenant",
                schema: "qams",
                table: "linearity_measurement",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "linearity_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lot_sample_pair_lot_comparison_study_tenant",
                schema: "qams",
                table: "lot_sample_pair",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "lot_comparison_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_maintenance_record_equipment_item_tenant",
                schema: "qams",
                table: "maintenance_record",
                column: "equipment_id",
                principalSchema: "qams",
                principalTable: "equipment_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_measurement_pair_method_comparison_study_tenant",
                schema: "qams",
                table: "measurement_pair",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "method_comparison_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_mitigation_action_risk_item_tenant",
                schema: "qams",
                table: "mitigation_action",
                column: "risk_id",
                principalSchema: "qams",
                principalTable: "risk_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_objective_progress_quality_objective_tenant",
                schema: "qams",
                table: "objective_progress",
                column: "objective_id",
                principalSchema: "qams",
                principalTable: "quality_objective",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_outlier_point_outlier_screening_tenant",
                schema: "qams",
                table: "outlier_point",
                column: "screening_id",
                principalSchema: "qams",
                principalTable: "outlier_screening",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_precision_measurement_precision_study_tenant",
                schema: "qams",
                table: "precision_measurement",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "precision_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pt_plan_item_pt_plan_tenant",
                schema: "qams",
                table: "pt_plan_item",
                column: "plan_id",
                principalSchema: "qams",
                principalTable: "pt_plan",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_rca_record_nonconformance_tenant",
                schema: "qams",
                table: "rca_record",
                column: "nc_id",
                principalSchema: "qams",
                principalTable: "nonconformance",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reference_sample_reference_interval_study_tenant",
                schema: "qams",
                table: "reference_sample",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "reference_interval_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_review_decision_management_review_tenant",
                schema: "qams",
                table: "review_decision",
                column: "review_id",
                principalSchema: "qams",
                principalTable: "management_review",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permission_role_tenant",
                schema: "qams",
                table: "role_permission",
                column: "role_id",
                principalSchema: "qams",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_supplier_certificate_supplier_tenant",
                schema: "qams",
                table: "supplier_certificate",
                column: "supplier_id",
                principalSchema: "qams",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_uncertainty_component_uncertainty_budget_tenant",
                schema: "qams",
                table: "uncertainty_component",
                column: "budget_id",
                principalSchema: "qams",
                principalTable: "uncertainty_budget",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_validation_replicate_validation_study_tenant",
                schema: "qams",
                table: "validation_replicate",
                column: "study_id",
                principalSchema: "qams",
                principalTable: "validation_study",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
