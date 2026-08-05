# Docker

Xpense runs as three containers: Postgres, a one-shot migration step, and the API. One
`docker-compose.yml` describes all of it. There is no deployment target — this is how the app and
its dependencies run on one machine.

```
docker/
  api/Dockerfile           sdk:10.0 build -> aspnet:10.0-alpine, non-root
  migrations/Dockerfile    sdk:10.0 -> EF migration bundle -> runtime-deps:10.0-alpine
  postgres/
    Dockerfile             postgres:17-alpine + config + backup script
    postgresql.conf        overrides only
    backup.sh              pg_dump to /backups
docker-compose.yml
```

## First run

```bash
cp .env.example .env       # compose defines no fallback password and will refuse to start
docker compose up -d --build
```

The order is enforced, not hoped for:

1. `postgres` starts and becomes healthy — `pg_isready`, not "the container exists".
2. `migrations` runs the EF bundle against it and exits 0. It is not a server; `docker compose ps`
   not listing it is success.
3. `api` starts, gated on `service_completed_successfully`, so it never meets a schema that is not
   there yet.

Swagger UI is at http://localhost:4000, and `GET /health` reports whether the API can reach
Postgres.

## Two ways to run, one port

The inner loop is still `dotnet run` on the host against the containerized database, which is what
[`README.md`](../README.md) shows. Both that and the `api` container publish port 4000, so only one
runs at a time. Stop the container with `docker compose stop api` before `dotnet run`.

The cost of that split is real: a bug that only appears inside the image stays invisible while you
work on the host. The seeder that read a file `publish` never copied is exactly that bug, and it
survived for months. CI therefore builds the images and boots the stack on every push, which is the
compensating check.

## Why the images look the way they do

**`api` and `migrations` share their first six layers, byte for byte.** BuildKit keys its cache on
layer content rather than on which Dockerfile produced it, so the restore happens once and the
second image reuses it. If the two prologues drift, both builds still work — they just quietly stop
sharing and get slower, with no error to tell you.

**The migration bundle is self-contained.** It carries its own .NET, so the final image needs no SDK,
no runtime and no source tree. It sits on `runtime-deps` rather than `runtime` because a
self-contained binary still needs the native libraries — libstdc++, ICU — that image provides.

**Alpine, and non-root.** BusyBox `wget` is what makes the API healthcheck a one-liner, and a shell
is what makes debugging possible at all. Nothing in either image needs root.

**Postgres is published on loopback only.** `127.0.0.1:5432:5432`, not `5432:5432`, so the database
is reachable by `psql` and Testcontainers but not by anything else on the network.

**`postgresql.conf` sets three settings it looks redundant to set.** Pointing `config_file` outside
`PGDATA` moves where Postgres looks for `pg_hba.conf` and `pg_ident.conf` — to `/etc/postgresql`,
where they do not exist — and it drops `listen_addresses` back to the compiled-in `localhost`,
which in a container means nothing can connect. All three are set explicitly. The file holds
overrides only; every other setting keeps its built-in default.

## Backups

`docker/postgres/backup.sh` writes a compressed `pg_dump` to `/backups`, which is the `./backups`
directory on the host — deliberately not the data volume, so losing the volume does not lose the
backups with it. It prunes dumps older than `BACKUP_RETAIN_DAYS` (default 14) *after* a successful
dump, never before.

Nothing runs it on a schedule. Add a host crontab line when you want that:

```cron
0 3 * * *  cd /path/to/Xpense.API && docker compose exec -T postgres backup.sh
```

Run it by hand any time:

```bash
docker compose exec -T postgres backup.sh
```

Note that the schedule lives on the host, so it is not version-controlled and a rebuilt machine
forgets it.

## Restore drill

A backup nobody has restored is a guess. Restoring into a scratch database proves the dump is real
without touching your data:

```bash
docker compose exec -T postgres createdb -U xpense xpense_restore_test
docker compose exec -T postgres pg_restore -U xpense -d xpense_restore_test /backups/xpense-<stamp>.dump
docker compose exec -T postgres psql -U xpense -d xpense_restore_test -c 'select count(*) from "Xpense"."Transactions";'
docker compose exec -T postgres dropdb -U xpense xpense_restore_test
```

To restore over the real database, stop the API first so nothing writes while the schema is being
replaced:

```bash
docker compose stop api
docker compose exec -T postgres pg_restore -U xpense -d xpense --clean --if-exists /backups/xpense-<stamp>.dump
docker compose start api
```

## Query statistics

`shared_preload_libraries` loads `pg_stat_statements`, but the view needs creating once per
database:

```bash
docker compose exec -T postgres psql -U xpense -d xpense -c 'create extension if not exists pg_stat_statements;'
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select calls, round(total_exec_time) ms, query from pg_stat_statements order by total_exec_time desc limit 10;'
```

Anything slower than a second is also logged — `log_min_duration_statement = 1000`.

## Things that will bite

**`Error: Error loading shared library libgssapi_krb5.so.2` is not an error.** Both containers log
it once, on their first database connection: Npgsql probes for Kerberos support, does not find it on
Alpine, and carries on. Migrations still apply and `/health` still returns 200. Installing
`krb5-libs` silences it, but that means an `apk add` in the final image stage, which needs the Alpine
CDN at build time — it failed from this machine's build sandbox while NuGet worked fine. A log line
that reads worse than it is beat a build step that cannot always run.

**A red healthcheck restarts nothing.** `restart: unless-stopped` reacts to the process exiting,
not to health going red. The healthcheck exists so `docker compose ps` and `up --wait` tell the
truth, not to self-heal.

**`depends_on` gates `compose up`, not the restart policy.** After a Docker daemon restart, the
`api` container can come back on its own without `migrations` running first. It will be talking to
whatever schema is already there, which is usually fine and occasionally not.

**Changing a migration means rebuilding the `migrations` image.** The bundle is baked in at build
time, so `docker compose up -d` alone will happily re-run yesterday's migrations. Use `--build`.

**The bind-mounted `./backups` must be writable by the container's postgres user.** Docker Desktop
on macOS maps this for you. On Linux it does not, so `chown` it if `backup.sh` fails on permissions.
