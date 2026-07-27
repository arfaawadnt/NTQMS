-- =============================================================================
-- NT.QMS — Production database hardening: least-privilege runtime role
-- Remediation of audit finding F-02 (records protection) and the second half of
-- F-01 (RLS must not be bypassed by the table owner).
--
-- WHY: The application must NOT connect as the role that OWNS the schema/tables.
-- A table owner is exempt from Row-Level Security unless FORCE is set, and always
-- retains full DML. Running the app under a separate, least-privilege role means:
--   * RLS (already FORCED by migration ActivateForcedTenantRls) is enforced.
--   * The signed-record immutability triggers cannot be sidestepped by ownership.
--   * DELETE on regulated data is impossible for the runtime role.
--
-- MODEL:
--   qams_owner   — owns the schema/objects; runs migrations ONLY (DDL).
--   qams_app     — the application's runtime login; DML only, least privilege.
--
-- Run this ONCE per environment as a superuser (e.g. postgres), AFTER migrations
-- have created the schema and BEFORE pointing the app's connection string at
-- qams_app. Idempotent: safe to re-run.
-- =============================================================================

\set ON_ERROR_STOP on

-- 1. Roles -------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'qams_owner') THEN
        CREATE ROLE qams_owner LOGIN PASSWORD :'owner_password';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'qams_app') THEN
        CREATE ROLE qams_app LOGIN PASSWORD :'app_password';
    END IF;
END $$;

-- 2. Ownership of all objects belongs to qams_owner (run migrations as it) ----
--    If the schema was created by another role, reassign it:
--       REASSIGN OWNED BY <old_owner> TO qams_owner;

-- 3. Schema usage (no CREATE — the runtime never issues DDL) ------------------
GRANT USAGE ON SCHEMA qams, audit, ref, read, saas TO qams_app;

-- 4. Table privileges --------------------------------------------------------
--    Business data: read + write, but NEVER delete (regulated records are never
--    hard-deleted; state transitions and the immutability triggers govern them).
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA qams  TO qams_app;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA read  TO qams_app;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA saas  TO qams_app;
GRANT SELECT              ON ALL TABLES IN SCHEMA ref   TO qams_app;

--    Audit ledgers: append-only. INSERT + SELECT only — no UPDATE, no DELETE.
--    (The append-only triggers are defence-in-depth on top of this.)
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA audit TO qams_app;

--    Explicitly remove DELETE anywhere it may have been inherited.
REVOKE DELETE ON ALL TABLES IN SCHEMA qams, audit, read, saas, ref FROM qams_app;

--    Exception (MSG-007): the transactional outbox is delivery transport, not a
--    regulated record — processed events live on in the hash-chained audit
--    ledger. The retention purge deletes processed rows past the window.
GRANT DELETE ON qams.outbox_event TO qams_app;

-- 5. Sequences (reference-number counters etc.) ------------------------------
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA qams, saas, ref TO qams_app;

-- 6. Default privileges for objects created by future migrations -------------
ALTER DEFAULT PRIVILEGES FOR ROLE qams_owner IN SCHEMA qams, read, saas
    GRANT SELECT, INSERT, UPDATE ON TABLES TO qams_app;
ALTER DEFAULT PRIVILEGES FOR ROLE qams_owner IN SCHEMA ref
    GRANT SELECT ON TABLES TO qams_app;
ALTER DEFAULT PRIVILEGES FOR ROLE qams_owner IN SCHEMA audit
    GRANT SELECT, INSERT ON TABLES TO qams_app;
ALTER DEFAULT PRIVILEGES FOR ROLE qams_owner IN SCHEMA qams, saas, ref
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO qams_app;

-- 7. Confirm qams_app is NOT a superuser / bypassrls -------------------------
--    (RLS is only enforced when the connecting role lacks BYPASSRLS.)
ALTER ROLE qams_app NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;

-- Usage:
--   psql "host=... dbname=ntqams user=postgres" \
--        -v owner_password="'<secret>'" -v app_password="'<secret>'" \
--        -f harden-runtime-role.sql
-- Then set the app connection string Username=qams_app; migrations run as qams_owner.
