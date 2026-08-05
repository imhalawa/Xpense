# syntax=docker/dockerfile:1

# Build context is this directory, since nothing here needs the solution.
FROM postgres:17-alpine

COPY postgresql.conf /etc/postgresql/postgresql.conf
COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh

# The official entrypoint still runs (initdb, POSTGRES_* handling); only the server command is
# replaced, which is the documented way to point Postgres at a config file of your own.
CMD ["postgres", "-c", "config_file=/etc/postgresql/postgresql.conf"]
