# Migration log

What actually happened, in order, and what it cost.

## Result

| Project | Before | After |
|---|---|---|
| `Xpense.API` | 45 files / 1,695 lines | 42 files / 1,921 lines |
| `Xpense.Services` → `Xpense.Domain` | 78 files / 1,347 lines | 29 files / 537 lines |
| `Xpense.Persistence` | 20 files / 396 lines | 12 files / 396 lines |
| `Xpense.Tests` | 7 files / 1,180 lines | 5 files / 1,012 lines |
| **Total** | **150 files / 4,918 lines** | **88 files / 3,866 lines** |

**62 fewer files (−41%), 1,052 fewer lines (−21%)**, for 21 endpoints across 28 feature files.

The API project grew slightly in lines while shrinking in files: endpoint code that used to be spread across two projects now sits in one, and slices carry their own request and validator.

## Order of work

Smallest feature first, so the pattern was proven cheaply before touching money.

1. **Infrastructure** — `IEndpoint`, discovery scan, validation filter
2. **Tags** — five endpoints, the smallest full CRUD set
3. **Categories, Accounts, Merchants, Analytics**
4. **Transactions, Transfers** — last, because they carry the real logic
5. **Delete the old layers** — controllers, use cases, repositories, marker interfaces
6. **Rename** `Xpense.Services` → `Xpense.Domain`, `Extensions.cs/` → `Extensions/`

Controllers and slices ran side by side in the same app for steps 2–4. There is no bridging needed; both routing systems coexist.

## The safety net

`ApiEndpointTests` is route-level: it makes HTTP calls and asserts status codes and response bodies. It cannot tell whether a controller or a minimal-API endpoint served the request.

**It passed unchanged through the entire migration.** That is the single most useful thing about how this was sequenced — every step was verifiable against tests that had no opinion about the architecture.

Test counts: 54 passed / 2 skipped before, 52 passed / 0 skipped after. The difference is 10 removed (4 DTO-mapping cases whose subject no longer exists, 4 database-backed transfer tests, 2 empty `[Ignore]` placeholders) and 6 added (`MoneyTransferTests`, which needs no database).

## Bugs found on the way through

Porting code forces you to read it.

- **Creating the very first account crashed.** `GetNextAccountNumber` called `Max()` over an empty sequence. Tests never caught it because they always seeded an account first.
- **`UpdateTagUseCase` dereferenced a missing entity** without a null check — `NullReferenceException` instead of a 404.
- **`CategoryRepository.DeleteById` wrapped everything in a catch-all** and rethrew as `CategoryNotFoundException`, so an unrelated failure would have been reported as a missing category.
- **Dead code**: four response types with no references, `GetAccountByNumberUseCase` registered in DI but called by nothing, three unused request DTOs, `PaginatedResult`, `TodayExpensesByCategory`, `ExpensesByCategory`, and the `AccountType` enum.

## Surprises

- **`TypedResults.Created` emits a relative `Location`** where `CreatedAtAction` emitted an absolute one. Caught by two tests. Preserved the old behaviour via `HttpContext.ResourceUri` rather than editing the tests to match new code.
- **Dropping MVC removed CORS.** `AddControllers()` registers `ICorsService` implicitly; `UseCors` then failed at startup.
- **`git mv` of the project directory failed atomically** because the index still held deleted files, but the `sed` that rewrote namespaces had already run — leaving references pointing at a directory that did not exist yet. Plain `mv` finished it.

## Still open

- `docs/api/v1.0/v1.0.yaml` describes the pre-v1 contract and is now a third source of truth
- `Dependencies/DependencyValidation1.layerdiagram` is meaningless under slices
- No `GET /api/v1/transfers/{id}`, so transfer creation returns 201 without a `Location`
