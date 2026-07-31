# Backup, Restore & Disaster Recovery Runbook (F-10)

**System:** NT.QMS (multi-tenant SaaS Quality Management System)
**Applies to:** production and validation (staging) environments
**Regulatory basis:** 21 CFR Part 11 §11.10(c) (protection of records), EU Annex 11
§7 (data storage) & §16 (business continuity), ISO/IEC 17025 §7.11 (data & information
management), ISO 9001 §7.5.3.

This runbook is a controlled document. Changes follow the change-control process and
each revision is reviewed and approved before it takes effect.

---

## 1. Objectives

| Objective | Target |
| --------- | ------ |
| **RPO** (max tolerable data loss) | **≤ 5 minutes** (continuous WAL archiving) |
| **RTO** (max tolerable downtime)  | **≤ 4 hours** (restore + verify + cutover) |
| Backup retention | Daily 35 days · weekly 3 months · monthly 7 years (≥ record retention) |
| Restore drill cadence | Quarterly (documented, evidence retained) |
| Encryption | At rest (AES-256) and in transit (TLS) for all backup copies |
| Off-site copy | Every backup replicated to a geographically separate region |

The 7-year monthly floor exists because quality/compliance records are retained for
their full lifecycle; backups must outlive the records they protect.

---

## 2. What is backed up

1. **PostgreSQL database** — all tenant data, the append-only audit/signature ledgers,
   and the control-plane (`saas`) schema. This is the system of record.
2. **File/object storage** — the immutable document and archive snapshots referenced
   by `file_reference` / `archive_entry.snapshot_file_id`. A database restore is
   incomplete without the matching files, so the two are backed up as a set and their
   timestamps recorded together.
3. **Configuration & secrets** — environment configuration and secret-store contents
   (JWT signing key, DB credentials). Secrets are backed up **in the secret store's own
   encrypted export**, never in the database dump (see `DEV-SECRETS.md` / F-17).

Not backed up (reconstructable): application binaries (rebuilt from a tagged release),
transient caches.

---

## 3. Backup strategy

Two complementary layers:

### 3a. Continuous archiving (Point-In-Time Recovery)
- PostgreSQL WAL archiving enabled (`archive_mode = on`, `wal_level = replica`),
  shipping WAL segments to encrypted off-site storage at least every 5 minutes
  (`archive_timeout = 300`).
- A weekly base backup (`pg_basebackup`) anchors the WAL chain.
- Enables restore to **any point in time** — essential for "undo" after a bad
  deployment or data-integrity incident without losing a full day.

### 3b. Nightly logical dump
- `deploy/backup.sh` runs a nightly `pg_dump -Fc` (custom format, compressed) plus a
  snapshot/manifest of the file store. Simple, portable, easy to restore a single
  tenant or table, and independent of the PostgreSQL major version.

Both layers are encrypted, checksummed, and copied off-site. A backup is not
considered complete until its checksum is verified at the destination.

---

## 4. Procedures

### 4a. Take a backup (nightly / on demand)
```bash
# Scheduled via cron/systemd-timer; can be run manually before a risky change.
PGHOST=... PGUSER=qms_backup PGPASSWORD=... \
  ./deploy/backup.sh /secure/backups
```
`qms_backup` is a dedicated read-only role. The script writes
`ntqms-<timestamp>.dump`, a `filestore-<timestamp>.tar`, and a `manifest.sha256`.

### 4b. Full restore (new/rebuilt environment)
```bash
# 1. Provision an empty PostgreSQL 17 + the runtime roles:
psql -f deploy/harden-runtime-role.sql
# 2. Restore the database and file store from a chosen backup set:
./deploy/restore.sh /secure/backups/ntqms-<timestamp>.dump /secure/backups/filestore-<timestamp>.tar
# 3. Point the app at restored config/secrets, start it, verify (§5).
```

### 4c. Point-in-time recovery (roll back to just before an incident)
1. Restore the most recent base backup taken **before** the target time.
2. Configure recovery: `restore_command` pointing at the WAL archive and
   `recovery_target_time = '<UTC instant>'`.
3. Start PostgreSQL in recovery; it replays WAL to the target and promotes.
4. Verify (§5), then re-open to traffic.

### 4d. Disaster recovery / regional failover
1. Stand up the app + PostgreSQL in the secondary region.
2. Restore the latest off-site base backup and replay archived WAL to the last
   received segment (meets the ≤ 5-minute RPO).
3. Restore the file store from its off-site copy for the same instant.
4. Verify (§5); update DNS/reverse-proxy to the secondary; announce cutover.
5. When the primary returns, reverse-replicate before failing back.

---

## 5. Restore verification (mandatory after every restore/drill)

A restore is only accepted after **all** of the following pass — and the evidence is
retained as a record:

1. **Application health** — `/health` returns 200; the app starts with no migration
   drift (`dotnet ef migrations list` shows all applied).
2. **Tenant isolation intact** — log in to two tenants; confirm each sees only its own
   data, then prove the fence structurally. The schema-hardening program made this a
   single query that must return **zero rows** (every table whose `tenant_id` is NOT NULL
   has RLS enabled, forced, and a `tenant_isolation` policy):

   ```sql
   SELECT n.nspname||'.'||c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE c.relkind='r' AND n.nspname IN ('qams','audit','read')
     AND EXISTS (SELECT 1 FROM information_schema.columns col
                 WHERE col.table_schema=n.nspname AND col.table_name=c.relname
                   AND col.column_name='tenant_id' AND col.is_nullable='NO')
     AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity
          OR NOT EXISTS (SELECT 1 FROM pg_policies p WHERE p.schemaname=n.nspname
                           AND p.tablename=c.relname AND p.policyname='tenant_isolation'));
   ```

   Expect **90** FORCE-RLS tables and 90 `tenant_isolation` policies. `qams.user_account`
   and `qams.outbox_event` are the documented exceptions (nullable tenant) and must **not**
   appear in the query above.
3. **Audit-trail integrity** — run the hash-chain verification
   (`GET /api/compliance/chain-verification`) for a sample of tenants; it must report
   the chain intact (no break). A broken chain after restore means an incomplete
   or tampered backup — reject it.
4. **Signature manifest + row counts** — spot-check `signature_record` and key table
   counts against the pre-incident figures recorded in the backup manifest. Row identity
   is `(tenant_id, id)` on 88 tables since the hardening program, so a manifest that
   records bare ids is ambiguous across tenants — record the pair.
5. **Guard triggers survived the restore** — `pg_restore` recreates triggers, but verify
   rather than assume: expect **17** enabled guards (4 append-only ledgers + 13
   `frozen_immutability`). A restore that silently lost them would leave the regulated
   records mutable.

   ```sql
   SELECT count(*), string_agg(DISTINCT tgenabled::text, ',') FROM pg_trigger
   WHERE NOT tgisinternal AND (tgname LIKE '%append_only' OR tgname='frozen_immutability');
   ```
6. **File integrity** — a sample of `file_reference` rows resolve to files whose
   SHA-256 matches the stored content hash (immutable-snapshot check).

Record the drill: date, backup set used, RTO achieved, verification results, and the
operator — under the periodic-review evidence expected by Annex 11 §16.

---

## 6. Roles & responsibilities

| Role | Responsibility |
| ---- | -------------- |
| Platform/DevOps on-call | Executes backups, restores, and failover; owns the schedule. |
| Quality Manager | Approves this runbook; reviews quarterly drill evidence. |
| Data owner (per tenant) | Notified of any restore affecting their data. |

## 7. Related controls
- `deploy/harden-runtime-role.sql` — least-privilege roles (F-02).
- `deploy/DEV-SECRETS.md` — secret handling (F-17).
- Audit-trail hash chain + immutability triggers (F-01/F-02) — the integrity checks
  in §5 depend on them.
