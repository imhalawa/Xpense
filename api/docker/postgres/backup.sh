#!/bin/sh
# Dumps the database to /backups, which is a bind mount from the host -- deliberately not the data
# volume, so a corrupted volume does not take the backups with it.
#
# Nothing calls this on a schedule from inside the container. Add a host crontab line:
#   0 3 * * *  cd /path/to/Xpense.API && docker compose exec -T postgres backup.sh
#
# See docs/docker.md for the restore drill. A backup nobody has restored is a guess.
set -eu

BACKUP_DIR=${BACKUP_DIR:-/backups}
RETAIN_DAYS=${BACKUP_RETAIN_DAYS:-14}

# -Fc is the custom format: compressed, and pg_restore can pull a single table out of it rather
# than forcing an all-or-nothing replay.
stamp=$(date -u +%Y%m%dT%H%M%SZ)
out="${BACKUP_DIR}/xpense-${stamp}.dump"

pg_dump -Fc -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -f "${out}"

# Prune after a successful dump, never before -- a failing pg_dump must not also delete history.
find "${BACKUP_DIR}" -name 'xpense-*.dump' -type f -mtime "+${RETAIN_DAYS}" -delete

echo "backup: ${out} ($(du -h "${out}" | cut -f1))"
