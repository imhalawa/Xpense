# syntax=docker/dockerfile:1

FROM postgres:17-alpine

COPY postgresql.conf /etc/postgresql/postgresql.conf
COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh

CMD ["postgres", "-c", "config_file=/etc/postgresql/postgresql.conf"]
