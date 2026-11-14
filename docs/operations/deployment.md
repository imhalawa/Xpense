# Deployment

Xpense uses four services: PostgreSQL, a one-shot migration service, the API, and the notifications worker. Run Compose commands from `api/`.

## Start

1. Copy `.env.example` to `.env` and set the database password.
2. Run `docker compose up -d --build`.
3. Check the services with `docker compose ps` and the API with `GET /health`.

Compose waits for PostgreSQL, runs migrations, then starts the API and worker. In Development, Swagger is available at `http://localhost:4000`.

For an internet-facing deployment, use Production, HTTPS, the external public URL and relying-party domain, the exact browser origin, and a TLS-terminating reverse proxy.

## Configuration

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


Set the trusted proxy CIDR to the address the API container actually sees. If it does not match, forwarded scheme and client addresses are ignored.

The API and worker share the `xpense-dataprotection` volume at `/keys`. Keep it private and durable. Back it up with PostgreSQL: restoring a database without its matching keys can invalidate sessions and leave pending invitation email unreadable.

## Backups

The backup script writes compressed PostgreSQL dumps to `api/backups/`. It removes dumps older than `BACKUP_RETAIN_DAYS` only after a successful backup; the default is 14 days. The script is not scheduled automatically.

Schedule a backup on the host, or run it manually:

```cron
0 3 * * *  cd /path/to/Xpense/api && docker compose exec -T postgres backup.sh
```

Run it by hand any time:

```bash
docker compose exec -T postgres backup.sh
```

## Restore

Restore a dump to a scratch database to check it:

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
```

Restore the matching Data Protection key snapshot using your host or container platform. Then start both writers with `docker compose start api notifications`.

## Query statistics

Enable and query `pg_stat_statements`:

```bash
docker compose exec -T postgres psql -U xpense -d xpense -c 'create extension if not exists pg_stat_statements;'
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select calls, round(total_exec_time) ms, query from pg_stat_statements order by total_exec_time desc limit 10;'
```

Anything slower than a second is also logged — `log_min_duration_statement = 1000`.

## Troubleshooting

- A failed health check does not restart a running container; the restart policy responds to process exit.
- Compose dependency ordering applies when starting the stack. A daemon restart can start containers without rerunning migrations.
- Rebuild the migration image after changing migrations. Run migrations before starting either writer.
- The host `backups` directory must be writable by the container's PostgreSQL user.
- The Alpine Npgsql Kerberos probe can log a missing `libgssapi_krb5.so.2`; check service health and subsequent errors before treating it as a failure.
- The host API and container API both use port 4000. Stop the container before running the host API.

Each .NET Dockerfile restores the whole solution. Add a new project to each Dockerfile when adding it to the solution.
