#!/bin/sh
set -eu

BACKUP_DIR=${BACKUP_DIR:-/backups}
RETAIN_DAYS=${BACKUP_RETAIN_DAYS:-14}

stamp=$(date -u +%Y%m%dT%H%M%SZ)
out="${BACKUP_DIR}/xpense-${stamp}.dump"

pg_dump -Fc -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -f "${out}"

find "${BACKUP_DIR}" -name 'xpense-*.dump' -type f -mtime "+${RETAIN_DAYS}" -delete

echo "backup: ${out} ($(du -h "${out}" | cut -f1))"
