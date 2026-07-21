-- NT.QAMS database bootstrap — run ONCE per server, as a PostgreSQL superuser.
-- Replace the password before running; never reuse dev credentials.

CREATE ROLE qams_app LOGIN PASSWORD 'CHANGE_ME_BEFORE_RUNNING';
CREATE DATABASE ntqams OWNER qams_app;

-- Note (Phase 0): qams_app owns the database so EF migrations can create
-- schemas/tables. The stricter split (migration role vs runtime role, RLS
-- policies, FORCE ROW LEVEL SECURITY) ships with the first tenant-scoped
-- business tables in Phase 1+, per NT_QAMS_Database_Architecture.md §1.1.
