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
- [`docs/multi-currency.md`](docs/multi-currency.md) — denominated accounts, and why nothing converts
- [`docs/contract/api-v1-contract-design.md`](docs/contract/api-v1-contract-design.md) — the v1 API contract
- [`AGENTS.md`](AGENTS.md) — the rules, enforced by `SliceIsolationTests`

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
