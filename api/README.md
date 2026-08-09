# Xpense.API

Financial tracker and advisor.

## Running it

```bash
cp .env.example .env      # required: compose has no fallback password and will refuse to start
```

Everything in containers — Postgres, then migrations, then the API on
http://localhost:4000:

```bash
docker compose up -d --build
```

Or the inner loop, with only the database in a container and the API on the host:

```bash
docker compose up -d postgres

cd src/Xpense
dotnet tool restore                                    # first time only
dotnet dotnet-ef database update --project Xpense.Persistence --startup-project Xpense.Persistence

dotnet run --project Xpense.API                        # serves http://localhost:4000
```

Both publish on port 4000, so run one at a time. Details of the container setup,
including backups and the restore drill, are in [`docs/docker.md`](docs/docker.md).

Migrations are a deployment step and deliberately do not run on startup, so the
schema has to exist before the API boots — in containers that is the one-shot
`migrations` service, and on the host it is the `database update` above. See
[ADR 0004](docs/adr/0004-migrations-are-a-deployment-step.md). Integration tests
are unaffected — `PostgresFixture` migrates its own template database.

`GET /health` reports whether the API can reach Postgres. It is the one route
mapped outside `Features/`, because it is infrastructure rather than a feature.

Swagger UI is at the root in Development, and its generated document is the
machine-readable API contract. There is no hand-maintained OpenAPI file; see
[ADR 0003](docs/adr/0003-generated-openapi-is-the-contract.md).

Swagger is not served outside Development. Its security section reflects the runtime contract: protected reads use the `xpense.session` cookie; protected mutations also require the `X-Xpense-Antiforgery` header and paired `xpense.antiforgery` cookie.

## Self-hosted identity and privacy

The first account is created through the public registration flow. Set `XPENSE_AUTH_REGISTRATION` to `Open`, `InviteOnly` or `Closed`. Passkeys are the primary sign-in method; recovery passwords and recovery files are independent vault-recovery methods. Losing every passkey and recovery method permanently loses access to encrypted data, so test recovery before importing important records.

Users may create groups, invite other accounts and share selected accounts or budgets as viewer or editor. Membership in one group grants nothing in another. Private-resource authorization returns the same 404 for missing, unrelated, revoked and deleted data.

The API stores encrypted group names, envelopes and record ciphertext. The server operator can still see identities, membership and grant metadata, timing, sizes, email delivery addresses and any legacy plaintext financial tables. This release does not migrate those legacy tables into the encrypted record store. Do not describe the whole database as zero knowledge until that migration is complete.

Public endpoints include registration/sign-in ceremonies, invitation inspection, antiforgery token issuance and health. Authenticated browser mutations use the session cookie plus the paired antiforgery header and cookie. Refresh the antiforgery pair after authentication changes.

For a hosted or internet-facing install, use HTTPS, set the relying-party domain and public URL to the external origin, list the exact browser origin, keep Data Protection keys on durable private storage, and run migrations before both API and worker. Never expose PostgreSQL or the backup directory publicly.

Invitation email is optional. With `XPENSE_EMAIL_ENABLED=false`, invitations still work: the create response returns the one-time link and the owner copies it to the recipient. When enabled, configure SMTP host, port, from address, TLS, timeout and paired credentials. API and worker must share the same Data Protection key directory or the worker cannot decrypt the protected invitation link.

Legacy claim mode is an operator-only migration bridge. Set `XPENSE_LEGACY_CLAIM_ENABLED=true` and `XPENSE_LEGACY_CLAIM_DESIGNATED_USER_ID` to an existing user UUID only during a rehearsed claim. The API blocks plaintext source writes and the notifications worker pauses. Keep claim mode enabled after completion until the reviewed Task 32 contract migration removes the plaintext schema; disabling it earlier can create new unclaimed rows. Claim mode never creates a user or changes plaintext ownership.

Direct-host configuration uses the standard .NET double-underscore environment form:

| Setting | Contract |
|---|---|
| `Authentication__RelyingPartyDomain` | Passkey relying-party host. It cannot be `localhost` outside Development. |
| `Authentication__RelyingPartyName` | Name shown by the passkey prompt. |
| `Authentication__AllowedOrigins__0` | Exact browser origin, including scheme and port. Add further origins with increasing indexes. |
| `Authentication__Registration` | `Open`, `InviteOnly` or `Closed`. |
| `Authentication__PublicUrl` | External application origin. It must be absolute HTTPS outside Development. |
| `DataProtection__KeyDirectory` | Writable, durable, private directory shared by the API and notification worker. |
| `ForwardedHeaders__KnownIPNetworks__0` | Trusted proxy address as CIDR, as seen from the API process. Use a narrow range such as a single-address `/32`. |
| `ForwardedHeaders__ForwardLimit` | Positive number of trusted proxy hops. The default is `1`. |
| `LegacyClaim__Enabled` | `true` enables the controlled claim window and pauses plaintext writers. Keep it enabled through the contract migration. |
| `LegacyClaim__DesignatedUserId` | Existing designated claimant UUID, required when claim mode is enabled. |
| `LegacyClaim__DataMode` | Durable data authority: keep `legacy` through Task 32, then set `encrypted` only after the reviewed plaintext-removal cutover. Never switch back. |
| `Email__Enabled` | `true` enables SMTP delivery in the notification worker. |
| `Email__Host` | Required when email is enabled. |
| `Email__Port` | Integer from 1 to 65535. The default is 587. |
| `Email__FromAddress` | Valid sender address, required when email is enabled. |
| `Email__UseTls` | Whether SMTP uses TLS. |
| `Email__Username` and `Email__Password` | Supply both or neither. |
| `Email__TimeoutSeconds` | Integer from 1 to 30. The default is 10. |

Forwarded headers are ignored unless the immediate proxy matches the configured address or network. A reverse proxy on the host or in another container is usually not `127.0.0.1` from inside the API container. Set the trusted CIDR to the narrow address the API actually sees; otherwise generated links may use HTTP and authentication rate limits group every client under the proxy address.

Back up the Data Protection key directory with the database. Restore both from the same backup set while the API and notification worker are stopped. Restoring only PostgreSQL invalidates sessions and can make pending protected invitation deliveries unreadable.

## Tests

```bash
dotnet test src/Xpense/Xpense.sln
```

Integration tests start a real PostgreSQL container via Testcontainers, so **Docker must be
running**. Unit and architecture tests do not need it.

## Stack

- .NET 10, ASP.NET Core minimal APIs
- PostgreSQL via Npgsql + EF Core 10
- FluentValidation, Serilog, Swashbuckle

## Architecture

Vertical slices: one endpoint per file, holding its route, request, validation and handler. Two
processes: the API, and a worker that turns events into notifications.

- [`docs/vertical-slicing-architecture/`](docs/vertical-slicing-architecture/) — why, how, and the trade-offs
- [`docs/postgres.md`](docs/postgres.md) — database, migrations, test setup
- [`docs/docker.md`](docs/docker.md) — the container setup, backups, restore drill
- [`docs/notifications.md`](docs/notifications.md) — events, the queue, and writing a rule
- [`docs/adr/0009-authorization-is-an-explicit-query.md`](docs/adr/0009-authorization-is-an-explicit-query.md) — private-resource authorization and neutral 404s
- [`docs/multi-currency.md`](docs/multi-currency.md) — denominated accounts, and why nothing converts
- [`docs/contract/api-v1-contract-design.md`](docs/contract/api-v1-contract-design.md) — the v1 API contract
- [`AGENTS.md`](../AGENTS.md) — the rules, enforced by `SliceIsolationTests`

```
src/Xpense/
  Xpense.API/           slices, shared contracts, exception handlers, infrastructure
  Xpense.Domain/        entities, value objects, enums, events, exceptions
  Xpense.Notifications/ the worker: notification rules, event processor, pump
  Xpense.Persistence/   DbContext, type configuration, migrations, OptionResolver
  Xpense.Tests/         ApiEndpointTests (canonical), Unit, Architecture

docker/
  api/                  the API image
  migrations/           EF migration bundle, applied as its own step
  notifications/        the worker image
  postgres/             Postgres image, config, backup script
docker-compose.yml      the four services and their ordering
```
