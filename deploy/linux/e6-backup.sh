#!/usr/bin/env bash
#
# E6 Car Spa — nightly encrypted off-site backup (Linux VPS).
#
# pg_dump (custom format) -> tar with appsettings.json -> gpg symmetric encrypt
# -> upload to Google Drive via rclone -> email report -> prune old local copies.
#
# Run manually once to verify, then schedule via cron (see deploy/linux/README.md).

set -euo pipefail

# ----- CONFIG: adjust these for your server -----
DB_NAME="e6carspa_prod"
DB_USER="e6backup"                          # dedicated SELECT-only backup user
DB_HOST="localhost"
DB_PORT="5432"
FILES_TO_BACKUP=(                            # non-DB files worth keeping with each backup
    "/opt/e6carspa/api/appsettings.json"
)
EMAIL_TO="you@trovotechsolutions.in"
RETAIN_LOCAL_DAYS=3                          # Drive is the real backup; local is a fast fallback
RCLONE_REMOTE="gdrive"                       # must match the remote name from `rclone config`
DRIVE_PATH="E6CarSpa/DailyBackups"
GPG_PASS_FILE="/root/.e6backup_gpg_pass"
WORK_DIR="/var/backups/e6carspa/tmp"
ARCHIVE_DIR="/var/backups/e6carspa/archive"
LOG_FILE="/var/log/e6carspa-backup.log"
# ----- end CONFIG -----

STAMP="$(date +%Y-%m-%d_%H-%M-%S)"
BASENAME="e6carspa_backup_${STAMP}"
TAR_PATH="${WORK_DIR}/${BASENAME}.tar"
ENC_PATH="${ARCHIVE_DIR}/${BASENAME}.tar.gpg"
DUMP_PATH="${WORK_DIR}/db_${STAMP}.dump"

log() { echo "$(date '+%Y-%m-%d %H:%M:%S')  $*" | tee -a "$LOG_FILE"; }

# Always wipe the plaintext dump/tar, even on failure — only the encrypted file may persist.
cleanup() { rm -f "$DUMP_PATH" "$TAR_PATH"; }
trap cleanup EXIT

notify_failure() {
    local reason="$1"
    log "ERROR: $reason"
    {
        echo "Subject: [E6 Car Spa] Backup FAILED — ${STAMP}"
        echo
        echo "The nightly backup failed: ${reason}"
        echo "See ${LOG_FILE} on the server for details."
    } | msmtp -a default "$EMAIL_TO" || true
    exit 1
}
trap 'notify_failure "unexpected error at line $LINENO"' ERR

mkdir -p "$WORK_DIR" "$ARCHIVE_DIR"

log "Starting backup ${STAMP}"

# 1. Dump the database (auth comes from ~/.pgpass — never typed or hardcoded here).
log "Dumping ${DB_NAME}..."
pg_dump -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -Fc -f "$DUMP_PATH"

# 2. Bundle the dump with any config files worth keeping.
log "Archiving..."
TAR_INPUTS=("$DUMP_PATH")
for f in "${FILES_TO_BACKUP[@]}"; do
    [ -f "$f" ] && TAR_INPUTS+=("$f") || log "WARNING: ${f} not found, skipping"
done
tar -cf "$TAR_PATH" "${TAR_INPUTS[@]}"

# 3. Encrypt. The plaintext tar/dump are removed by the EXIT trap either way.
log "Encrypting..."
gpg --batch --yes --pinentry-mode loopback \
    --passphrase-file "$GPG_PASS_FILE" \
    -c --cipher-algo AES256 \
    -o "$ENC_PATH" "$TAR_PATH"

# 4. Upload off-site.
log "Uploading to ${RCLONE_REMOTE}:${DRIVE_PATH}..."
rclone copy "$ENC_PATH" "${RCLONE_REMOTE}:${DRIVE_PATH}/" --quiet

# 5. Prune local copies older than RETAIN_LOCAL_DAYS (Drive keeps the real history).
find "$ARCHIVE_DIR" -name 'e6carspa_backup_*.tar.gpg' -mtime "+${RETAIN_LOCAL_DAYS}" -delete

SIZE_KB=$(du -k "$ENC_PATH" | cut -f1)
log "Backup complete: $(basename "$ENC_PATH") (${SIZE_KB} KB)"

{
    echo "Subject: [E6 Car Spa] Backup OK — ${STAMP}"
    echo
    echo "Backup succeeded: $(basename "$ENC_PATH") (${SIZE_KB} KB)"
    echo "Uploaded to ${RCLONE_REMOTE}:${DRIVE_PATH}/"
} | msmtp -a default "$EMAIL_TO" || log "WARNING: success email failed to send"
