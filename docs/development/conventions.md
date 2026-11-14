# Coding conventions

## Scope

Paths in these API rules are relative to `api/`. The web client uses TypeScript in strict mode; `npm run build` is its static check. The client has no linter.

Every `docker/*/Dockerfile` restores the whole solution, so adding a project means adding its `.csproj` to each of them. Miss one and all three builds fail, not just the new one.

Migrations never run at application startup, and reference data lives in migrations rather than in a
startup seeder. Both are load-bearing: see [ADR 0004](../decisions/0004-migrations-are-a-deployment-step.md)
and [ADR 0005](../decisions/0005-reference-data-lives-in-migrations.md) before adding code that writes to
the database during boot.

## API slice rules

These are enforced by `Xpense.Tests/Architecture/SliceIsolationTests.cs`. Breaking one fails the build, not just review.

1. **One endpoint per file**, holding its route, request, validator and handler.
2. **Slices never reference each other.** Not the request, not the handler, not the response. Anything shared moves to `Xpense.API/Contracts/` (HTTP contracts) or `Xpense.Domain` (domain concepts).
3. **Slices never catch domain exceptions.** Throw; `ExceptionHandlers/` owns the HTTP mapping. A `try/catch` for a domain exception in a slice is a bug.
4. **Endpoints implement `IEndpoint`** with a `public static void Map(IEndpointRouteBuilder)` and live under `Features/`. Discovery is a startup scan — there is no registration step.
5. **Authorization is explicit in each query.** Do not add global authorization query filters. Start every decision from the current user, correlate the group, membership, grant and resource type in the same SQL query, and return the approved neutral 404 when a caller must not learn whether a private resource exists. Feature code never returns 403.

`GET /health` is the one deliberate exception: it is mapped directly in `Program.cs` because it is infrastructure, not a feature. It has no request, no contract to version and no slice to belong to. The architecture tests allow it — they constrain types under `Xpense.API.Features.*` and types implementing `IEndpoint`, and it is neither. Do not treat it as precedent for a second one.

## Events and notifications

Producers state facts and never decide what is worth telling anyone. `IEventBus.Emit` inserts an event through the caller's `DbContext`, so it commits with whatever the caller is writing -- it does **not** save, and a caller that never saves has emitted nothing.

- **The `Events` table is the queue.** No broker. [ADR 0008](../decisions/0008-the-events-table-is-the-queue.md).
- **Budgets never touch the write path.** [ADR 0006](../decisions/0006-a-budget-reports-and-never-blocks.md).
- **One notification kind per rule**, in its own file under `Xpense.Notifications/Rules/`, discovered by a scan. A rule may not reference another rule, and rules duplicate each other's queries on purpose -- `NotificationRuleIsolationTests` enforces both.
- **Event bodies hold primitives, enums and ids only.** No entity, no `Money`. A body is a wire format read back long after it was written; `EventContractTests` enforces this.
- **A rule's payload must contain nothing time-varying.** It is hashed to deduplicate notifications, so a timestamp inside one makes every redelivery look new.

## Pinned packages

Three versions are held deliberately. Bumping any of them needs a reason.

- **FluentAssertions stays on 6.x.** Version 8 moved to a commercial Xceed licence.
- **`Microsoft.EntityFrameworkCore.Relational` is pinned explicitly.** Npgsql floors it at `[10.0.4, 11.0.0)`, so without a pin NuGet resolves 10.0.4 against a 10.0.10 core and the app fails at runtime with a missing assembly. See [`docs/operations/database.md`](../operations/database.md).
- **`dotnet-ef` is a local tool** in `.config/dotnet-tools.json`, matching the EF runtime. A globally installed 8.x tool refuses to run against EF 10.

## Documentation style

- Put prose under descriptive headings. Use Markdown lists, tables, and diagrams when they make the content easier to scan.

## Code style

- **No comments.** Not in C#, Dockerfiles, YAML, XML or shell. The one exception is `<summary>` on `*Request` and `*Response` records, because Swagger renders those into the OpenAPI document that [ADR 0003](../decisions/0003-generated-openapi-is-the-contract.md) makes authoritative. Write code that says what it does and put reasoning in the commit message, an ADR, or `docs/`. Parser directives that only look like comments stay: `# syntax=` in a Dockerfile, `#!` in a script.
- **Spell names out.** `cancellationToken`, not `ct`. `dbContext`, not `db`. `httpContext`, not `http`. Lambda parameters are named for what they hold, and for *their own* type -- in relationship configuration the two sides differ, so `HasOne(transaction => transaction.Category).WithMany(category => category.Transactions)`. `app` in `Map(IEndpointRouteBuilder app)` is the one exception, being the framework's own name for that thing.
- **Constants first.** `const` and `static readonly` go at the top of the type, before fields, properties and methods.
- **Validation messages are prose, not identifiers.** "The category must be a valid selection", never "The categoryId must reference an existing category". The ProblemDetails error dictionary is already keyed by the camelCase field, which is how a client attaches an error to an input, so the message is free to read like a sentence. Domain words stay: "minor units" is the term, per [`docs/domain/glossary.md`](../domain/glossary.md).

## API conventions

- **Requests nest inside their slice** (`CreateTag.Request`), even when two look alike today. Responses are shared per feature or in `Contracts/`, because a resource should look the same however you fetched it.
- **Return `TypedResults`**, not `IActionResult`. The concrete return type is the OpenAPI description.
- **Creates return an absolute `Location`** via `HttpContext.ResourceUri(path)`. `TypedResults.Created` emits a relative header if you hand it a bare path.
- **Resource deletes are soft** — `MarkAsDeleted()` + `Touch()`. A global visibility filter may hide ordinary deleted resources, but it is never an authorization boundary. Memberships, grants and invitations use their explicit revoked state and retain history; do not force them through `MarkAsDeleted()`. Do not use `Remove` for user data.
- **Money is `{cents, currency}` on the wire** via `Contracts/MoneyResponse`, and `Money` in the domain. Accounts are denominated in one currency and nothing converts — a mismatched amount is a 400, never a conversion. See [`docs/domain/money.md`](../domain/money.md).
- **Timestamps are UTC.** `DateTime.UtcNow` everywhere; a value converter in `XpenseDbContext` tags reads as UTC.
- **Validation is FluentValidation only.** Do not add DataAnnotations — two validation systems produce two error shapes.

## Duplication is deliberate

Slices trade DRY for independence. Two slices with similar EF queries or similar projections is the intended state, not debt. **Do not extract a shared abstraction just because two slices look alike** — that undoes the architecture. Extract only when the logic is genuinely domain logic (invariants, money, rules that hold regardless of caller), and then it goes in `Xpense.Domain`.


## Commits

Do not add a `Co-Authored-By` trailer to commits.
