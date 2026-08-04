# Postgres

Xpense runs on PostgreSQL via Npgsql. It was previously SQL Server with SQLite in tests.

## Running locally

```bash
docker compose up -d                                   # postgres:17-alpine on :5432
dotnet run --project src/Xpense/Xpense.API             # migrates on startup, then serves :4000
```

`appsettings.Development.json` points at the compose database. Other environments supply
`ConnectionStrings__DefaultConnection` as an environment variable — nothing else ships a
connection string, so a missing one fails loudly instead of silently reaching localhost.

Startup calls `Database.Migrate()`, not `EnsureCreated()`. `EnsureCreated` builds the schema
without recording migration history, which then makes `dotnet ef database update` fail against
that database.

## Migrations

The four SQL Server migrations could not be replayed on Postgres — they emit `nvarchar`,
`datetime2` and SQL Server identity — so they were deleted and replaced by a single
`InitialCreate`. Safe because nothing was deployed and the dev connection string was still a
placeholder.

```bash
cd src/Xpense
dotnet ef migrations add <Name> --project Xpense.Persistence --startup-project Xpense.Persistence
dotnet ef database update      --project Xpense.Persistence --startup-project Xpense.Persistence
```

`XpenseContextFactory` supplies the design-time connection string. It is only opened for
`database update`; scaffolding a migration just needs it to parse.

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
