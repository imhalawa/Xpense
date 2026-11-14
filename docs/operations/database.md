# PostgreSQL

PostgreSQL stores Xpense data. Migrations are a deployment step; the API and worker do not apply them at startup. Reference data is applied by migrations.

## Run locally

From `api/`:

```bash
cp .env.example .env
docker compose up -d postgres
cd src/Xpense
dotnet tool restore
dotnet dotnet-ef database update --project Xpense.Persistence --startup-project Xpense.Persistence
dotnet run --project Xpense.API
```

The development settings point to the Compose database. Other environments provide `ConnectionStrings__DefaultConnection`; Compose supplies it to the containers. Tables use the `Xpense` schema.

## Migrations

From `api/src/Xpense/`:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project Xpense.Persistence --startup-project Xpense.Persistence
dotnet dotnet-ef database update --project Xpense.Persistence --startup-project Xpense.Persistence
```

Use the locally pinned `dotnet-ef` tool. Its version must match the EF runtime. The design-time factory uses a host connection; the container migration bundle receives its connection explicitly. Do not use `EnsureCreated()` in place of migrations: it does not record migration history.

## Tests and provider requirements

- Integration tests use PostgreSQL through Testcontainers and require Docker. They migrate a template database and create an isolated database for each test.
- The relational EF package is pinned to match the core package and avoid runtime assembly mismatches.
- PostgreSQL timestamps require UTC. The value converter passes UTC through, converts local values, and treats unspecified values as UTC.

See [deployment](deployment.md) for backups and restore.
