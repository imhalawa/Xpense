---
status: accepted
date: 2026-08-05
---

# Migrations are a deployment step, not a startup step

`Program.cs` does not call `Database.Migrate()`. Schema changes are applied by a separate
one-shot `migrations` container, built from `docker/migrations/Dockerfile`, which runs an EF
migration bundle against the database and exits. The `api` service starts only once that
container has exited successfully — `docker-compose.yml` gates it with
`condition: service_completed_successfully`.

## Why

An application that migrates itself on boot applies schema changes from whichever instance
starts first. With one instance that is invisible; with two it is a race, and the loser either
fails or applies a partial change. Nothing about the code says which instance wins.

Worse, it collapses two steps that need to stay separate. An expand/contract change is
deliberately ordered: widen the schema, deploy code that tolerates both shapes, then narrow.
An app that migrates itself makes the schema change and the code change the same event, so
there is no way to stage them and no way to hold one back.

Failure mode matters too. A bad migration applied at boot produces a crash-looping application
and an error buried in application logs. The same migration applied as its own step produces a
step that failed, with its own exit code and its own log, and an application that was never
started against a half-changed schema.

The bundle is self-contained, so the deploy needs neither the .NET SDK nor the source tree —
only the binary and a connection string.

## Considered options

**`Database.Migrate()` at startup.** What this repo did until the Postgres port, and what
`docs/postgres.md` described for longer than it was true. It is one line and needs no
infrastructure, which is exactly why it survives in codebases past the point where it is safe.
Rejected for the reasons above.

**An entrypoint script in the API image that migrates, then starts Kestrel.** Fewer moving
parts in compose, and the ordering is guaranteed within one container. Rejected because it is
startup migration with an extra file: every replica still races, and the failure is still a
crash loop.

**Migrations only ever run by hand.** Keeps the strongest possible gate. Rejected because
`docker compose up` would then start an API against a schema-less database, and the resulting
failure is a runtime exception rather than a message telling you what you forgot.

## Consequences

`docker compose up` now applies migrations as part of coming up, so the gate has moved: it is
no longer "when do you run migrations" but "which image do you deploy". Staging an
expand/contract change means two deploys in order, not one deploy and a decision.

`XpenseContextFactory` hardcodes `Host=localhost` for design time. Inside a container that
resolves to the container itself, so the bundle is handed `--connection` explicitly rather than
being allowed to fall back.

Local development without containers has to apply migrations before `dotnet run`, because
nothing else will. `README.md` and `docs/postgres.md` say so.
