# Xpense.API

Financial tracker and advisor.

## Running it

```bash
docker compose up -d                          # PostgreSQL on :5432
dotnet run --project src/Xpense/Xpense.API    # migrates on startup, serves http://localhost:4000
```

Swagger UI is at the root in Development.

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

Vertical slices: one endpoint per file, holding its route, request, validation and handler.

- [`docs/vertical-slicing-architecture/`](docs/vertical-slicing-architecture/) — why, how, and the trade-offs
- [`docs/postgres.md`](docs/postgres.md) — database, migrations, test setup
- [`docs/multi-currency.md`](docs/multi-currency.md) — denominated accounts, and why nothing converts
- [`docs/contract/api-v1-contract-design.md`](docs/contract/api-v1-contract-design.md) — the v1 API contract
- [`AGENTS.md`](AGENTS.md) — the rules, enforced by `SliceIsolationTests`

```
src/Xpense/
  Xpense.API/          slices, shared contracts, exception handlers, infrastructure
  Xpense.Domain/       entities, value objects, enums, exceptions, MoneyTransfer
  Xpense.Persistence/  DbContext, type configuration, migrations, OptionResolver
  Xpense.Tests/        ApiEndpointTests (canonical), Unit, Architecture
```
