# Vertical Slicing Architecture

How Xpense.API is organised, and why.

## The idea in one sentence

A feature's route, request shape, validation and handler live in **one file**, because those four things change together.

The name for this is **Vertical Slice Architecture**. If you have written React, you already know the shape: a component colocates its markup, styles, state and event handlers rather than scattering them into `templates/`, `styles/` and `handlers/` folders. What React keeps shared is the design system and the domain types. What we keep shared is entities, the `DbContext`, and error handling.

## Why we moved

The previous structure was layered: `Xpense.API` (controllers, DTOs, validators) over `Xpense.Services` (use cases, commands, exceptions) over `Xpense.Persistence` (repositories, EF). Adding one endpoint meant touching **eight files across two projects**:

| File | Lines |
|---|---|
| `Controllers/AccountController.cs` | 66 |
| `Models/Requests/CreateAccountRequest.cs` | 13 |
| `Models/Responses/AccountResponse.cs` | 39 |
| `Models/Validators/AccountRequestValidators.cs` | 27 |
| `Features/Accounts/Commands/CreateAccountCommand.cs` | 3 |
| `Features/Accounts/Usecases/CreateAccountUseCase.cs` | 32 |
| `Exceptions/AccountExceptions.cs` | 30 |
| `Extensions.cs/IoC.cs` (registration) | 143 |

The layering was costing more than it returned. Measured before the migration:

- `Xpense.Services` held **78 files for 1,347 lines** — about 17 lines per file
- **41 of those files were under 15 lines**
- **31 hand-written `AddScoped` registrations**, one per use case, each a step you could forget
- **4 use-case marker interfaces** (`ICommandHandler`, `ICommandResultHandler`, `IQueryHandler`, `IQueryParamHandler`) with one method each — nothing dispatched on them, nothing implemented two of them
- **9 repository interfaces, each with exactly one implementation** — no second provider, no polymorphism, no test double that the SQLite integration host doesn't already give us

None of that is abstraction. It is indirection with an abstraction's file count.

## What this is not

This is **not** "no architecture". Slices are a statement about *where code lives*, not about whether design matters. Three rules keep it from degrading:

1. **A slice owns its endpoint, not the domain.** Money movement, invariants and anything with real rules stays in the domain layer and gets called by the slice. `MoneyTransfer` is the worked example — see [03-what-stays-shared.md](03-what-stays-shared.md).
2. **Slices do not call each other.** If two slices need the same thing it moves down — to `Xpense.API/Contracts/` for a shared HTTP contract, or to `Xpense.Domain` for a shared rule. It never becomes slice-to-slice.
3. **Duplication is allowed, and is the point.** Two slices writing similar EF queries is cheaper than one shared query that four features are afraid to change. This is a deliberate trade of DRY for independence.

Rules 2 and 3 are not honour-system: `Xpense.Tests/Architecture/SliceIsolationTests.cs` reads the compiled IL and fails the build on a cross-slice reference or a domain-exception `catch` inside a slice. That is the replacement for the compiler-enforced project boundary this architecture gives up — see [04-trade-offs.md](04-trade-offs.md).

## Where things live

```
src/Xpense/
  Xpense.API/
    Infrastructure/        IEndpoint, endpoint discovery, validation filter
    ExceptionHandlers/     one handler per exception type -> RFC 7807
    Contracts/             response contracts shared by 2+ features
    Features/
      Tags/
        TagResponse.cs     response contract for this feature
        TagRules.cs        validation shared by CreateTag and UpdateTag
        ListTags.cs        one endpoint = one file
        GetTagById.cs
        CreateTag.cs
        UpdateTag.cs
        DeleteTag.cs
      Accounts/ ...
  Xpense.Domain/           entities, value objects, enums, exceptions, MoneyTransfer
  Xpense.Persistence/      DbContext, type configuration, migrations, OptionResolver
```

Repo-root [`AGENTS.md`](../../AGENTS.md) carries the same rules in the place agents read first.

## Reading order

1. [01-anatomy-of-a-slice.md](01-anatomy-of-a-slice.md) — what a slice file contains and why
2. [02-endpoint-infrastructure.md](02-endpoint-infrastructure.md) — discovery, validation, error handling
3. [03-what-stays-shared.md](03-what-stays-shared.md) — the line between slice and domain
4. [04-trade-offs.md](04-trade-offs.md) — honest costs, and when this is the wrong choice
5. [05-migration-log.md](05-migration-log.md) — what changed, what broke, what we learned
6. [06-ai-assisted-development.md](06-ai-assisted-development.md) — how this structure behaves with an AI agent in the loop, versus the layered one
