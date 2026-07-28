using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// EA remediation Phase 5, DB-005: the database as the LAST line of
    /// defense. CHECK constraints on regulated tables reject what the domain
    /// layer should never send — enum values outside the domain, scores
    /// outside 1–5, negative quantities, and completion timestamps that
    /// precede creation — so a bug (or a direct-SQL actor) cannot corrupt the
    /// quality record. Raw SQL by design: EF's model has no vocabulary for
    /// these invariants.
    /// </summary>
    public partial class Phase5CheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nonconformance — the central regulated record.
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance
                    ADD CONSTRAINT ck_nonconformance_severity_range CHECK (severity BETWEEN 1 AND 5),
                    ADD CONSTRAINT ck_nonconformance_likelihood_range CHECK (likelihood BETWEEN 1 AND 5),
                    ADD CONSTRAINT ck_nonconformance_rpn_range CHECK (rpn BETWEEN 1 AND 25),
                    ADD CONSTRAINT ck_nonconformance_status_domain CHECK (status IN
                        ('Draft','Raised','Assigned','Rca','ActionPlan','PendingVerification',
                         'EffectivenessCheck','Closed','Rejected'));
                """);

            // Risk register — explicit 1–5 assessments, no out-of-scale scores.
            migrationBuilder.Sql("""
                ALTER TABLE qams.risk_item
                    ADD CONSTRAINT ck_risk_item_likelihood_range CHECK (likelihood BETWEEN 1 AND 5),
                    ADD CONSTRAINT ck_risk_item_impact_range CHECK (impact BETWEEN 1 AND 5),
                    ADD CONSTRAINT ck_risk_item_rpn_range CHECK (rpn BETWEEN 1 AND 25),
                    ADD CONSTRAINT ck_risk_item_residual_ranges CHECK (
                        (residual_likelihood IS NULL OR residual_likelihood BETWEEN 1 AND 5) AND
                        (residual_impact IS NULL OR residual_impact BETWEEN 1 AND 5) AND
                        (residual_rpn IS NULL OR residual_rpn BETWEEN 1 AND 25));
                """);

            // Equipment — calibration cadence must be a real schedule.
            migrationBuilder.Sql("""
                ALTER TABLE qams.equipment_item
                    ADD CONSTRAINT ck_equipment_interval_positive CHECK (calibration_interval_days > 0),
                    ADD CONSTRAINT ck_equipment_grace_nonnegative CHECK (grace_period_days >= 0);
                """);

            // Supplier evaluation — a weighted score can never be negative.
            migrationBuilder.Sql("""
                ALTER TABLE qams.supplier_evaluation
                    ADD CONSTRAINT ck_supplier_evaluation_score_nonnegative CHECK (weighted_total >= 0);
                """);

            // Date-ordering invariants — completion can never precede creation.
            migrationBuilder.Sql("""
                ALTER TABLE qams.work_task
                    ADD CONSTRAINT ck_work_task_completion_order CHECK
                        (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc);
                ALTER TABLE qams.training_assignment
                    ADD CONSTRAINT ck_training_completion_order CHECK
                        (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc);
                ALTER TABLE qams.audit
                    ADD CONSTRAINT ck_audit_signoff_order CHECK
                        (signed_off_at_utc IS NULL OR signed_off_at_utc >= created_at_utc);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE qams.nonconformance
                    DROP CONSTRAINT IF EXISTS ck_nonconformance_severity_range,
                    DROP CONSTRAINT IF EXISTS ck_nonconformance_likelihood_range,
                    DROP CONSTRAINT IF EXISTS ck_nonconformance_rpn_range,
                    DROP CONSTRAINT IF EXISTS ck_nonconformance_status_domain;
                ALTER TABLE qams.risk_item
                    DROP CONSTRAINT IF EXISTS ck_risk_item_likelihood_range,
                    DROP CONSTRAINT IF EXISTS ck_risk_item_impact_range,
                    DROP CONSTRAINT IF EXISTS ck_risk_item_rpn_range,
                    DROP CONSTRAINT IF EXISTS ck_risk_item_residual_ranges;
                ALTER TABLE qams.equipment_item
                    DROP CONSTRAINT IF EXISTS ck_equipment_interval_positive,
                    DROP CONSTRAINT IF EXISTS ck_equipment_grace_nonnegative;
                ALTER TABLE qams.supplier_evaluation
                    DROP CONSTRAINT IF EXISTS ck_supplier_evaluation_score_nonnegative;
                ALTER TABLE qams.work_task DROP CONSTRAINT IF EXISTS ck_work_task_completion_order;
                ALTER TABLE qams.training_assignment DROP CONSTRAINT IF EXISTS ck_training_completion_order;
                ALTER TABLE qams.audit DROP CONSTRAINT IF EXISTS ck_audit_signoff_order;
                """);
        }
    }
}
