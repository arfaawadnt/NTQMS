using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening4_ChildTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "validation_replicate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "user_department_access",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "user_branch_access",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "uncertainty_component",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "supplier_certificate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "role_permission",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "review_decision",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "reference_sample",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "rca_record",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "pt_plan_item",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "precision_measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "outlier_point",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "objective_progress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "mitigation_action",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "measurement_pair",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "maintenance_record",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "lot_sample_pair",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "linearity_measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "intermediate_check",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "interference_measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "instrument_reading",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "environmental_reading",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "document_version",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "detection_measurement",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "carryover_reading",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "capa_action",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "calibration_record",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "audit_finding",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "audit_checklist_item",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "qams",
                table: "assessment_result",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
-- 0) This migration is trusted infrastructure: the backfill below must read
-- parent rows across every tenant, and the parents' own tenant_isolation
-- policies would otherwise hide them from the tenant-less migration session
-- (the round-trip proved it: every UPDATE..FROM was a no-op and the first
-- composite FK failed on a nil tenant). Transaction-local, so nothing leaks.
SELECT set_config('app.bypass_rls', 'on', true);

-- 1) Backfill tenant_id from the owning aggregate, then drop the nil default.
UPDATE qams.assessment_result c SET tenant_id = p.tenant_id FROM qams.competency_record p WHERE p.id = c.competency_id;
ALTER TABLE qams.assessment_result ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.audit_checklist_item c SET tenant_id = p.tenant_id FROM qams.audit p WHERE p.id = c.audit_id;
ALTER TABLE qams.audit_checklist_item ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.audit_finding c SET tenant_id = p.tenant_id FROM qams.audit p WHERE p.id = c.audit_id;
ALTER TABLE qams.audit_finding ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.calibration_record c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
ALTER TABLE qams.calibration_record ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.capa_action c SET tenant_id = p.tenant_id FROM qams.nonconformance p WHERE p.id = c.nc_id;
ALTER TABLE qams.capa_action ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.carryover_reading c SET tenant_id = p.tenant_id FROM qams.carryover_study p WHERE p.id = c.study_id;
ALTER TABLE qams.carryover_reading ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.detection_measurement c SET tenant_id = p.tenant_id FROM qams.detection_limit_study p WHERE p.id = c.study_id;
ALTER TABLE qams.detection_measurement ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.document_version c SET tenant_id = p.tenant_id FROM qams.controlled_document p WHERE p.id = c.document_id;
ALTER TABLE qams.document_version ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.environmental_reading c SET tenant_id = p.tenant_id FROM qams.monitoring_point p WHERE p.id = c.point_id;
ALTER TABLE qams.environmental_reading ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.instrument_reading c SET tenant_id = p.tenant_id FROM qams.instrument_comparability_study p WHERE p.id = c.study_id;
ALTER TABLE qams.instrument_reading ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.interference_measurement c SET tenant_id = p.tenant_id FROM qams.interference_study p WHERE p.id = c.study_id;
ALTER TABLE qams.interference_measurement ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.intermediate_check c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
ALTER TABLE qams.intermediate_check ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.linearity_measurement c SET tenant_id = p.tenant_id FROM qams.linearity_study p WHERE p.id = c.study_id;
ALTER TABLE qams.linearity_measurement ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.lot_sample_pair c SET tenant_id = p.tenant_id FROM qams.lot_comparison_study p WHERE p.id = c.study_id;
ALTER TABLE qams.lot_sample_pair ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.maintenance_record c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
ALTER TABLE qams.maintenance_record ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.measurement_pair c SET tenant_id = p.tenant_id FROM qams.method_comparison_study p WHERE p.id = c.study_id;
ALTER TABLE qams.measurement_pair ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.mitigation_action c SET tenant_id = p.tenant_id FROM qams.risk_item p WHERE p.id = c.risk_id;
ALTER TABLE qams.mitigation_action ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.objective_progress c SET tenant_id = p.tenant_id FROM qams.quality_objective p WHERE p.id = c.objective_id;
ALTER TABLE qams.objective_progress ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.outlier_point c SET tenant_id = p.tenant_id FROM qams.outlier_screening p WHERE p.id = c.screening_id;
ALTER TABLE qams.outlier_point ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.precision_measurement c SET tenant_id = p.tenant_id FROM qams.precision_study p WHERE p.id = c.study_id;
ALTER TABLE qams.precision_measurement ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.pt_plan_item c SET tenant_id = p.tenant_id FROM qams.pt_plan p WHERE p.id = c.plan_id;
ALTER TABLE qams.pt_plan_item ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.rca_record c SET tenant_id = p.tenant_id FROM qams.nonconformance p WHERE p.id = c.nc_id;
ALTER TABLE qams.rca_record ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.reference_sample c SET tenant_id = p.tenant_id FROM qams.reference_interval_study p WHERE p.id = c.study_id;
ALTER TABLE qams.reference_sample ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.review_decision c SET tenant_id = p.tenant_id FROM qams.management_review p WHERE p.id = c.review_id;
ALTER TABLE qams.review_decision ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.role_permission c SET tenant_id = p.tenant_id FROM qams.role p WHERE p.id = c.role_id;
ALTER TABLE qams.role_permission ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.supplier_certificate c SET tenant_id = p.tenant_id FROM qams.supplier p WHERE p.id = c.supplier_id;
ALTER TABLE qams.supplier_certificate ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.uncertainty_component c SET tenant_id = p.tenant_id FROM qams.uncertainty_budget p WHERE p.id = c.budget_id;
ALTER TABLE qams.uncertainty_component ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.user_branch_access c SET tenant_id = p.tenant_id FROM qams.user_account p WHERE p.id = c.user_id;
ALTER TABLE qams.user_branch_access ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.user_department_access c SET tenant_id = p.tenant_id FROM qams.user_account p WHERE p.id = c.user_id;
ALTER TABLE qams.user_department_access ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE qams.validation_replicate c SET tenant_id = p.tenant_id FROM qams.validation_study p WHERE p.id = c.study_id;
ALTER TABLE qams.validation_replicate ALTER COLUMN tenant_id DROP DEFAULT;

-- 2) One UNIQUE (id, tenant_id) per parent, so the composite FKs have a target.
ALTER TABLE qams.audit ADD CONSTRAINT ux_audit_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.carryover_study ADD CONSTRAINT ux_carryover_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.competency_record ADD CONSTRAINT ux_competency_record_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.controlled_document ADD CONSTRAINT ux_controlled_document_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.detection_limit_study ADD CONSTRAINT ux_detection_limit_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.equipment_item ADD CONSTRAINT ux_equipment_item_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.instrument_comparability_study ADD CONSTRAINT ux_instrument_comparability_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.interference_study ADD CONSTRAINT ux_interference_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.linearity_study ADD CONSTRAINT ux_linearity_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.lot_comparison_study ADD CONSTRAINT ux_lot_comparison_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.management_review ADD CONSTRAINT ux_management_review_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.method_comparison_study ADD CONSTRAINT ux_method_comparison_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.monitoring_point ADD CONSTRAINT ux_monitoring_point_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.nonconformance ADD CONSTRAINT ux_nonconformance_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.outlier_screening ADD CONSTRAINT ux_outlier_screening_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.precision_study ADD CONSTRAINT ux_precision_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.pt_plan ADD CONSTRAINT ux_pt_plan_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.quality_objective ADD CONSTRAINT ux_quality_objective_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ux_reference_interval_study_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.risk_item ADD CONSTRAINT ux_risk_item_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.role ADD CONSTRAINT ux_role_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.supplier ADD CONSTRAINT ux_supplier_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.uncertainty_budget ADD CONSTRAINT ux_uncertainty_budget_id_tenant UNIQUE (id, tenant_id);
ALTER TABLE qams.validation_study ADD CONSTRAINT ux_validation_study_id_tenant UNIQUE (id, tenant_id);

-- 3) Swap each single-column CASCADE FK for the tenant-composite one:
--    a child row with a tenant different from its parent's becomes impossible.
ALTER TABLE qams.assessment_result DROP CONSTRAINT fk_assessment_result_competency_record_competency_id;
ALTER TABLE qams.assessment_result ADD CONSTRAINT fk_assessment_result_competency_record_tenant FOREIGN KEY (competency_id, tenant_id)
  REFERENCES qams.competency_record (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT fk_audit_checklist_item_audit_audit_id;
ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT fk_audit_checklist_item_audit_tenant FOREIGN KEY (audit_id, tenant_id)
  REFERENCES qams.audit (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.audit_finding DROP CONSTRAINT fk_audit_finding_audit_audit_id;
ALTER TABLE qams.audit_finding ADD CONSTRAINT fk_audit_finding_audit_tenant FOREIGN KEY (audit_id, tenant_id)
  REFERENCES qams.audit (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.calibration_record DROP CONSTRAINT fk_calibration_record_equipment_item_equipment_id;
ALTER TABLE qams.calibration_record ADD CONSTRAINT fk_calibration_record_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
  REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.capa_action DROP CONSTRAINT fk_capa_action_nonconformance_nc_id;
ALTER TABLE qams.capa_action ADD CONSTRAINT fk_capa_action_nonconformance_tenant FOREIGN KEY (nc_id, tenant_id)
  REFERENCES qams.nonconformance (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.carryover_reading DROP CONSTRAINT fk_carryover_reading_carryover_study_study_id;
ALTER TABLE qams.carryover_reading ADD CONSTRAINT fk_carryover_reading_carryover_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.carryover_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.detection_measurement DROP CONSTRAINT fk_detection_measurement_detection_limit_study_study_id;
ALTER TABLE qams.detection_measurement ADD CONSTRAINT fk_detection_measurement_detection_limit_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.detection_limit_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.document_version DROP CONSTRAINT fk_document_version_controlled_document_document_id;
ALTER TABLE qams.document_version ADD CONSTRAINT fk_document_version_controlled_document_tenant FOREIGN KEY (document_id, tenant_id)
  REFERENCES qams.controlled_document (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.environmental_reading DROP CONSTRAINT fk_environmental_reading_monitoring_point_point_id;
ALTER TABLE qams.environmental_reading ADD CONSTRAINT fk_environmental_reading_monitoring_point_tenant FOREIGN KEY (point_id, tenant_id)
  REFERENCES qams.monitoring_point (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.instrument_reading DROP CONSTRAINT fk_instrument_reading_instrument_comparability_study_study_id;
ALTER TABLE qams.instrument_reading ADD CONSTRAINT fk_instrument_reading_instrument_comparability_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.instrument_comparability_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.interference_measurement DROP CONSTRAINT fk_interference_measurement_interference_study_study_id;
ALTER TABLE qams.interference_measurement ADD CONSTRAINT fk_interference_measurement_interference_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.interference_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.intermediate_check DROP CONSTRAINT fk_intermediate_check_equipment_item_equipment_id;
ALTER TABLE qams.intermediate_check ADD CONSTRAINT fk_intermediate_check_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
  REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.linearity_measurement DROP CONSTRAINT fk_linearity_measurement_linearity_study_study_id;
ALTER TABLE qams.linearity_measurement ADD CONSTRAINT fk_linearity_measurement_linearity_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.linearity_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.lot_sample_pair DROP CONSTRAINT fk_lot_sample_pair_lot_comparison_study_study_id;
ALTER TABLE qams.lot_sample_pair ADD CONSTRAINT fk_lot_sample_pair_lot_comparison_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.lot_comparison_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.maintenance_record DROP CONSTRAINT fk_maintenance_record_equipment_item_equipment_id;
ALTER TABLE qams.maintenance_record ADD CONSTRAINT fk_maintenance_record_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
  REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.measurement_pair DROP CONSTRAINT fk_measurement_pair_method_comparison_study_study_id;
ALTER TABLE qams.measurement_pair ADD CONSTRAINT fk_measurement_pair_method_comparison_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.method_comparison_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.mitigation_action DROP CONSTRAINT fk_mitigation_action_risk_item_risk_id;
ALTER TABLE qams.mitigation_action ADD CONSTRAINT fk_mitigation_action_risk_item_tenant FOREIGN KEY (risk_id, tenant_id)
  REFERENCES qams.risk_item (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.objective_progress DROP CONSTRAINT fk_objective_progress_quality_objective_objective_id;
ALTER TABLE qams.objective_progress ADD CONSTRAINT fk_objective_progress_quality_objective_tenant FOREIGN KEY (objective_id, tenant_id)
  REFERENCES qams.quality_objective (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.outlier_point DROP CONSTRAINT fk_outlier_point_outlier_screening_screening_id;
ALTER TABLE qams.outlier_point ADD CONSTRAINT fk_outlier_point_outlier_screening_tenant FOREIGN KEY (screening_id, tenant_id)
  REFERENCES qams.outlier_screening (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.precision_measurement DROP CONSTRAINT fk_precision_measurement_precision_study_study_id;
ALTER TABLE qams.precision_measurement ADD CONSTRAINT fk_precision_measurement_precision_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.precision_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.pt_plan_item DROP CONSTRAINT fk_pt_plan_item_pt_plan_plan_id;
ALTER TABLE qams.pt_plan_item ADD CONSTRAINT fk_pt_plan_item_pt_plan_tenant FOREIGN KEY (plan_id, tenant_id)
  REFERENCES qams.pt_plan (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.rca_record DROP CONSTRAINT fk_rca_record_nonconformance_nc_id;
ALTER TABLE qams.rca_record ADD CONSTRAINT fk_rca_record_nonconformance_tenant FOREIGN KEY (nc_id, tenant_id)
  REFERENCES qams.nonconformance (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.reference_sample DROP CONSTRAINT fk_reference_sample_reference_interval_study_study_id;
ALTER TABLE qams.reference_sample ADD CONSTRAINT fk_reference_sample_reference_interval_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.reference_interval_study (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.review_decision DROP CONSTRAINT fk_review_decision_management_review_review_id;
ALTER TABLE qams.review_decision ADD CONSTRAINT fk_review_decision_management_review_tenant FOREIGN KEY (review_id, tenant_id)
  REFERENCES qams.management_review (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.role_permission DROP CONSTRAINT fk_role_permission_role_role_id;
ALTER TABLE qams.role_permission ADD CONSTRAINT fk_role_permission_role_tenant FOREIGN KEY (role_id, tenant_id)
  REFERENCES qams.role (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.supplier_certificate DROP CONSTRAINT fk_supplier_certificate_supplier_supplier_id;
ALTER TABLE qams.supplier_certificate ADD CONSTRAINT fk_supplier_certificate_supplier_tenant FOREIGN KEY (supplier_id, tenant_id)
  REFERENCES qams.supplier (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.uncertainty_component DROP CONSTRAINT fk_uncertainty_component_uncertainty_budget_budget_id;
ALTER TABLE qams.uncertainty_component ADD CONSTRAINT fk_uncertainty_component_uncertainty_budget_tenant FOREIGN KEY (budget_id, tenant_id)
  REFERENCES qams.uncertainty_budget (id, tenant_id) ON DELETE CASCADE;
ALTER TABLE qams.validation_replicate DROP CONSTRAINT fk_validation_replicate_validation_study_study_id;
ALTER TABLE qams.validation_replicate ADD CONSTRAINT fk_validation_replicate_validation_study_tenant FOREIGN KEY (study_id, tenant_id)
  REFERENCES qams.validation_study (id, tenant_id) ON DELETE CASCADE;

-- 4) RLS on every child - the CASCADE FK never isolated reads; this does.
ALTER TABLE qams.assessment_result ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.assessment_result FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.assessment_result;
CREATE POLICY tenant_isolation ON qams.assessment_result
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.audit_checklist_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.audit_checklist_item FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.audit_checklist_item;
CREATE POLICY tenant_isolation ON qams.audit_checklist_item
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.audit_finding ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.audit_finding FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.audit_finding;
CREATE POLICY tenant_isolation ON qams.audit_finding
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.calibration_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.calibration_record FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.calibration_record;
CREATE POLICY tenant_isolation ON qams.calibration_record
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.capa_action ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.capa_action FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.capa_action;
CREATE POLICY tenant_isolation ON qams.capa_action
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.carryover_reading ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.carryover_reading FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.carryover_reading;
CREATE POLICY tenant_isolation ON qams.carryover_reading
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.detection_measurement ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.detection_measurement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.detection_measurement;
CREATE POLICY tenant_isolation ON qams.detection_measurement
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.document_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.document_version FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.document_version;
CREATE POLICY tenant_isolation ON qams.document_version
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.environmental_reading ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.environmental_reading FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.environmental_reading;
CREATE POLICY tenant_isolation ON qams.environmental_reading
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.instrument_reading ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.instrument_reading FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.instrument_reading;
CREATE POLICY tenant_isolation ON qams.instrument_reading
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.interference_measurement ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.interference_measurement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.interference_measurement;
CREATE POLICY tenant_isolation ON qams.interference_measurement
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.intermediate_check ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.intermediate_check FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.intermediate_check;
CREATE POLICY tenant_isolation ON qams.intermediate_check
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.linearity_measurement ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.linearity_measurement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.linearity_measurement;
CREATE POLICY tenant_isolation ON qams.linearity_measurement
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.lot_sample_pair ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.lot_sample_pair FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.lot_sample_pair;
CREATE POLICY tenant_isolation ON qams.lot_sample_pair
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.maintenance_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.maintenance_record FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.maintenance_record;
CREATE POLICY tenant_isolation ON qams.maintenance_record
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.measurement_pair ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.measurement_pair FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.measurement_pair;
CREATE POLICY tenant_isolation ON qams.measurement_pair
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.mitigation_action ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.mitigation_action FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.mitigation_action;
CREATE POLICY tenant_isolation ON qams.mitigation_action
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.objective_progress ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.objective_progress FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.objective_progress;
CREATE POLICY tenant_isolation ON qams.objective_progress
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.outlier_point ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.outlier_point FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.outlier_point;
CREATE POLICY tenant_isolation ON qams.outlier_point
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.precision_measurement ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.precision_measurement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.precision_measurement;
CREATE POLICY tenant_isolation ON qams.precision_measurement
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.pt_plan_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.pt_plan_item FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.pt_plan_item;
CREATE POLICY tenant_isolation ON qams.pt_plan_item
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.rca_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.rca_record FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.rca_record;
CREATE POLICY tenant_isolation ON qams.rca_record
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.reference_sample ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.reference_sample FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.reference_sample;
CREATE POLICY tenant_isolation ON qams.reference_sample
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.review_decision ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.review_decision FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.review_decision;
CREATE POLICY tenant_isolation ON qams.review_decision
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.role_permission ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.role_permission FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.role_permission;
CREATE POLICY tenant_isolation ON qams.role_permission
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.supplier_certificate ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.supplier_certificate FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.supplier_certificate;
CREATE POLICY tenant_isolation ON qams.supplier_certificate
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.uncertainty_component ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.uncertainty_component FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.uncertainty_component;
CREATE POLICY tenant_isolation ON qams.uncertainty_component
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.user_branch_access ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.user_branch_access FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.user_branch_access;
CREATE POLICY tenant_isolation ON qams.user_branch_access
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.user_department_access ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.user_department_access FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.user_department_access;
CREATE POLICY tenant_isolation ON qams.user_department_access
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
ALTER TABLE qams.validation_replicate ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.validation_replicate FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.validation_replicate;
CREATE POLICY tenant_isolation ON qams.validation_replicate
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');

-- 5) Tenant FKs on the five elevated-writer tables (plan 4.5) - RESTRICT, never CASCADE.
ALTER TABLE qams.outbox_event ADD CONSTRAINT fk_outbox_event_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
ALTER TABLE qams.outbox_event VALIDATE CONSTRAINT fk_outbox_event_tenant;
ALTER TABLE qams.ref_counter ADD CONSTRAINT fk_ref_counter_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
ALTER TABLE qams.ref_counter VALIDATE CONSTRAINT fk_ref_counter_tenant;
ALTER TABLE read.kpi_snapshot ADD CONSTRAINT fk_kpi_snapshot_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
ALTER TABLE read.kpi_snapshot VALIDATE CONSTRAINT fk_kpi_snapshot_tenant;
ALTER TABLE qams.branch ADD CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
ALTER TABLE qams.branch VALIDATE CONSTRAINT fk_branch_tenant;
ALTER TABLE qams.user_account ADD CONSTRAINT fk_user_account_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
ALTER TABLE qams.user_account VALIDATE CONSTRAINT fk_user_account_tenant;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
-- Same reason as Up: FORCE ROW LEVEL SECURITY applies to referential-integrity
-- checks too, so re-adding the single-column FKs needs the parent rows visible
-- to this tenant-less migration session. Transaction-local.
SELECT set_config('app.bypass_rls', 'on', true);

ALTER TABLE qams.outbox_event DROP CONSTRAINT fk_outbox_event_tenant;
ALTER TABLE qams.ref_counter DROP CONSTRAINT fk_ref_counter_tenant;
ALTER TABLE read.kpi_snapshot DROP CONSTRAINT fk_kpi_snapshot_tenant;
ALTER TABLE qams.branch DROP CONSTRAINT fk_branch_tenant;
ALTER TABLE qams.user_account DROP CONSTRAINT fk_user_account_tenant;

DROP POLICY IF EXISTS tenant_isolation ON qams.assessment_result;
ALTER TABLE qams.assessment_result NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.assessment_result DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.audit_checklist_item;
ALTER TABLE qams.audit_checklist_item NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.audit_checklist_item DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.audit_finding;
ALTER TABLE qams.audit_finding NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.audit_finding DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.calibration_record;
ALTER TABLE qams.calibration_record NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.calibration_record DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.capa_action;
ALTER TABLE qams.capa_action NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.capa_action DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.carryover_reading;
ALTER TABLE qams.carryover_reading NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.carryover_reading DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.detection_measurement;
ALTER TABLE qams.detection_measurement NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.detection_measurement DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.document_version;
ALTER TABLE qams.document_version NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.document_version DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.environmental_reading;
ALTER TABLE qams.environmental_reading NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.environmental_reading DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.instrument_reading;
ALTER TABLE qams.instrument_reading NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.instrument_reading DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.interference_measurement;
ALTER TABLE qams.interference_measurement NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.interference_measurement DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.intermediate_check;
ALTER TABLE qams.intermediate_check NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.intermediate_check DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.linearity_measurement;
ALTER TABLE qams.linearity_measurement NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.linearity_measurement DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.lot_sample_pair;
ALTER TABLE qams.lot_sample_pair NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.lot_sample_pair DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.maintenance_record;
ALTER TABLE qams.maintenance_record NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.maintenance_record DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.measurement_pair;
ALTER TABLE qams.measurement_pair NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.measurement_pair DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.mitigation_action;
ALTER TABLE qams.mitigation_action NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.mitigation_action DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.objective_progress;
ALTER TABLE qams.objective_progress NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.objective_progress DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.outlier_point;
ALTER TABLE qams.outlier_point NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.outlier_point DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.precision_measurement;
ALTER TABLE qams.precision_measurement NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.precision_measurement DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.pt_plan_item;
ALTER TABLE qams.pt_plan_item NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.pt_plan_item DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.rca_record;
ALTER TABLE qams.rca_record NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.rca_record DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.reference_sample;
ALTER TABLE qams.reference_sample NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.reference_sample DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.review_decision;
ALTER TABLE qams.review_decision NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.review_decision DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.role_permission;
ALTER TABLE qams.role_permission NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.role_permission DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.supplier_certificate;
ALTER TABLE qams.supplier_certificate NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.supplier_certificate DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.uncertainty_component;
ALTER TABLE qams.uncertainty_component NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.uncertainty_component DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.user_branch_access;
ALTER TABLE qams.user_branch_access NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.user_branch_access DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.user_department_access;
ALTER TABLE qams.user_department_access NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.user_department_access DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.validation_replicate;
ALTER TABLE qams.validation_replicate NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.validation_replicate DISABLE ROW LEVEL SECURITY;

ALTER TABLE qams.assessment_result DROP CONSTRAINT fk_assessment_result_competency_record_tenant;
ALTER TABLE qams.assessment_result ADD CONSTRAINT fk_assessment_result_competency_record_competency_id FOREIGN KEY (competency_id)
  REFERENCES qams.competency_record (id) ON DELETE CASCADE;
ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT fk_audit_checklist_item_audit_tenant;
ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT fk_audit_checklist_item_audit_audit_id FOREIGN KEY (audit_id)
  REFERENCES qams.audit (id) ON DELETE CASCADE;
ALTER TABLE qams.audit_finding DROP CONSTRAINT fk_audit_finding_audit_tenant;
ALTER TABLE qams.audit_finding ADD CONSTRAINT fk_audit_finding_audit_audit_id FOREIGN KEY (audit_id)
  REFERENCES qams.audit (id) ON DELETE CASCADE;
ALTER TABLE qams.calibration_record DROP CONSTRAINT fk_calibration_record_equipment_item_tenant;
ALTER TABLE qams.calibration_record ADD CONSTRAINT fk_calibration_record_equipment_item_equipment_id FOREIGN KEY (equipment_id)
  REFERENCES qams.equipment_item (id) ON DELETE CASCADE;
ALTER TABLE qams.capa_action DROP CONSTRAINT fk_capa_action_nonconformance_tenant;
ALTER TABLE qams.capa_action ADD CONSTRAINT fk_capa_action_nonconformance_nc_id FOREIGN KEY (nc_id)
  REFERENCES qams.nonconformance (id) ON DELETE CASCADE;
ALTER TABLE qams.carryover_reading DROP CONSTRAINT fk_carryover_reading_carryover_study_tenant;
ALTER TABLE qams.carryover_reading ADD CONSTRAINT fk_carryover_reading_carryover_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.carryover_study (id) ON DELETE CASCADE;
ALTER TABLE qams.detection_measurement DROP CONSTRAINT fk_detection_measurement_detection_limit_study_tenant;
ALTER TABLE qams.detection_measurement ADD CONSTRAINT fk_detection_measurement_detection_limit_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.detection_limit_study (id) ON DELETE CASCADE;
ALTER TABLE qams.document_version DROP CONSTRAINT fk_document_version_controlled_document_tenant;
ALTER TABLE qams.document_version ADD CONSTRAINT fk_document_version_controlled_document_document_id FOREIGN KEY (document_id)
  REFERENCES qams.controlled_document (id) ON DELETE CASCADE;
ALTER TABLE qams.environmental_reading DROP CONSTRAINT fk_environmental_reading_monitoring_point_tenant;
ALTER TABLE qams.environmental_reading ADD CONSTRAINT fk_environmental_reading_monitoring_point_point_id FOREIGN KEY (point_id)
  REFERENCES qams.monitoring_point (id) ON DELETE CASCADE;
ALTER TABLE qams.instrument_reading DROP CONSTRAINT fk_instrument_reading_instrument_comparability_study_tenant;
ALTER TABLE qams.instrument_reading ADD CONSTRAINT fk_instrument_reading_instrument_comparability_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.instrument_comparability_study (id) ON DELETE CASCADE;
ALTER TABLE qams.interference_measurement DROP CONSTRAINT fk_interference_measurement_interference_study_tenant;
ALTER TABLE qams.interference_measurement ADD CONSTRAINT fk_interference_measurement_interference_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.interference_study (id) ON DELETE CASCADE;
ALTER TABLE qams.intermediate_check DROP CONSTRAINT fk_intermediate_check_equipment_item_tenant;
ALTER TABLE qams.intermediate_check ADD CONSTRAINT fk_intermediate_check_equipment_item_equipment_id FOREIGN KEY (equipment_id)
  REFERENCES qams.equipment_item (id) ON DELETE CASCADE;
ALTER TABLE qams.linearity_measurement DROP CONSTRAINT fk_linearity_measurement_linearity_study_tenant;
ALTER TABLE qams.linearity_measurement ADD CONSTRAINT fk_linearity_measurement_linearity_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.linearity_study (id) ON DELETE CASCADE;
ALTER TABLE qams.lot_sample_pair DROP CONSTRAINT fk_lot_sample_pair_lot_comparison_study_tenant;
ALTER TABLE qams.lot_sample_pair ADD CONSTRAINT fk_lot_sample_pair_lot_comparison_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.lot_comparison_study (id) ON DELETE CASCADE;
ALTER TABLE qams.maintenance_record DROP CONSTRAINT fk_maintenance_record_equipment_item_tenant;
ALTER TABLE qams.maintenance_record ADD CONSTRAINT fk_maintenance_record_equipment_item_equipment_id FOREIGN KEY (equipment_id)
  REFERENCES qams.equipment_item (id) ON DELETE CASCADE;
ALTER TABLE qams.measurement_pair DROP CONSTRAINT fk_measurement_pair_method_comparison_study_tenant;
ALTER TABLE qams.measurement_pair ADD CONSTRAINT fk_measurement_pair_method_comparison_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.method_comparison_study (id) ON DELETE CASCADE;
ALTER TABLE qams.mitigation_action DROP CONSTRAINT fk_mitigation_action_risk_item_tenant;
ALTER TABLE qams.mitigation_action ADD CONSTRAINT fk_mitigation_action_risk_item_risk_id FOREIGN KEY (risk_id)
  REFERENCES qams.risk_item (id) ON DELETE CASCADE;
ALTER TABLE qams.objective_progress DROP CONSTRAINT fk_objective_progress_quality_objective_tenant;
ALTER TABLE qams.objective_progress ADD CONSTRAINT fk_objective_progress_quality_objective_objective_id FOREIGN KEY (objective_id)
  REFERENCES qams.quality_objective (id) ON DELETE CASCADE;
ALTER TABLE qams.outlier_point DROP CONSTRAINT fk_outlier_point_outlier_screening_tenant;
ALTER TABLE qams.outlier_point ADD CONSTRAINT fk_outlier_point_outlier_screening_screening_id FOREIGN KEY (screening_id)
  REFERENCES qams.outlier_screening (id) ON DELETE CASCADE;
ALTER TABLE qams.precision_measurement DROP CONSTRAINT fk_precision_measurement_precision_study_tenant;
ALTER TABLE qams.precision_measurement ADD CONSTRAINT fk_precision_measurement_precision_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.precision_study (id) ON DELETE CASCADE;
ALTER TABLE qams.pt_plan_item DROP CONSTRAINT fk_pt_plan_item_pt_plan_tenant;
ALTER TABLE qams.pt_plan_item ADD CONSTRAINT fk_pt_plan_item_pt_plan_plan_id FOREIGN KEY (plan_id)
  REFERENCES qams.pt_plan (id) ON DELETE CASCADE;
ALTER TABLE qams.rca_record DROP CONSTRAINT fk_rca_record_nonconformance_tenant;
ALTER TABLE qams.rca_record ADD CONSTRAINT fk_rca_record_nonconformance_nc_id FOREIGN KEY (nc_id)
  REFERENCES qams.nonconformance (id) ON DELETE CASCADE;
ALTER TABLE qams.reference_sample DROP CONSTRAINT fk_reference_sample_reference_interval_study_tenant;
ALTER TABLE qams.reference_sample ADD CONSTRAINT fk_reference_sample_reference_interval_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.reference_interval_study (id) ON DELETE CASCADE;
ALTER TABLE qams.review_decision DROP CONSTRAINT fk_review_decision_management_review_tenant;
ALTER TABLE qams.review_decision ADD CONSTRAINT fk_review_decision_management_review_review_id FOREIGN KEY (review_id)
  REFERENCES qams.management_review (id) ON DELETE CASCADE;
ALTER TABLE qams.role_permission DROP CONSTRAINT fk_role_permission_role_tenant;
ALTER TABLE qams.role_permission ADD CONSTRAINT fk_role_permission_role_role_id FOREIGN KEY (role_id)
  REFERENCES qams.role (id) ON DELETE CASCADE;
ALTER TABLE qams.supplier_certificate DROP CONSTRAINT fk_supplier_certificate_supplier_tenant;
ALTER TABLE qams.supplier_certificate ADD CONSTRAINT fk_supplier_certificate_supplier_supplier_id FOREIGN KEY (supplier_id)
  REFERENCES qams.supplier (id) ON DELETE CASCADE;
ALTER TABLE qams.uncertainty_component DROP CONSTRAINT fk_uncertainty_component_uncertainty_budget_tenant;
ALTER TABLE qams.uncertainty_component ADD CONSTRAINT fk_uncertainty_component_uncertainty_budget_budget_id FOREIGN KEY (budget_id)
  REFERENCES qams.uncertainty_budget (id) ON DELETE CASCADE;
ALTER TABLE qams.validation_replicate DROP CONSTRAINT fk_validation_replicate_validation_study_tenant;
ALTER TABLE qams.validation_replicate ADD CONSTRAINT fk_validation_replicate_validation_study_study_id FOREIGN KEY (study_id)
  REFERENCES qams.validation_study (id) ON DELETE CASCADE;

ALTER TABLE qams.audit DROP CONSTRAINT ux_audit_id_tenant;
ALTER TABLE qams.carryover_study DROP CONSTRAINT ux_carryover_study_id_tenant;
ALTER TABLE qams.competency_record DROP CONSTRAINT ux_competency_record_id_tenant;
ALTER TABLE qams.controlled_document DROP CONSTRAINT ux_controlled_document_id_tenant;
ALTER TABLE qams.detection_limit_study DROP CONSTRAINT ux_detection_limit_study_id_tenant;
ALTER TABLE qams.equipment_item DROP CONSTRAINT ux_equipment_item_id_tenant;
ALTER TABLE qams.instrument_comparability_study DROP CONSTRAINT ux_instrument_comparability_study_id_tenant;
ALTER TABLE qams.interference_study DROP CONSTRAINT ux_interference_study_id_tenant;
ALTER TABLE qams.linearity_study DROP CONSTRAINT ux_linearity_study_id_tenant;
ALTER TABLE qams.lot_comparison_study DROP CONSTRAINT ux_lot_comparison_study_id_tenant;
ALTER TABLE qams.management_review DROP CONSTRAINT ux_management_review_id_tenant;
ALTER TABLE qams.method_comparison_study DROP CONSTRAINT ux_method_comparison_study_id_tenant;
ALTER TABLE qams.monitoring_point DROP CONSTRAINT ux_monitoring_point_id_tenant;
ALTER TABLE qams.nonconformance DROP CONSTRAINT ux_nonconformance_id_tenant;
ALTER TABLE qams.outlier_screening DROP CONSTRAINT ux_outlier_screening_id_tenant;
ALTER TABLE qams.precision_study DROP CONSTRAINT ux_precision_study_id_tenant;
ALTER TABLE qams.pt_plan DROP CONSTRAINT ux_pt_plan_id_tenant;
ALTER TABLE qams.quality_objective DROP CONSTRAINT ux_quality_objective_id_tenant;
ALTER TABLE qams.reference_interval_study DROP CONSTRAINT ux_reference_interval_study_id_tenant;
ALTER TABLE qams.risk_item DROP CONSTRAINT ux_risk_item_id_tenant;
ALTER TABLE qams.role DROP CONSTRAINT ux_role_id_tenant;
ALTER TABLE qams.supplier DROP CONSTRAINT ux_supplier_id_tenant;
ALTER TABLE qams.uncertainty_budget DROP CONSTRAINT ux_uncertainty_budget_id_tenant;
ALTER TABLE qams.validation_study DROP CONSTRAINT ux_validation_study_id_tenant;
""");
            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "validation_replicate");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "user_department_access");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "user_branch_access");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "uncertainty_component");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "supplier_certificate");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "role_permission");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "review_decision");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "reference_sample");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "rca_record");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "pt_plan_item");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "precision_measurement");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "outlier_point");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "objective_progress");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "mitigation_action");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "measurement_pair");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "maintenance_record");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "lot_sample_pair");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "linearity_measurement");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "intermediate_check");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "interference_measurement");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "instrument_reading");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "environmental_reading");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "document_version");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "detection_measurement");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "carryover_reading");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "capa_action");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "calibration_record");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "audit_finding");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "audit_checklist_item");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "qams",
                table: "assessment_result");
        }
    }
}
