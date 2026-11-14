---
status: accepted
date: 2026-08-05
---

# Migrations are a deployment step

## Decision

Apply migrations in a separate, one-shot migration service before starting the application. Application startup does not run migrations.

## Context

Startup migrations can race across instances, combine schema and code rollout, and hide migration failures in application crash loops. A separate step has its own logs and exit status and supports staged schema changes. The bundle is self-contained.

## Alternatives considered

- **Migrate at startup:** rejected because instances can race and rollout cannot be staged independently.
- **Migrate in the API entrypoint:** rejected for the same reasons.
- **Run migrations only by hand:** rejected because a normal Compose startup could reach an unprepared database.

## Consequences

Compose gates application startup on successful migrations. Schema changes require the correct migration image and, for staged changes, ordered deployments. The container bundle receives its connection explicitly; host development must apply migrations before running the API.
