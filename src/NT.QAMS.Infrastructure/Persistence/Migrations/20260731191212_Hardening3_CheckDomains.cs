using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening3_CheckDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema hardening Phase 3: database-level value domains.
            // Every IN-list below is generated from the same C# enum inventory that
            // generated the pre-flight scan (scripts/preflight-enum-domains.sql),
            // so the constraint and the scan cannot disagree. Executed clean
            // (0 violations) immediately before this migration was authored.
            // NOT VALID + VALIDATE: enforcement starts on new rows without a
            // long exclusive lock, then existing rows are proven.
            //
            // Deliberately NOT constrained (open sets - see SCHEMA-HARDENING-PLAN.md):
            //   audit.security_event.event_type   - open telemetry label
            //   audit.audit_trail.event_type      - CLR type names
            //   qams.outbox_event.event_type      - CLR type names
            //   qams.work_task.assignee_role      - dynamic role strings (post-v1.51)
            // token_hash is UPPERCASE hex (Convert.ToHexString); the other four
            // hash columns are lowercase (pre-flight verified both).
            migrationBuilder.Sql("""
ALTER TABLE qams.archive_entry ADD CONSTRAINT ck_archive_entry_retention_class_domain CHECK (retention_class IN ('FiveYears', 'TenYears', 'Permanent')) NOT VALID;
ALTER TABLE qams.archive_entry VALIDATE CONSTRAINT ck_archive_entry_retention_class_domain;
ALTER TABLE qams.archive_entry ADD CONSTRAINT ck_archive_entry_state_domain CHECK (state IN ('Archived', 'Retrieved', 'Disposed')) NOT VALID;
ALTER TABLE qams.archive_entry VALIDATE CONSTRAINT ck_archive_entry_state_domain;
ALTER TABLE qams.audit ADD CONSTRAINT ck_audit_status_domain CHECK (status IN ('Scheduled', 'InProgress', 'SignedOff')) NOT VALID;
ALTER TABLE qams.audit VALIDATE CONSTRAINT ck_audit_status_domain;
ALTER TABLE qams.audit ADD CONSTRAINT ck_audit_type_domain CHECK (type IN ('Internal', 'ExternalHosted')) NOT VALID;
ALTER TABLE qams.audit VALIDATE CONSTRAINT ck_audit_type_domain;
ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT ck_audit_checklist_item_verdict_domain CHECK (verdict IN ('Unanswered', 'Conform', 'Ofi', 'NonConform')) NOT VALID;
ALTER TABLE qams.audit_checklist_item VALIDATE CONSTRAINT ck_audit_checklist_item_verdict_domain;
ALTER TABLE qams.audit_finding ADD CONSTRAINT ck_audit_finding_grade_domain CHECK (grade IN ('Ofi', 'MinorNc', 'MajorNc')) NOT VALID;
ALTER TABLE qams.audit_finding VALIDATE CONSTRAINT ck_audit_finding_grade_domain;
ALTER TABLE qams.audit_trail_review ADD CONSTRAINT ck_audit_trail_review_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
ALTER TABLE qams.audit_trail_review VALIDATE CONSTRAINT ck_audit_trail_review_status_domain;
ALTER TABLE qams.capa_action ADD CONSTRAINT ck_capa_action_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
ALTER TABLE qams.capa_action VALIDATE CONSTRAINT ck_capa_action_status_domain;
ALTER TABLE qams.capa_action ADD CONSTRAINT ck_capa_action_type_domain CHECK (type IN ('Corrective', 'Preventive')) NOT VALID;
ALTER TABLE qams.capa_action VALIDATE CONSTRAINT ck_capa_action_type_domain;
ALTER TABLE qams.carryover_reading ADD CONSTRAINT ck_carryover_reading_kind_domain CHECK (kind IN ('High', 'Low')) NOT VALID;
ALTER TABLE qams.carryover_reading VALIDATE CONSTRAINT ck_carryover_reading_kind_domain;
ALTER TABLE qams.carryover_study ADD CONSTRAINT ck_carryover_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.carryover_study VALIDATE CONSTRAINT ck_carryover_study_state_domain;
ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_status_domain CHECK (status IN ('Proposed', 'Approved', 'Rejected', 'Closed', 'Reviewed')) NOT VALID;
ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_status_domain;
ALTER TABLE qams.competency_record ADD CONSTRAINT ck_competency_record_status_domain CHECK (status IN ('PendingTraining', 'Evaluated', 'Authorized', 'Revoked')) NOT VALID;
ALTER TABLE qams.competency_record VALIDATE CONSTRAINT ck_competency_record_status_domain;
ALTER TABLE qams.complaint ADD CONSTRAINT ck_complaint_channel_domain CHECK (channel IN ('Phone', 'Email', 'Portal', 'InPerson', 'Letter')) NOT VALID;
ALTER TABLE qams.complaint VALIDATE CONSTRAINT ck_complaint_channel_domain;
ALTER TABLE qams.complaint ADD CONSTRAINT ck_complaint_status_domain CHECK (status IN ('Logged', 'Acknowledged', 'Validated', 'Investigating', 'OutcomeLogged', 'Resolved', 'Closed', 'Invalid')) NOT VALID;
ALTER TABLE qams.complaint VALIDATE CONSTRAINT ck_complaint_status_domain;
ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_outcome_domain CHECK (outcome IN ('Accepted', 'Mitigated', 'Withdrawn')) NOT VALID;
ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_outcome_domain;
ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_risk_level_domain CHECK (risk_level IN ('Low', 'Medium', 'High')) NOT VALID;
ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_risk_level_domain;
ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_status_domain CHECK (status IN ('Declared', 'Assessed', 'Closed')) NOT VALID;
ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_status_domain;
ALTER TABLE qams.context_issue ADD CONSTRAINT ck_context_issue_status_domain CHECK (status IN ('Active', 'Closed')) NOT VALID;
ALTER TABLE qams.context_issue VALIDATE CONSTRAINT ck_context_issue_status_domain;
ALTER TABLE qams.context_issue ADD CONSTRAINT ck_context_issue_type_domain CHECK (type IN ('Internal', 'External')) NOT VALID;
ALTER TABLE qams.context_issue VALIDATE CONSTRAINT ck_context_issue_type_domain;
ALTER TABLE qams.controlled_document ADD CONSTRAINT ck_controlled_document_status_domain CHECK (status IN ('Draft', 'Published', 'Obsolete')) NOT VALID;
ALTER TABLE qams.controlled_document VALIDATE CONSTRAINT ck_controlled_document_status_domain;
ALTER TABLE qams.detection_limit_study ADD CONSTRAINT ck_detection_limit_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.detection_limit_study VALIDATE CONSTRAINT ck_detection_limit_study_state_domain;
ALTER TABLE qams.detection_measurement ADD CONSTRAINT ck_detection_measurement_kind_domain CHECK (kind IN ('Blank', 'LowLevel')) NOT VALID;
ALTER TABLE qams.detection_measurement VALIDATE CONSTRAINT ck_detection_measurement_kind_domain;
ALTER TABLE qams.document_controlled_copy ADD CONSTRAINT ck_document_controlled_copy_status_domain CHECK (status IN ('Issued', 'Returned', 'Destroyed')) NOT VALID;
ALTER TABLE qams.document_controlled_copy VALIDATE CONSTRAINT ck_document_controlled_copy_status_domain;
ALTER TABLE qams.document_version ADD CONSTRAINT ck_document_version_state_domain CHECK (state IN ('Draft', 'UnderReview', 'Approved', 'Published', 'Obsolete', 'Rejected')) NOT VALID;
ALTER TABLE qams.document_version VALIDATE CONSTRAINT ck_document_version_state_domain;
ALTER TABLE qams.equipment_item ADD CONSTRAINT ck_equipment_item_status_domain CHECK (status IN ('NeedsCalibration', 'Active', 'OutOfService', 'Retired')) NOT VALID;
ALTER TABLE qams.equipment_item VALIDATE CONSTRAINT ck_equipment_item_status_domain;
ALTER TABLE qams.feedback_entry ADD CONSTRAINT ck_feedback_entry_status_domain CHECK (status IN ('Logged', 'Reviewed', 'Closed', 'Escalated')) NOT VALID;
ALTER TABLE qams.feedback_entry VALIDATE CONSTRAINT ck_feedback_entry_status_domain;
ALTER TABLE qams.feedback_entry ADD CONSTRAINT ck_feedback_entry_type_domain CHECK (type IN ('Compliment', 'Suggestion', 'Dissatisfaction')) NOT VALID;
ALTER TABLE qams.feedback_entry VALIDATE CONSTRAINT ck_feedback_entry_type_domain;
ALTER TABLE qams.instrument_comparability_study ADD CONSTRAINT ck_instrument_comparability_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.instrument_comparability_study VALIDATE CONSTRAINT ck_instrument_comparability_study_state_domain;
ALTER TABLE qams.interested_party ADD CONSTRAINT ck_interested_party_status_domain CHECK (status IN ('Active', 'Archived')) NOT VALID;
ALTER TABLE qams.interested_party VALIDATE CONSTRAINT ck_interested_party_status_domain;
ALTER TABLE qams.interference_study ADD CONSTRAINT ck_interference_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.interference_study VALIDATE CONSTRAINT ck_interference_study_state_domain;
ALTER TABLE qams.linearity_study ADD CONSTRAINT ck_linearity_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.linearity_study VALIDATE CONSTRAINT ck_linearity_study_state_domain;
ALTER TABLE qams.lot_comparison_study ADD CONSTRAINT ck_lot_comparison_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.lot_comparison_study VALIDATE CONSTRAINT ck_lot_comparison_study_state_domain;
ALTER TABLE qams.management_review ADD CONSTRAINT ck_management_review_status_domain CHECK (status IN ('Scheduled', 'Closed')) NOT VALID;
ALTER TABLE qams.management_review VALIDATE CONSTRAINT ck_management_review_status_domain;
ALTER TABLE qams.method_comparison_study ADD CONSTRAINT ck_method_comparison_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.method_comparison_study VALIDATE CONSTRAINT ck_method_comparison_study_state_domain;
ALTER TABLE qams.monitoring_point ADD CONSTRAINT ck_monitoring_point_status_domain CHECK (status IN ('Active', 'Suspended', 'Retired')) NOT VALID;
ALTER TABLE qams.monitoring_point VALIDATE CONSTRAINT ck_monitoring_point_status_domain;
ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_event_type_domain CHECK (event_type IN ('Nonconformity', 'Deviation', 'OutOfSpecification', 'OutOfTrend')) NOT VALID;
ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_event_type_domain;
ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain CHECK (source_type IN ('Internal', 'Complaint', 'Audit', 'Supplier', 'ProficiencyTest')) NOT VALID;
ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
ALTER TABLE qams.notification_dispatch ADD CONSTRAINT ck_notification_dispatch_email_status_domain CHECK (email_status IN ('Queued', 'Sent', 'Failed')) NOT VALID;
ALTER TABLE qams.notification_dispatch VALIDATE CONSTRAINT ck_notification_dispatch_email_status_domain;
ALTER TABLE qams.outlier_screening ADD CONSTRAINT ck_outlier_screening_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.outlier_screening VALIDATE CONSTRAINT ck_outlier_screening_state_domain;
ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.precision_study VALIDATE CONSTRAINT ck_precision_study_state_domain;
ALTER TABLE qams.pt_enrollment ADD CONSTRAINT ck_pt_enrollment_performance_domain CHECK (performance IN ('Pending', 'Satisfactory', 'Questionable', 'Unsatisfactory')) NOT VALID;
ALTER TABLE qams.pt_enrollment VALIDATE CONSTRAINT ck_pt_enrollment_performance_domain;
ALTER TABLE qams.pt_plan ADD CONSTRAINT ck_pt_plan_status_domain CHECK (status IN ('Draft', 'Approved', 'Closed')) NOT VALID;
ALTER TABLE qams.pt_plan VALIDATE CONSTRAINT ck_pt_plan_status_domain;
ALTER TABLE qams.quality_objective ADD CONSTRAINT ck_quality_objective_direction_domain CHECK (direction IN ('AtLeast', 'AtMost')) NOT VALID;
ALTER TABLE qams.quality_objective VALIDATE CONSTRAINT ck_quality_objective_direction_domain;
ALTER TABLE qams.quality_objective ADD CONSTRAINT ck_quality_objective_status_domain CHECK (status IN ('Active', 'Achieved', 'Missed', 'Cancelled')) NOT VALID;
ALTER TABLE qams.quality_objective VALIDATE CONSTRAINT ck_quality_objective_status_domain;
ALTER TABLE qams.quality_policy ADD CONSTRAINT ck_quality_policy_status_domain CHECK (status IN ('Draft', 'Active', 'Superseded')) NOT VALID;
ALTER TABLE qams.quality_policy VALIDATE CONSTRAINT ck_quality_policy_status_domain;
ALTER TABLE qams.rca_record ADD CONSTRAINT ck_rca_record_method_domain CHECK (method IN ('FiveWhys', 'Fishbone', 'Other')) NOT VALID;
ALTER TABLE qams.rca_record VALIDATE CONSTRAINT ck_rca_record_method_domain;
ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ck_reference_interval_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.reference_interval_study VALIDATE CONSTRAINT ck_reference_interval_study_state_domain;
ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ck_reference_interval_study_verdict_domain CHECK (verdict IN ('Verified', 'Rejected')) NOT VALID;
ALTER TABLE qams.reference_interval_study VALIDATE CONSTRAINT ck_reference_interval_study_verdict_domain;
ALTER TABLE qams.reference_standard ADD CONSTRAINT ck_reference_standard_status_domain CHECK (status IN ('Active', 'Quarantined', 'Expired', 'Retired')) NOT VALID;
ALTER TABLE qams.reference_standard VALIDATE CONSTRAINT ck_reference_standard_status_domain;
ALTER TABLE qams.reference_standard ADD CONSTRAINT ck_reference_standard_type_domain CHECK (type IN ('CertifiedReferenceMaterial', 'ReferenceStandard', 'WorkingStandard')) NOT VALID;
ALTER TABLE qams.reference_standard VALIDATE CONSTRAINT ck_reference_standard_type_domain;
ALTER TABLE qams.risk_item ADD CONSTRAINT ck_risk_item_status_domain CHECK (status IN ('Identified', 'Mitigating', 'Closed')) NOT VALID;
ALTER TABLE qams.risk_item VALIDATE CONSTRAINT ck_risk_item_status_domain;
ALTER TABLE qams.sigma_assessment ADD CONSTRAINT ck_sigma_assessment_grade_domain CHECK (grade IN ('Unacceptable', 'Marginal', 'Good', 'Excellent', 'WorldClass')) NOT VALID;
ALTER TABLE qams.sigma_assessment VALIDATE CONSTRAINT ck_sigma_assessment_grade_domain;
ALTER TABLE qams.sigma_assessment ADD CONSTRAINT ck_sigma_assessment_state_domain CHECK (state IN ('Draft', 'SignedOff')) NOT VALID;
ALTER TABLE qams.sigma_assessment VALIDATE CONSTRAINT ck_sigma_assessment_state_domain;
ALTER TABLE qams.supplier ADD CONSTRAINT ck_supplier_status_domain CHECK (status IN ('PendingEvaluation', 'Approved', 'Suspended')) NOT VALID;
ALTER TABLE qams.supplier VALIDATE CONSTRAINT ck_supplier_status_domain;
ALTER TABLE qams.test_authorization ADD CONSTRAINT ck_test_authorization_scope_domain CHECK (scope IN ('Perform', 'ReviewAndRelease', 'Train')) NOT VALID;
ALTER TABLE qams.test_authorization VALIDATE CONSTRAINT ck_test_authorization_scope_domain;
ALTER TABLE qams.test_authorization ADD CONSTRAINT ck_test_authorization_status_domain CHECK (status IN ('Active', 'Suspended', 'Revoked', 'Expired')) NOT VALID;
ALTER TABLE qams.test_authorization VALIDATE CONSTRAINT ck_test_authorization_status_domain;
ALTER TABLE qams.uncertainty_budget ADD CONSTRAINT ck_uncertainty_budget_status_domain CHECK (status IN ('Draft', 'Calculated', 'Approved')) NOT VALID;
ALTER TABLE qams.uncertainty_budget VALIDATE CONSTRAINT ck_uncertainty_budget_status_domain;
ALTER TABLE qams.uncertainty_component ADD CONSTRAINT ck_uncertainty_component_type_domain CHECK (type IN ('TypeA', 'TypeB')) NOT VALID;
ALTER TABLE qams.uncertainty_component VALIDATE CONSTRAINT ck_uncertainty_component_type_domain;
ALTER TABLE qams.user_access_review ADD CONSTRAINT ck_user_access_review_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
ALTER TABLE qams.user_access_review VALIDATE CONSTRAINT ck_user_access_review_status_domain;
ALTER TABLE qams.user_account ADD CONSTRAINT ck_user_account_role_domain CHECK (role IN ('PlatformAdmin', 'TenantAdmin', 'QualityManager', 'DepartmentHead', 'Analyst', 'ExternalAuditor')) NOT VALID;
ALTER TABLE qams.user_account VALIDATE CONSTRAINT ck_user_account_role_domain;
ALTER TABLE qams.validation_study ADD CONSTRAINT ck_validation_study_state_domain CHECK (state IN ('ProtocolConfigured', 'DataEntered', 'StatsCalculated', 'SignedOff')) NOT VALID;
ALTER TABLE qams.validation_study VALIDATE CONSTRAINT ck_validation_study_state_domain;
ALTER TABLE qams.work_task ADD CONSTRAINT ck_work_task_status_domain CHECK (status IN ('Pending', 'Completed')) NOT VALID;
ALTER TABLE qams.work_task VALIDATE CONSTRAINT ck_work_task_status_domain;
ALTER TABLE saas.tenant ADD CONSTRAINT ck_tenant_status_domain CHECK (status IN ('Provisioning', 'Active', 'Suspended', 'Terminated')) NOT VALID;
ALTER TABLE saas.tenant VALIDATE CONSTRAINT ck_tenant_status_domain;
ALTER TABLE qams.qc_run ADD CONSTRAINT ck_qc_run_outcome_domain CHECK (outcome IN ('InControl', 'Warning', 'OutOfControl')) NOT VALID;
ALTER TABLE qams.qc_run VALIDATE CONSTRAINT ck_qc_run_outcome_domain;
ALTER TABLE audit.field_change ADD CONSTRAINT ck_field_change_action_domain CHECK (action IN ('Created', 'Modified', 'Deleted')) NOT VALID;
ALTER TABLE audit.field_change VALIDATE CONSTRAINT ck_field_change_action_domain;
ALTER TABLE audit.audit_trail ADD CONSTRAINT ck_audit_trail_prev_hash_sha256 CHECK (prev_hash ~ '^[0-9a-f]{64}$') NOT VALID;
ALTER TABLE audit.audit_trail VALIDATE CONSTRAINT ck_audit_trail_prev_hash_sha256;
ALTER TABLE audit.audit_trail ADD CONSTRAINT ck_audit_trail_entry_hash_sha256 CHECK (entry_hash ~ '^[0-9a-f]{64}$') NOT VALID;
ALTER TABLE audit.audit_trail VALIDATE CONSTRAINT ck_audit_trail_entry_hash_sha256;
ALTER TABLE audit.electronic_signature ADD CONSTRAINT ck_electronic_signature_content_hash_sha256 CHECK (content_hash ~ '^[0-9a-f]{64}$') NOT VALID;
ALTER TABLE audit.electronic_signature VALIDATE CONSTRAINT ck_electronic_signature_content_hash_sha256;
ALTER TABLE qams.file_reference ADD CONSTRAINT ck_file_reference_sha256_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$') NOT VALID;
ALTER TABLE qams.file_reference VALIDATE CONSTRAINT ck_file_reference_sha256_sha256;
ALTER TABLE qams.refresh_session ADD CONSTRAINT ck_refresh_session_token_hash_sha256 CHECK (token_hash ~ '^[0-9A-F]{64}$') NOT VALID;
ALTER TABLE qams.refresh_session VALIDATE CONSTRAINT ck_refresh_session_token_hash_sha256;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE qams.archive_entry DROP CONSTRAINT IF EXISTS ck_archive_entry_retention_class_domain;
ALTER TABLE qams.archive_entry DROP CONSTRAINT IF EXISTS ck_archive_entry_state_domain;
ALTER TABLE qams.audit DROP CONSTRAINT IF EXISTS ck_audit_status_domain;
ALTER TABLE qams.audit DROP CONSTRAINT IF EXISTS ck_audit_type_domain;
ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT IF EXISTS ck_audit_checklist_item_verdict_domain;
ALTER TABLE qams.audit_finding DROP CONSTRAINT IF EXISTS ck_audit_finding_grade_domain;
ALTER TABLE qams.audit_trail_review DROP CONSTRAINT IF EXISTS ck_audit_trail_review_status_domain;
ALTER TABLE qams.capa_action DROP CONSTRAINT IF EXISTS ck_capa_action_status_domain;
ALTER TABLE qams.capa_action DROP CONSTRAINT IF EXISTS ck_capa_action_type_domain;
ALTER TABLE qams.carryover_reading DROP CONSTRAINT IF EXISTS ck_carryover_reading_kind_domain;
ALTER TABLE qams.carryover_study DROP CONSTRAINT IF EXISTS ck_carryover_study_state_domain;
ALTER TABLE qams.change_request DROP CONSTRAINT IF EXISTS ck_change_request_status_domain;
ALTER TABLE qams.competency_record DROP CONSTRAINT IF EXISTS ck_competency_record_status_domain;
ALTER TABLE qams.complaint DROP CONSTRAINT IF EXISTS ck_complaint_channel_domain;
ALTER TABLE qams.complaint DROP CONSTRAINT IF EXISTS ck_complaint_status_domain;
ALTER TABLE qams.conflict_declaration DROP CONSTRAINT IF EXISTS ck_conflict_declaration_outcome_domain;
ALTER TABLE qams.conflict_declaration DROP CONSTRAINT IF EXISTS ck_conflict_declaration_risk_level_domain;
ALTER TABLE qams.conflict_declaration DROP CONSTRAINT IF EXISTS ck_conflict_declaration_status_domain;
ALTER TABLE qams.context_issue DROP CONSTRAINT IF EXISTS ck_context_issue_status_domain;
ALTER TABLE qams.context_issue DROP CONSTRAINT IF EXISTS ck_context_issue_type_domain;
ALTER TABLE qams.controlled_document DROP CONSTRAINT IF EXISTS ck_controlled_document_status_domain;
ALTER TABLE qams.detection_limit_study DROP CONSTRAINT IF EXISTS ck_detection_limit_study_state_domain;
ALTER TABLE qams.detection_measurement DROP CONSTRAINT IF EXISTS ck_detection_measurement_kind_domain;
ALTER TABLE qams.document_controlled_copy DROP CONSTRAINT IF EXISTS ck_document_controlled_copy_status_domain;
ALTER TABLE qams.document_version DROP CONSTRAINT IF EXISTS ck_document_version_state_domain;
ALTER TABLE qams.equipment_item DROP CONSTRAINT IF EXISTS ck_equipment_item_status_domain;
ALTER TABLE qams.feedback_entry DROP CONSTRAINT IF EXISTS ck_feedback_entry_status_domain;
ALTER TABLE qams.feedback_entry DROP CONSTRAINT IF EXISTS ck_feedback_entry_type_domain;
ALTER TABLE qams.instrument_comparability_study DROP CONSTRAINT IF EXISTS ck_instrument_comparability_study_state_domain;
ALTER TABLE qams.interested_party DROP CONSTRAINT IF EXISTS ck_interested_party_status_domain;
ALTER TABLE qams.interference_study DROP CONSTRAINT IF EXISTS ck_interference_study_state_domain;
ALTER TABLE qams.linearity_study DROP CONSTRAINT IF EXISTS ck_linearity_study_state_domain;
ALTER TABLE qams.lot_comparison_study DROP CONSTRAINT IF EXISTS ck_lot_comparison_study_state_domain;
ALTER TABLE qams.management_review DROP CONSTRAINT IF EXISTS ck_management_review_status_domain;
ALTER TABLE qams.method_comparison_study DROP CONSTRAINT IF EXISTS ck_method_comparison_study_state_domain;
ALTER TABLE qams.monitoring_point DROP CONSTRAINT IF EXISTS ck_monitoring_point_status_domain;
ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_event_type_domain;
ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
ALTER TABLE qams.notification_dispatch DROP CONSTRAINT IF EXISTS ck_notification_dispatch_email_status_domain;
ALTER TABLE qams.outlier_screening DROP CONSTRAINT IF EXISTS ck_outlier_screening_state_domain;
ALTER TABLE qams.precision_study DROP CONSTRAINT IF EXISTS ck_precision_study_state_domain;
ALTER TABLE qams.pt_enrollment DROP CONSTRAINT IF EXISTS ck_pt_enrollment_performance_domain;
ALTER TABLE qams.pt_plan DROP CONSTRAINT IF EXISTS ck_pt_plan_status_domain;
ALTER TABLE qams.quality_objective DROP CONSTRAINT IF EXISTS ck_quality_objective_direction_domain;
ALTER TABLE qams.quality_objective DROP CONSTRAINT IF EXISTS ck_quality_objective_status_domain;
ALTER TABLE qams.quality_policy DROP CONSTRAINT IF EXISTS ck_quality_policy_status_domain;
ALTER TABLE qams.rca_record DROP CONSTRAINT IF EXISTS ck_rca_record_method_domain;
ALTER TABLE qams.reference_interval_study DROP CONSTRAINT IF EXISTS ck_reference_interval_study_state_domain;
ALTER TABLE qams.reference_interval_study DROP CONSTRAINT IF EXISTS ck_reference_interval_study_verdict_domain;
ALTER TABLE qams.reference_standard DROP CONSTRAINT IF EXISTS ck_reference_standard_status_domain;
ALTER TABLE qams.reference_standard DROP CONSTRAINT IF EXISTS ck_reference_standard_type_domain;
ALTER TABLE qams.risk_item DROP CONSTRAINT IF EXISTS ck_risk_item_status_domain;
ALTER TABLE qams.sigma_assessment DROP CONSTRAINT IF EXISTS ck_sigma_assessment_grade_domain;
ALTER TABLE qams.sigma_assessment DROP CONSTRAINT IF EXISTS ck_sigma_assessment_state_domain;
ALTER TABLE qams.supplier DROP CONSTRAINT IF EXISTS ck_supplier_status_domain;
ALTER TABLE qams.test_authorization DROP CONSTRAINT IF EXISTS ck_test_authorization_scope_domain;
ALTER TABLE qams.test_authorization DROP CONSTRAINT IF EXISTS ck_test_authorization_status_domain;
ALTER TABLE qams.uncertainty_budget DROP CONSTRAINT IF EXISTS ck_uncertainty_budget_status_domain;
ALTER TABLE qams.uncertainty_component DROP CONSTRAINT IF EXISTS ck_uncertainty_component_type_domain;
ALTER TABLE qams.user_access_review DROP CONSTRAINT IF EXISTS ck_user_access_review_status_domain;
ALTER TABLE qams.user_account DROP CONSTRAINT IF EXISTS ck_user_account_role_domain;
ALTER TABLE qams.validation_study DROP CONSTRAINT IF EXISTS ck_validation_study_state_domain;
ALTER TABLE qams.work_task DROP CONSTRAINT IF EXISTS ck_work_task_status_domain;
ALTER TABLE saas.tenant DROP CONSTRAINT IF EXISTS ck_tenant_status_domain;
ALTER TABLE qams.qc_run DROP CONSTRAINT IF EXISTS ck_qc_run_outcome_domain;
ALTER TABLE audit.field_change DROP CONSTRAINT IF EXISTS ck_field_change_action_domain;
ALTER TABLE audit.audit_trail DROP CONSTRAINT IF EXISTS ck_audit_trail_prev_hash_sha256;
ALTER TABLE audit.audit_trail DROP CONSTRAINT IF EXISTS ck_audit_trail_entry_hash_sha256;
ALTER TABLE audit.electronic_signature DROP CONSTRAINT IF EXISTS ck_electronic_signature_content_hash_sha256;
ALTER TABLE qams.file_reference DROP CONSTRAINT IF EXISTS ck_file_reference_sha256_sha256;
ALTER TABLE qams.refresh_session DROP CONSTRAINT IF EXISTS ck_refresh_session_token_hash_sha256;
""");
        }
    }
}
