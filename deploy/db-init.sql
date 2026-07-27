-- NT.QAMS database bootstrap — run ONCE per server, as a PostgreSQL superuser.
-- Replace both passwords before running; never reuse dev credentials.
--
-- Two-role model (TENANT-004 / harden-runtime-role.sql):
--   qams_owner — owns the database/schema; runs migrations ONLY (DDL).
--   qams_app   — the application's runtime login; least privilege, DML only.
-- The application must NEVER connect as qams_owner: a table owner can drop the
-- RLS policies and immutability triggers, and the Production start-up guard
-- refuses to boot as an over-privileged role.

CREATE ROLE qams_owner LOGIN PASSWORD 'CHANGE_ME_OWNER_BEFORE_RUNNING';
CREATE ROLE qams_app   LOGIN PASSWORD 'CHANGE_ME_APP_BEFORE_RUNNING'
    NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
CREATE DATABASE ntqams OWNER qams_owner;

-- Next steps (see DEPLOY.md):
--   1. Run migrations as the owner:  psql -U qams_owner -d ntqams -f migrations.sql
--   2. Grant the runtime role its least-privilege DML surface:
--      psql -U postgres -d ntqams -f harden-runtime-role.sql
--   3. Point the app's ConnectionStrings__Postgres at Username=qams_app.
