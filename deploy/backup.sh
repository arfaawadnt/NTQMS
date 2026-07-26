#!/usr/bin/env bash
#
# NT.QMS nightly backup (F-10). Produces a compressed, checksummed, restorable
# backup set: the PostgreSQL database + the file/object store + a manifest.
#
# Usage:  PGHOST=... PGUSER=qms_backup PGPASSWORD=... \
#           ./backup.sh <output-dir> [filestore-dir]
#
# Restore with deploy/restore.sh. See deploy/BACKUP-RESTORE-DR.md for the full
# procedure, PITR/WAL archiving, retention, and off-site replication.
set -euo pipefail

OUT_DIR="${1:?output directory required}"
FILESTORE_DIR="${2:-/var/lib/ntqms/files}"
DB_NAME="${PGDATABASE:-ntqms}"
TS="$(date -u +%Y%m%dT%H%M%SZ)"

mkdir -p "$OUT_DIR"
DUMP="$OUT_DIR/ntqms-$TS.dump"
FILES="$OUT_DIR/filestore-$TS.tar"
MANIFEST="$OUT_DIR/manifest-$TS.sha256"

echo "[backup] $TS  db=$DB_NAME  -> $OUT_DIR"

# 1. Database — custom format (compressed, selective restore, version-portable).
pg_dump --format=custom --compress=9 --dbname="$DB_NAME" --file="$DUMP"

# 2. File/object store — the immutable document & archive snapshots.
if [ -d "$FILESTORE_DIR" ]; then
  tar -cf "$FILES" -C "$FILESTORE_DIR" .
else
  echo "[backup] WARNING: file store '$FILESTORE_DIR' not found — DB-only backup." >&2
  FILES=""
fi

# 3. Manifest — checksums verified at the off-site destination before a backup is
#    accepted as complete (a backup you cannot verify is not a backup).
{
  sha256sum "$DUMP"
  [ -n "$FILES" ] && sha256sum "$FILES"
} > "$MANIFEST"

echo "[backup] complete:"
echo "  database : $DUMP"
[ -n "$FILES" ] && echo "  files    : $FILES"
echo "  manifest : $MANIFEST"
echo "[backup] NEXT: encrypt + replicate off-site, then verify checksums at destination."
