-- Pre-flight data validation for the schema-hardening migrations (read-only).
-- Run before each phase; every count must be 0.
-- Rev 2 (post-Phase-1): criteria is jsonb and ip_address is inet, so their old
-- content checks are now structural - asserted as type checks instead.
SELECT set_config('app.bypass_rls','on',false);

SELECT 'criteria-not-jsonb' AS chk, count(*) FROM information_schema.columns WHERE table_schema='qams' AND table_name='supplier_evaluation' AND column_name='criteria' AND data_type <> 'jsonb';
SELECT 'ip-not-inet' AS chk, count(*) FROM information_schema.columns WHERE table_schema='audit' AND table_name='security_event' AND column_name='ip_address' AND data_type <> 'inet';

-- Phase 4: scope rows owned by platform admins would break the NOT NULL backfill.
SELECT 'platform-scope-rows' AS chk, (SELECT count(*) FROM qams.user_branch_access b JOIN qams.user_account u ON u.id=b.user_id WHERE u.tenant_id IS NULL) + (SELECT count(*) FROM qams.user_department_access d JOIN qams.user_account u ON u.id=d.user_id WHERE u.tenant_id IS NULL);

-- Phase 4: no orphaned owned-child rows (parent gone would break the tenant backfill).
-- The CASCADE FKs make orphans structurally impossible; this proves it anyway.
SELECT 'orphan-assessment_result' AS chk, count(*) FROM qams.assessment_result c WHERE NOT EXISTS (SELECT 1 FROM qams.competency_record p WHERE p.id = c.competency_id);
SELECT 'orphan-audit_checklist_item' AS chk, count(*) FROM qams.audit_checklist_item c WHERE NOT EXISTS (SELECT 1 FROM qams.audit p WHERE p.id = c.audit_id);
SELECT 'orphan-audit_finding' AS chk, count(*) FROM qams.audit_finding c WHERE NOT EXISTS (SELECT 1 FROM qams.audit p WHERE p.id = c.audit_id);
SELECT 'orphan-calibration_record' AS chk, count(*) FROM qams.calibration_record c WHERE NOT EXISTS (SELECT 1 FROM qams.equipment_item p WHERE p.id = c.equipment_id);
SELECT 'orphan-capa_action' AS chk, count(*) FROM qams.capa_action c WHERE NOT EXISTS (SELECT 1 FROM qams.nonconformance p WHERE p.id = c.nc_id);
SELECT 'orphan-carryover_reading' AS chk, count(*) FROM qams.carryover_reading c WHERE NOT EXISTS (SELECT 1 FROM qams.carryover_study p WHERE p.id = c.study_id);
SELECT 'orphan-detection_measurement' AS chk, count(*) FROM qams.detection_measurement c WHERE NOT EXISTS (SELECT 1 FROM qams.detection_limit_study p WHERE p.id = c.study_id);
SELECT 'orphan-document_version' AS chk, count(*) FROM qams.document_version c WHERE NOT EXISTS (SELECT 1 FROM qams.controlled_document p WHERE p.id = c.document_id);
SELECT 'orphan-environmental_reading' AS chk, count(*) FROM qams.environmental_reading c WHERE NOT EXISTS (SELECT 1 FROM qams.monitoring_point p WHERE p.id = c.point_id);
SELECT 'orphan-instrument_reading' AS chk, count(*) FROM qams.instrument_reading c WHERE NOT EXISTS (SELECT 1 FROM qams.instrument_comparability_study p WHERE p.id = c.study_id);
SELECT 'orphan-interference_measurement' AS chk, count(*) FROM qams.interference_measurement c WHERE NOT EXISTS (SELECT 1 FROM qams.interference_study p WHERE p.id = c.study_id);
SELECT 'orphan-intermediate_check' AS chk, count(*) FROM qams.intermediate_check c WHERE NOT EXISTS (SELECT 1 FROM qams.equipment_item p WHERE p.id = c.equipment_id);
SELECT 'orphan-linearity_measurement' AS chk, count(*) FROM qams.linearity_measurement c WHERE NOT EXISTS (SELECT 1 FROM qams.linearity_study p WHERE p.id = c.study_id);
SELECT 'orphan-lot_sample_pair' AS chk, count(*) FROM qams.lot_sample_pair c WHERE NOT EXISTS (SELECT 1 FROM qams.lot_comparison_study p WHERE p.id = c.study_id);
SELECT 'orphan-maintenance_record' AS chk, count(*) FROM qams.maintenance_record c WHERE NOT EXISTS (SELECT 1 FROM qams.equipment_item p WHERE p.id = c.equipment_id);
SELECT 'orphan-measurement_pair' AS chk, count(*) FROM qams.measurement_pair c WHERE NOT EXISTS (SELECT 1 FROM qams.method_comparison_study p WHERE p.id = c.study_id);
SELECT 'orphan-mitigation_action' AS chk, count(*) FROM qams.mitigation_action c WHERE NOT EXISTS (SELECT 1 FROM qams.risk_item p WHERE p.id = c.risk_id);
SELECT 'orphan-objective_progress' AS chk, count(*) FROM qams.objective_progress c WHERE NOT EXISTS (SELECT 1 FROM qams.quality_objective p WHERE p.id = c.objective_id);
SELECT 'orphan-outlier_point' AS chk, count(*) FROM qams.outlier_point c WHERE NOT EXISTS (SELECT 1 FROM qams.outlier_screening p WHERE p.id = c.screening_id);
SELECT 'orphan-precision_measurement' AS chk, count(*) FROM qams.precision_measurement c WHERE NOT EXISTS (SELECT 1 FROM qams.precision_study p WHERE p.id = c.study_id);
SELECT 'orphan-pt_plan_item' AS chk, count(*) FROM qams.pt_plan_item c WHERE NOT EXISTS (SELECT 1 FROM qams.pt_plan p WHERE p.id = c.plan_id);
SELECT 'orphan-rca_record' AS chk, count(*) FROM qams.rca_record c WHERE NOT EXISTS (SELECT 1 FROM qams.nonconformance p WHERE p.id = c.nc_id);
SELECT 'orphan-reference_sample' AS chk, count(*) FROM qams.reference_sample c WHERE NOT EXISTS (SELECT 1 FROM qams.reference_interval_study p WHERE p.id = c.study_id);
SELECT 'orphan-review_decision' AS chk, count(*) FROM qams.review_decision c WHERE NOT EXISTS (SELECT 1 FROM qams.management_review p WHERE p.id = c.review_id);
SELECT 'orphan-role_permission' AS chk, count(*) FROM qams.role_permission c WHERE NOT EXISTS (SELECT 1 FROM qams.role p WHERE p.id = c.role_id);
SELECT 'orphan-supplier_certificate' AS chk, count(*) FROM qams.supplier_certificate c WHERE NOT EXISTS (SELECT 1 FROM qams.supplier p WHERE p.id = c.supplier_id);
SELECT 'orphan-uncertainty_component' AS chk, count(*) FROM qams.uncertainty_component c WHERE NOT EXISTS (SELECT 1 FROM qams.uncertainty_budget p WHERE p.id = c.budget_id);
SELECT 'orphan-user_branch_access' AS chk, count(*) FROM qams.user_branch_access c WHERE NOT EXISTS (SELECT 1 FROM qams.user_account p WHERE p.id = c.user_id);
SELECT 'orphan-user_department_access' AS chk, count(*) FROM qams.user_department_access c WHERE NOT EXISTS (SELECT 1 FROM qams.user_account p WHERE p.id = c.user_id);
SELECT 'orphan-validation_replicate' AS chk, count(*) FROM qams.validation_replicate c WHERE NOT EXISTS (SELECT 1 FROM qams.validation_study p WHERE p.id = c.study_id);
