# Postgres

Xpense runs on PostgreSQL via Npgsql. It was previously SQL Server with SQLite in tests.

## Running locally

```bash
cp .env.example .env                                   # compose has no fallback password
docker compose up -d postgres                          # on 127.0.0.1:5432
dotnet dotnet-ef database update --project Xpense.Persistence --startup-project Xpense.Persistence
dotnet run --project src/Xpense/Xpense.API             # serves :4000
```

`appsettings.Development.json` points at the compose database, and `.env.example` uses the
password it expects, so the two agree out of the box. Other environments supply
`ConnectionStrings__DefaultConnection` as an environment variable — nothing else ships a
connection string, so a missing one fails loudly instead of silently reaching localhost. The
containerized API gets that variable from compose, which is why the hardcoded `Host=localhost`
in the Development file does not need changing: environment variables outrank appsettings.

**Nothing migrates at startup.** The application does not call `Database.Migrate()`; migrations
are applied by their own step, either `database update` above or the one-shot `migrations`
container. [ADR 0004](adr/0004-migrations-are-a-deployment-step.md) has the argument.

`EnsureCreated()` is not used either, and not just because migrations exist: it builds the schema
without recording migration history, which then makes `dotnet ef database update` fail against
that database.

Reference data is part of the schema rather than a startup write — the five **Priority** rows come
from `HasData` in `PriorityEntityTypeConfiguration`, applied by the `SeedPriorities` migration. See
[ADR 0005](adr/0005-reference-data-lives-in-migrations.md).

## Migrations

The four SQL Server migrations could not be replayed on Postgres — they emit `nvarchar`,
`datetime2` and SQL Server identity — so they were deleted and replaced by a single
`InitialCreate`. Safe because nothing was deployed and the dev connection string was still a
placeholder.

```bash
cd src/Xpense
dotnet tool restore                                                                        # once
dotnet dotnet-ef migrations add <Name> --project Xpense.Persistence --startup-project Xpense.Persistence
dotnet dotnet-ef database update       --project Xpense.Persistence --startup-project Xpense.Persistence
```

`dotnet-ef` is a **local** tool pinned in `.config/dotnet-tools.json`, hence `dotnet dotnet-ef`
rather than `dotnet ef`. The version has to match the EF runtime: a globally installed 8.x tool
refuses to run against EF 10, and the manifest is the one place that version is stated — the
`migrations` image restores the same manifest.

`XpenseContextFactory` supplies the design-time connection string. It is only opened for
`database update`; scaffolding a migration just needs it to parse. It hardcodes `Host=localhost`,
which is correct on the host and meaningless inside a container, so the migration bundle is always
handed `--connection` explicitly.

Tables live in the `Xpense` schema, not `public`, so `\dt` alone shows nothing — use
`\dt "Xpense".*`.

## Tests

Integration tests run against **real Postgres in Testcontainers**, not SQLite. This costs about
six seconds a run and buys the thing SQLite could not give: the provider under test is the
provider that ships.

That matters most for timestamps. Npgsql maps `DateTime` to `timestamp with time zone` and
**throws** on any `DateTimeKind` other than `Utc`. SQLite accepted anything, so the whole class
of bug the UTC work addressed was invisible in tests before.

The shape:

- `PostgresFixture` is a `[SetUpFixture]` in the `Xpense.Tests.Integration` namespace. It starts
  one container per run, creates a template database and applies the real migrations to it once.
  A broken migration therefore fails the whole integration suite immediately and obviously.
- Each test gets its own database via `CREATE DATABASE ... TEMPLATE`, which is a file copy and
  costs milliseconds. Re-running migrations per test would dominate the suite.
- Unit and architecture tests never touch it — the fixture is namespace-scoped, so no container
  starts when only those run.

CI needs a Docker daemon. GitHub Actions `ubuntu-latest` has one, so the workflow is unchanged.

## Two things that bit during the port

**Version conflict.** Npgsql 10.0.3 floors `Microsoft.EntityFrameworkCore.Relational` at
`[10.0.4, 11.0.0)`, so NuGet resolved 10.0.4 while the core package was pinned at 10.0.10. That
built with `MSB3277` and then failed at runtime with a missing
`Microsoft.EntityFrameworkCore.Relational` assembly. `Relational` is now pinned explicitly to
match.

**UTC on write.** The value converter previously passed `Unspecified` timestamps through
unchanged, which SQL Server accepted and Npgsql rejects. It now normalises: `Utc` passes,
`Local` is converted, and `Unspecified` is *tagged* rather than converted — in this codebase an
untagged value already means UTC, so calling `ToUniversalTime()` on it would shift it by the
server's offset.
