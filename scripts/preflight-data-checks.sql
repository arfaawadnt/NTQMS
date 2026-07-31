-- Pre-flight data validation for the schema-hardening migrations (read-only).
-- Run before each phase; every count must be 0. Executed 2026-07-31: all clean.
SELECT set_config('app.bypass_rls','on',false);

-- Phase 1.3: criteria_json must be valid JSON before the jsonb cast
SELECT 'json-invalid' AS chk, count(*) FROM qams.supplier_evaluation
WHERE criteria_json IS NOT NULL AND NOT (criteria_json IS JSON);

-- Phase 1.1: ip_address must be inet-parseable (also proves the column is empty today)
SELECT 'ip-nonnull' AS chk, count(*) FROM audit.security_event WHERE ip_address IS NOT NULL;
SELECT 'ip-unparseable' AS chk, count(*) FROM audit.security_event
WHERE ip_address IS NOT NULL
  AND NOT (ip_address ~ '^([0-9]{1,3}\.){3}[0-9]{1,3}(/[0-9]+)?$'
        OR ip_address ~ '^[0-9a-fA-F:]+(/[0-9]+)?$');

-- Phase 4: no orphaned owned-child rows (parent gone would break the tenant backfill).
-- The CASCADE FKs make orphans structurally impossible; this proves it anyway.
SELECT 'orphan-capa_action' AS chk, count(*) FROM qams.capa_action c
WHERE NOT EXISTS (SELECT 1 FROM qams.nonconformance p WHERE p.id = c.nc_id);
SELECT 'orphan-rca_record' AS chk, count(*) FROM qams.rca_record c
WHERE NOT EXISTS (SELECT 1 FROM qams.nonconformance p WHERE p.id = c.nc_id);
SELECT 'orphan-document_version' AS chk, count(*) FROM qams.document_version c
WHERE NOT EXISTS (SELECT 1 FROM qams.controlled_document p WHERE p.id = c.document_id);
SELECT 'orphan-role_permission' AS chk, count(*) FROM qams.role_permission c
WHERE NOT EXISTS (SELECT 1 FROM qams.role p WHERE p.id = c.role_id);

-- Phase 4 special case: scope rows may not belong to platform admins (tenant NULL),
-- or the NOT NULL tenant backfill on user_branch_access/user_department_access fails.
SELECT 'platform-admin-scope-rows' AS chk,
  (SELECT count(*) FROM qams.user_branch_access b
     JOIN qams.user_account u ON u.id = b.user_id WHERE u.tenant_id IS NULL)
+ (SELECT count(*) FROM qams.user_department_access d
     JOIN qams.user_account u ON u.id = d.user_id WHERE u.tenant_id IS NULL);
