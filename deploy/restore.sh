#!/usr/bin/env bash
#
# NT.QMS restore (F-10). Restores a backup set produced by deploy/backup.sh into an
# empty, already-provisioned database (run deploy/harden-runtime-role.sql first).
#
# Usage:  PGHOST=... PGUSER=qams_owner PGPASSWORD=... \
#           ./restore.sh <db-dump> [filestore-tar] [filestore-dir]
#
# After restoring, run the mandatory verification in deploy/BACKUP-RESTORE-DR.md §5
# (health, tenant isolation, audit-trail hash-chain, file integrity) BEFORE opening
# the environment to traffic.
set -euo pipefail

DUMP="${1:?database dump required}"
FILES="${2:-}"
FILESTORE_DIR="${3:-/var/lib/ntqms/files}"
DB_NAME="${PGDATABASE:-ntqms}"

echo "[restore] db=$DB_NAME  dump=$DUMP"

# Restore is run as the owning role so RLS/ownership are correct. --clean --if-exists
# makes the restore idempotent onto an existing (empty or prior) database.
pg_restore --clean --if-exists --no-owner --role="${PGUSER:-qams_owner}" \
  --dbname="$DB_NAME" "$DUMP"

if [ -n "$FILES" ]; then
  echo "[restore] file store -> $FILESTORE_DIR"
  mkdir -p "$FILESTORE_DIR"
  tar -xf "$FILES" -C "$FILESTORE_DIR"
fi

cat <<'EOF'
[restore] database restored.
[restore] MANDATORY next steps (see BACKUP-RESTORE-DR.md §5):
  1. /health returns 200 and no EF migration drift.
  2. Tenant isolation intact (RLS forced; two tenants see only their own data).
  3. Audit-trail hash chain verifies (GET /api/compliance/chain-verification).
  4. Signature/row counts match the backup manifest.
  5. Sampled file_reference SHA-256 matches stored content hash.
Do NOT open to traffic until all five pass, and record the result as a drill.
EOF
