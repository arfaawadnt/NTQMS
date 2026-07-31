-- Pre-flight: rows whose enum-backed column value is outside the C# enum member set.
-- Generated from the C# enums (source of truth) on 2026-07-31. Empty result = safe to add CHECKs.
SELECT set_config('app.bypass_rls','on',false);
SELECT 'qams.validation_study.state' AS col, state::text AS bad, count(*) FROM qams.validation_study WHERE state IS NOT NULL AND state NOT IN ('ProtocolConfigured','DataEntered','StatsCalculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.pt_enrollment.performance' AS col, performance::text AS bad, count(*) FROM qams.pt_enrollment WHERE performance IS NOT NULL AND performance NOT IN ('Pending','Satisfactory','Questionable','Unsatisfactory') GROUP BY 2
UNION ALL
SELECT 'qams.uncertainty_budget.status' AS col, status::text AS bad, count(*) FROM qams.uncertainty_budget WHERE status IS NOT NULL AND status NOT IN ('Draft','Calculated','Approved') GROUP BY 2
UNION ALL
SELECT 'qams.uncertainty_component.type' AS col, type::text AS bad, count(*) FROM qams.uncertainty_component WHERE type IS NOT NULL AND type NOT IN ('TypeA','TypeB') GROUP BY 2
UNION ALL
SELECT 'qams.pt_plan.status' AS col, status::text AS bad, count(*) FROM qams.pt_plan WHERE status IS NOT NULL AND status NOT IN ('Draft','Approved','Closed') GROUP BY 2
UNION ALL
SELECT 'qams.method_comparison_study.state' AS col, state::text AS bad, count(*) FROM qams.method_comparison_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.linearity_study.state' AS col, state::text AS bad, count(*) FROM qams.linearity_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.detection_limit_study.state' AS col, state::text AS bad, count(*) FROM qams.detection_limit_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.detection_measurement.kind' AS col, kind::text AS bad, count(*) FROM qams.detection_measurement WHERE kind IS NOT NULL AND kind NOT IN ('Blank','LowLevel') GROUP BY 2
UNION ALL
SELECT 'qams.reference_interval_study.state' AS col, state::text AS bad, count(*) FROM qams.reference_interval_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.reference_interval_study.verdict' AS col, verdict::text AS bad, count(*) FROM qams.reference_interval_study WHERE verdict IS NOT NULL AND verdict NOT IN ('Verified','Rejected') GROUP BY 2
UNION ALL
SELECT 'qams.sigma_assessment.state' AS col, state::text AS bad, count(*) FROM qams.sigma_assessment WHERE state IS NOT NULL AND state NOT IN ('Draft','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.sigma_assessment.grade' AS col, grade::text AS bad, count(*) FROM qams.sigma_assessment WHERE grade IS NOT NULL AND grade NOT IN ('Unacceptable','Marginal','Good','Excellent','WorldClass') GROUP BY 2
UNION ALL
SELECT 'qams.precision_study.state' AS col, state::text AS bad, count(*) FROM qams.precision_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.outlier_screening.state' AS col, state::text AS bad, count(*) FROM qams.outlier_screening WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.carryover_study.state' AS col, state::text AS bad, count(*) FROM qams.carryover_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.carryover_reading.kind' AS col, kind::text AS bad, count(*) FROM qams.carryover_reading WHERE kind IS NOT NULL AND kind NOT IN ('High','Low') GROUP BY 2
UNION ALL
SELECT 'qams.lot_comparison_study.state' AS col, state::text AS bad, count(*) FROM qams.lot_comparison_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.interference_study.state' AS col, state::text AS bad, count(*) FROM qams.interference_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.instrument_comparability_study.state' AS col, state::text AS bad, count(*) FROM qams.instrument_comparability_study WHERE state IS NOT NULL AND state NOT IN ('DataEntry','Calculated','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.audit.type' AS col, type::text AS bad, count(*) FROM qams.audit WHERE type IS NOT NULL AND type NOT IN ('Internal','ExternalHosted') GROUP BY 2
UNION ALL
SELECT 'qams.audit.status' AS col, status::text AS bad, count(*) FROM qams.audit WHERE status IS NOT NULL AND status NOT IN ('Scheduled','InProgress','SignedOff') GROUP BY 2
UNION ALL
SELECT 'qams.audit_checklist_item.verdict' AS col, verdict::text AS bad, count(*) FROM qams.audit_checklist_item WHERE verdict IS NOT NULL AND verdict NOT IN ('Unanswered','Conform','Ofi','NonConform') GROUP BY 2
UNION ALL
SELECT 'qams.audit_finding.grade' AS col, grade::text AS bad, count(*) FROM qams.audit_finding WHERE grade IS NOT NULL AND grade NOT IN ('Ofi','MinorNc','MajorNc') GROUP BY 2
UNION ALL
SELECT 'qams.audit_trail_review.status' AS col, status::text AS bad, count(*) FROM qams.audit_trail_review WHERE status IS NOT NULL AND status NOT IN ('Open','Completed') GROUP BY 2
UNION ALL
SELECT 'qams.controlled_document.status' AS col, status::text AS bad, count(*) FROM qams.controlled_document WHERE status IS NOT NULL AND status NOT IN ('Draft','Published','Obsolete') GROUP BY 2
UNION ALL
SELECT 'qams.document_version.state' AS col, state::text AS bad, count(*) FROM qams.document_version WHERE state IS NOT NULL AND state NOT IN ('Draft','UnderReview','Approved','Published','Obsolete','Rejected') GROUP BY 2
UNION ALL
SELECT 'qams.document_controlled_copy.status' AS col, status::text AS bad, count(*) FROM qams.document_controlled_copy WHERE status IS NOT NULL AND status NOT IN ('Issued','Returned','Destroyed') GROUP BY 2
UNION ALL
SELECT 'qams.monitoring_point.status' AS col, status::text AS bad, count(*) FROM qams.monitoring_point WHERE status IS NOT NULL AND status NOT IN ('Active','Suspended','Retired') GROUP BY 2
UNION ALL
SELECT 'qams.risk_item.status' AS col, status::text AS bad, count(*) FROM qams.risk_item WHERE status IS NOT NULL AND status NOT IN ('Identified','Mitigating','Closed') GROUP BY 2
UNION ALL
SELECT 'qams.change_request.status' AS col, status::text AS bad, count(*) FROM qams.change_request WHERE status IS NOT NULL AND status NOT IN ('Proposed','Approved','Rejected','Closed','Reviewed') GROUP BY 2
UNION ALL
SELECT 'qams.management_review.status' AS col, status::text AS bad, count(*) FROM qams.management_review WHERE status IS NOT NULL AND status NOT IN ('Scheduled','Closed') GROUP BY 2
UNION ALL
SELECT 'qams.supplier.status' AS col, status::text AS bad, count(*) FROM qams.supplier WHERE status IS NOT NULL AND status NOT IN ('PendingEvaluation','Approved','Suspended') GROUP BY 2
UNION ALL
SELECT 'qams.conflict_declaration.status' AS col, status::text AS bad, count(*) FROM qams.conflict_declaration WHERE status IS NOT NULL AND status NOT IN ('Declared','Assessed','Closed') GROUP BY 2
UNION ALL
SELECT 'qams.conflict_declaration.risk_level' AS col, risk_level::text AS bad, count(*) FROM qams.conflict_declaration WHERE risk_level IS NOT NULL AND risk_level NOT IN ('Low','Medium','High') GROUP BY 2
UNION ALL
SELECT 'qams.conflict_declaration.outcome' AS col, outcome::text AS bad, count(*) FROM qams.conflict_declaration WHERE outcome IS NOT NULL AND outcome NOT IN ('Accepted','Mitigated','Withdrawn') GROUP BY 2
UNION ALL
SELECT 'qams.user_account.role' AS col, role::text AS bad, count(*) FROM qams.user_account WHERE role IS NOT NULL AND role NOT IN ('PlatformAdmin','TenantAdmin','QualityManager','DepartmentHead','Analyst','ExternalAuditor') GROUP BY 2
UNION ALL
SELECT 'qams.nonconformance.status' AS col, status::text AS bad, count(*) FROM qams.nonconformance WHERE status IS NOT NULL AND status NOT IN ('Draft','Raised','Assigned','Rca','ActionPlan','PendingVerification','EffectivenessCheck','Closed','Rejected') GROUP BY 2
UNION ALL
SELECT 'qams.nonconformance.source_type' AS col, source_type::text AS bad, count(*) FROM qams.nonconformance WHERE source_type IS NOT NULL AND source_type NOT IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest') GROUP BY 2
UNION ALL
SELECT 'qams.nonconformance.event_type' AS col, event_type::text AS bad, count(*) FROM qams.nonconformance WHERE event_type IS NOT NULL AND event_type NOT IN ('Nonconformity','Deviation','OutOfSpecification','OutOfTrend') GROUP BY 2
UNION ALL
SELECT 'qams.capa_action.type' AS col, type::text AS bad, count(*) FROM qams.capa_action WHERE type IS NOT NULL AND type NOT IN ('Corrective','Preventive') GROUP BY 2
UNION ALL
SELECT 'qams.capa_action.status' AS col, status::text AS bad, count(*) FROM qams.capa_action WHERE status IS NOT NULL AND status NOT IN ('Open','Completed') GROUP BY 2
UNION ALL
SELECT 'qams.rca_record.method' AS col, method::text AS bad, count(*) FROM qams.rca_record WHERE method IS NOT NULL AND method NOT IN ('FiveWhys','Fishbone','Other') GROUP BY 2
UNION ALL
SELECT 'qams.complaint.channel' AS col, channel::text AS bad, count(*) FROM qams.complaint WHERE channel IS NOT NULL AND channel NOT IN ('Phone','Email','Portal','InPerson','Letter') GROUP BY 2
UNION ALL
SELECT 'qams.complaint.status' AS col, status::text AS bad, count(*) FROM qams.complaint WHERE status IS NOT NULL AND status NOT IN ('Logged','Acknowledged','Validated','Investigating','OutcomeLogged','Resolved','Closed','Invalid') GROUP BY 2
UNION ALL
SELECT 'qams.quality_objective.direction' AS col, direction::text AS bad, count(*) FROM qams.quality_objective WHERE direction IS NOT NULL AND direction NOT IN ('AtLeast','AtMost') GROUP BY 2
UNION ALL
SELECT 'qams.quality_objective.status' AS col, status::text AS bad, count(*) FROM qams.quality_objective WHERE status IS NOT NULL AND status NOT IN ('Active','Achieved','Missed','Cancelled') GROUP BY 2
UNION ALL
SELECT 'qams.user_access_review.status' AS col, status::text AS bad, count(*) FROM qams.user_access_review WHERE status IS NOT NULL AND status NOT IN ('Open','Completed') GROUP BY 2
UNION ALL
SELECT 'qams.quality_policy.status' AS col, status::text AS bad, count(*) FROM qams.quality_policy WHERE status IS NOT NULL AND status NOT IN ('Draft','Active','Superseded') GROUP BY 2
UNION ALL
SELECT 'qams.feedback_entry.type' AS col, type::text AS bad, count(*) FROM qams.feedback_entry WHERE type IS NOT NULL AND type NOT IN ('Compliment','Suggestion','Dissatisfaction') GROUP BY 2
UNION ALL
SELECT 'qams.feedback_entry.status' AS col, status::text AS bad, count(*) FROM qams.feedback_entry WHERE status IS NOT NULL AND status NOT IN ('Logged','Reviewed','Closed','Escalated') GROUP BY 2
UNION ALL
SELECT 'qams.archive_entry.retention_class' AS col, retention_class::text AS bad, count(*) FROM qams.archive_entry WHERE retention_class IS NOT NULL AND retention_class NOT IN ('FiveYears','TenYears','Permanent') GROUP BY 2
UNION ALL
SELECT 'qams.archive_entry.state' AS col, state::text AS bad, count(*) FROM qams.archive_entry WHERE state IS NOT NULL AND state NOT IN ('Archived','Retrieved','Disposed') GROUP BY 2
UNION ALL
SELECT 'qams.work_task.status' AS col, status::text AS bad, count(*) FROM qams.work_task WHERE status IS NOT NULL AND status NOT IN ('Pending','Completed') GROUP BY 2
UNION ALL
SELECT 'qams.notification_dispatch.email_status' AS col, email_status::text AS bad, count(*) FROM qams.notification_dispatch WHERE email_status IS NOT NULL AND email_status NOT IN ('Queued','Sent','Failed') GROUP BY 2
UNION ALL
SELECT 'qams.interested_party.status' AS col, status::text AS bad, count(*) FROM qams.interested_party WHERE status IS NOT NULL AND status NOT IN ('Active','Archived') GROUP BY 2
UNION ALL
SELECT 'qams.context_issue.type' AS col, type::text AS bad, count(*) FROM qams.context_issue WHERE type IS NOT NULL AND type NOT IN ('Internal','External') GROUP BY 2
UNION ALL
SELECT 'qams.context_issue.status' AS col, status::text AS bad, count(*) FROM qams.context_issue WHERE status IS NOT NULL AND status NOT IN ('Active','Closed') GROUP BY 2
UNION ALL
SELECT 'qams.equipment_item.status' AS col, status::text AS bad, count(*) FROM qams.equipment_item WHERE status IS NOT NULL AND status NOT IN ('NeedsCalibration','Active','OutOfService','Retired') GROUP BY 2
UNION ALL
SELECT 'qams.reference_standard.type' AS col, type::text AS bad, count(*) FROM qams.reference_standard WHERE type IS NOT NULL AND type NOT IN ('CertifiedReferenceMaterial','ReferenceStandard','WorkingStandard') GROUP BY 2
UNION ALL
SELECT 'qams.reference_standard.status' AS col, status::text AS bad, count(*) FROM qams.reference_standard WHERE status IS NOT NULL AND status NOT IN ('Active','Quarantined','Expired','Retired') GROUP BY 2
UNION ALL
SELECT 'qams.test_authorization.scope' AS col, scope::text AS bad, count(*) FROM qams.test_authorization WHERE scope IS NOT NULL AND scope NOT IN ('Perform','ReviewAndRelease','Train') GROUP BY 2
UNION ALL
SELECT 'qams.test_authorization.status' AS col, status::text AS bad, count(*) FROM qams.test_authorization WHERE status IS NOT NULL AND status NOT IN ('Active','Suspended','Revoked','Expired') GROUP BY 2
UNION ALL
SELECT 'qams.competency_record.status' AS col, status::text AS bad, count(*) FROM qams.competency_record WHERE status IS NOT NULL AND status NOT IN ('PendingTraining','Evaluated','Authorized','Revoked') GROUP BY 2
UNION ALL
SELECT 'saas.tenant.status' AS col, status::text AS bad, count(*) FROM saas.tenant WHERE status IS NOT NULL AND status NOT IN ('Provisioning','Active','Suspended','Terminated') GROUP BY 2
UNION ALL
SELECT 'qams.qc_run.outcome' AS col, outcome::text AS bad, count(*) FROM qams.qc_run WHERE outcome IS NOT NULL AND outcome NOT IN ('InControl','Warning','OutOfControl') GROUP BY 2
UNION ALL
SELECT 'audit.field_change.action' AS col, action::text AS bad, count(*) FROM audit.field_change WHERE action IS NOT NULL AND action NOT IN ('Created','Deleted','Modified') GROUP BY 2;
