---
status: accepted
date: 2026-08-05
---

# Reference data lives in migrations

## Decision

Declare the five priority rows as fixed reference data and apply them through a migration. Remove the startup seeder and its duplicate JSON files.

## Context

The seeder read a source file that was not present in the published container. Its count-based check could also race between instances. Two JSON copies had already diverged. Fixed reference data belongs with the schema and its migration history.

## Alternatives considered

- **Read the embedded resource:** rejected because it retained startup writes and the race.
- **Copy the files to the image:** rejected because it retained the duplicate data and startup behavior.

## Consequences

Changing a priority requires a migration. Seed timestamps must be deterministic. The migration advances the priority identity sequence after inserting fixed IDs. Application startup no longer seeds the database.
