# Working in this repo

Xpense.API uses **vertical slice architecture**. Full write-up in
[`docs/vertical-slicing-architecture/`](docs/vertical-slicing-architecture/).

## Layout

```
src/Xpense/
  Xpense.API/
    Features/<Feature>/<Endpoint>.cs   one endpoint per file
    Contracts/                         response contracts shared by 2+ features
    Infrastructure/                    IEndpoint, discovery, validation filter
    ExceptionHandlers/                 one handler per exception type -> RFC 7807
  Xpense.Domain/                       entities, value objects, enums, exceptions, MoneyTransfer
  Xpense.Persistence/                  DbContext, type configuration, migrations, OptionResolver
  Xpense.Tests/                        ApiEndpointTests (canonical), Unit, Architecture
```

## Rules

These are enforced by `Xpense.Tests/Architecture/SliceIsolationTests.cs`. Breaking one fails the build, not just review.

1. **One endpoint per file**, holding its route, request, validator and handler.
2. **Slices never reference each other.** Not the request, not the handler, not the response. Anything shared moves to `Xpense.API/Contracts/` (HTTP contracts) or `Xpense.Domain` (domain concepts).
3. **Slices never catch domain exceptions.** Throw; `ExceptionHandlers/` owns the HTTP mapping. A `try/catch` for a domain exception in a slice is a bug.
4. **Endpoints implement `IEndpoint`** with a `public static void Map(IEndpointRouteBuilder)` and live under `Features/`. Discovery is a startup scan — there is no registration step.

## Conventions

- **Requests nest inside their slice** (`CreateTag.Request`), even when two look alike today. Responses are shared per feature or in `Contracts/`, because a resource should look the same however you fetched it.
- **Return `TypedResults`**, not `IActionResult`. The concrete return type is the OpenAPI description.
- **Creates return an absolute `Location`** via `HttpContext.ResourceUri(path)`. `TypedResults.Created` emits a relative header if you hand it a bare path.
- **Deletes are soft** — `MarkAsDeleted()` + `Touch()`. A global query filter hides the rows. Do not use `Remove`.
- **Timestamps are UTC.** `DateTime.UtcNow` everywhere; a value converter in `XpenseDbContext` tags reads as UTC.
- **Validation is FluentValidation only.** Do not add DataAnnotations — two validation systems produce two error shapes.

## Duplication is deliberate

Slices trade DRY for independence. Two slices with similar EF queries or similar projections is the intended state, not debt. **Do not extract a shared abstraction just because two slices look alike** — that undoes the architecture. Extract only when the logic is genuinely domain logic (invariants, money, rules that hold regardless of caller), and then it goes in `Xpense.Domain`.

## Before you finish

```bash
dotnet build src/Xpense/Xpense.sln
dotnet test  src/Xpense/Xpense.sln
```

The build is warning-free; keep it that way. `ApiEndpointTests` is route-level and is the safety net for any refactor — it should not need editing when you move code around, only when you deliberately change the HTTP contract.

Do not add a `Co-Authored-By` trailer to commits.
