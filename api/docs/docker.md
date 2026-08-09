# Docker

Xpense runs as four containers: Postgres, a one-shot migration step, the API and the notification worker. One
`docker-compose.yml` describes all of it. There is no deployment target — this is how the app and
its dependencies run on one machine.

```
docker/
  api/Dockerfile           sdk:10.0 build -> aspnet:10.0-alpine, non-root
  migrations/Dockerfile    sdk:10.0 -> EF migration bundle -> runtime-deps:10.0-alpine
  notifications/Dockerfile sdk:10.0 build -> aspnet:10.0-alpine, non-root
  postgres/
    Dockerfile             postgres:17-alpine + config + backup script
    postgresql.conf        overrides only
    backup.sh              pg_dump to /backups
docker-compose.yml
```

All three .NET Dockerfiles restore the **whole solution**, so every project's `.csproj` has to be copied
in each of them. Adding a project and forgetting one breaks all three builds, not only the new one.

## First run

```bash
cp .env.example .env       # compose defines no fallback password and will refuse to start
docker compose up -d --build
```

The order is enforced, not hoped for:

1. `postgres` starts and becomes healthy — `pg_isready`, not "the container exists".
2. `migrations` runs the EF bundle against it and exits 0. It is not a server; `docker compose ps`
   not listing it is success.
3. `api` and `notifications` start, both gated on `service_completed_successfully`, so neither meets a
   schema that is not there yet.

Swagger UI is at http://localhost:4000, and `GET /health` reports whether the API can reach
Postgres.

Compose reads deployment settings from `.env`. `XPENSE_ENVIRONMENT=Development` enables Swagger and permits HTTP localhost passkeys. Internet-facing installs use `Production`, an HTTPS `XPENSE_PUBLIC_URL`, the exact relying-party domain and browser origin, and a reverse proxy that terminates TLS. Registration can be `Open`, `InviteOnly` or `Closed`.

The deployment variables map to these runtime settings:

| Compose variable | Runtime setting | Validation |
|---|---|---|
| `XPENSE_AUTH_RP_DOMAIN` | `Authentication__RelyingPartyDomain` | Cannot be `localhost` outside Development. |
| `XPENSE_AUTH_RP_NAME` | `Authentication__RelyingPartyName` | Display name for passkey prompts. |
| `XPENSE_AUTH_ORIGIN` | `Authentication__AllowedOrigins__0` | Exact browser origin, including scheme and port. |
| `XPENSE_AUTH_REGISTRATION` | `Authentication__Registration` | `Open`, `InviteOnly` or `Closed`. |
| `XPENSE_PUBLIC_URL` | `Authentication__PublicUrl` | Absolute HTTPS outside Development. |
| `XPENSE_TRUSTED_PROXY_NETWORK` | `ForwardedHeaders__KnownIPNetworks__0` | Valid narrow CIDR matching the proxy address seen inside the API container. |
| fixed `/keys` mount | `DataProtection__KeyDirectory` | Writable by UID 1001 and shared by API and worker. |
| `XPENSE_EMAIL_ENABLED` | `Email__Enabled` | `false` disables delivery without disabling invitation links. |
| `XPENSE_EMAIL_HOST` | `Email__Host` | Required when delivery is enabled. |
| `XPENSE_EMAIL_PORT` | `Email__Port` | Integer from 1 to 65535. |
| `XPENSE_EMAIL_FROM_ADDRESS` | `Email__FromAddress` | Valid address required when enabled. |
| `XPENSE_EMAIL_USE_TLS` | `Email__UseTls` | Boolean. |
| `XPENSE_EMAIL_USERNAME` and `XPENSE_EMAIL_PASSWORD` | `Email__Username` and `Email__Password` | Supply both or neither. |
| `XPENSE_EMAIL_TIMEOUT_SECONDS` | `Email__TimeoutSeconds` | Integer from 1 to 30. |
| `XPENSE_LEGACY_CLAIM_ENABLED` | `LegacyClaim__Enabled` | Keep `false` normally, then keep `true` from the operator-controlled claim through the Task 32 contract migration; also pauses notifications. |
| `XPENSE_LEGACY_CLAIM_DESIGNATED_USER_ID` | `LegacyClaim__DesignatedUserId` | Existing user UUID required by the API when claim mode is enabled. |

`XPENSE_TRUSTED_PROXY_NETWORK=127.0.0.1/32` is only the local default. A host proxy or sibling-container proxy normally reaches the API from a bridge address. Replace the value with the narrow CIDR the API container actually sees, preferably one `/32`. If it does not match, ASP.NET Core ignores `X-Forwarded-Proto` and `X-Forwarded-For`: generated links can use HTTP and the authentication rate limiter sees every client as the proxy.

Both API and worker mount `xpense-dataprotection` at `/keys`. Keep that volume private, durable and in the backup plan. Replacing it invalidates protected session and invitation-delivery payloads. Restoring PostgreSQL without the matching keys leaves pending invitation email unreadable.

Treat the PostgreSQL dump and `xpense-dataprotection` volume as one backup set. Use the volume-backup mechanism supplied by the host or container platform. For restore, stop `api` and `notifications`, restore the database dump and its matching key-volume snapshot, then start both writers. A database-only restore is not a complete Xpense restore.

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

**Alpine, and non-root.** The worker uses the ASP.NET runtime because shared Data Protection is part of `Microsoft.AspNetCore.App`, even though the worker exposes no HTTP endpoint. BusyBox `wget` is what makes the API healthcheck a one-liner, and a shell
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

To restore over the real database, stop both writers first so nothing writes while the schema is being
replaced:

```bash
docker compose stop api notifications
docker compose exec -T postgres pg_restore -U xpense -d xpense --clean --if-exists /backups/xpense-<stamp>.dump
docker compose start api notifications
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

**Migrations run before both writers.** Never bypass the one-shot migration service by starting an old API or worker image directly against a newer database backup. Check pending model changes during release verification and rebuild all images after schema changes.

**The bind-mounted `./backups` must be writable by the container's postgres user.** Docker Desktop
on macOS maps this for you. On Linux it does not, so `chown` it if `backup.sh` fails on permissions.
