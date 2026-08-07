---
status: accepted
date: 2026-08-05
---

# Reference data lives in migrations, not a runtime seeder

The five **Priority** rows are declared with `HasData` in `PriorityEntityTypeConfiguration` and
inserted by the `SeedPriorities` migration. `Xpense.API/Extensions/Seeder.cs`,
`Xpense.API/Seeds/Priorities.json` and the unused `Xpense.Persistence/Seeds/PrioritySeeds.json`
are deleted, along with the seeding block in `Program.cs`.

## Why

Containerizing exposed the seeder as broken. `Xpense.API.csproj` removed
`Seeds\Priorities.json` from `Content` and added it as an `EmbeddedResource`, so the file never
reached the build output — `bin/Debug/net10.0` contained no `Priorities.json`. But
`Seeder.LoadData` read it from disk with `File.ReadAllText("Seeds/Priorities.json")`. It worked
only because `dotnet run` sets the working directory to the project folder, where the file
happens to sit in source. A published image has `/app` and no `Seeds/` directory, so the first
container boot would have thrown `FileNotFoundException` before serving a request.

That was the trigger, not the argument. The argument is that the seeder was never a seeder:

- Its idempotency check was `dbSetCount >= dataCount`. Two instances starting together both
  read zero rows and both insert, because nothing serializes them.
- It wrote to the database on every boot, which is the same objection
  [ADR 0004](0004-migrations-are-a-deployment-step.md) makes about migrating on boot.
- The data was duplicated in two JSON files that had already drifted — one with `Id` values and
  one without — and only one was referenced by any code.

Reference data with fixed identifiers has the same lifecycle as the schema that constrains it.
`HasData` puts it there, applied by the same gated step, recorded in the same history, and
verified by the same integration tests that already run the real migrations.

## Considered options

**Fix the loader to read the embedded resource.** Three lines, and the csproj is already set up
for it. Rejected because it preserves the boot-time write and the racy check, and keeps roughly
seventy lines doing what one `HasData` call does.

**Copy `Seeds/` into the build output.** The smallest possible change. Rejected because it
carries every existing problem into production and keeps two copies of the same five rows.

## Consequences

Changing a Priority is now a migration rather than a file edit. For five rows of reference data
that have not changed since they were written, that is the correct amount of friction.

`CreatedAt` is a hard-coded literal. `HasData` with `DateTime.UtcNow` makes the model snapshot
non-deterministic, so `dotnet ef migrations add` would emit a spurious migration on every run.

`Priorities.Id` is `IdentityByDefaultColumn`, so inserting explicit Ids is allowed but leaves
the identity sequence at 1. The migration restarts it at 6. Nothing creates a **Priority** at
runtime today — there is no `Features/Priorities` slice — but a future one would otherwise
collide on the primary key, and the error would point at the wrong thing.

Nothing writes to the database during application startup any more.
