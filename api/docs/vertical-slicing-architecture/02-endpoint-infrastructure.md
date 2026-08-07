# Endpoint infrastructure

Three small pieces hold the slices together. All of them live in `Xpense.API/Infrastructure/`.

## Discovery

```csharp
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
```

`MapEndpoints()` scans the assembly for implementors and invokes each `Map`. That is the entire registration story.

Why `static abstract` rather than an instance method: slices hold no state and have no dependencies, so instantiating them would be pointless work and would need a DI registration per slice — the exact per-feature bookkeeping this migration removed. The scan is ordered by full type name so route registration is deterministic across runs.

The scan throws if a type implements `IEndpoint` without a public static `Map`. That turns an easy mistake into a startup failure instead of a silently missing route.

## Validation

`ValidationEndpointFilter` resolves `IValidator<T>` for each handler argument, runs it, and **throws** `ValidationException` on failure rather than short-circuiting with a result.

Throwing is deliberate: validation failures then travel the same path as domain failures and are formatted by the same `ValidationExceptionHandler`, so there is exactly one place that decides what a 400 looks like.

Opt in per slice with `.Validated()`:

```csharp
app.MapPost("/api/v1/tags", Handle).WithName(nameof(CreateTag)).Validated();
```

It is opt-in rather than global because read endpoints have nothing to validate, and an empty filter on every GET is cost with no benefit.

## Error handling

Unchanged by this migration, and deliberately so. `ExceptionHandlers/` holds one `IExceptionHandler` per exception type, mapping to RFC 7807 problem details:

| Exception base | Status |
|---|---|
| `NotFoundException` | 404 |
| `DomainRuleViolationException` | 400 |
| `PersistenceFailedException` | 500, generic detail, cause logged |
| FluentValidation `ValidationException` | 400 with a per-field `errors` object |
| anything else | 500, generic detail, cause logged |

`InsufficientFundsForTransferException` registers ahead of its `DomainRuleViolationException` base so it can report a field error against `amount.cents` instead of a flat problem detail.

**Slices never catch domain exceptions.** A `try/catch` in a slice means the HTTP contract is being decided in two places.

## Created responses

`HttpContext.ResourceUri(path)` builds the absolute URL for a `201`.

This exists because of a real behaviour difference: MVC's `CreatedAtAction` emitted an absolute `Location` header, while `TypedResults.Created` emits whatever string it is handed — so passing a path yields a *relative* header. Both are legal under RFC 9110, but changing it would have been a silent contract change, so every create slice goes through the helper rather than each one deciding.

## What was removed

- `Microsoft.AspNetCore.Mvc.NewtonsoftJson` — minimal APIs use System.Text.Json, which is camelCase by default and matches the existing contract
- `Asp.Versioning.Mvc` — the version is in the route path (`/api/v1/...`); a header-based version reader was configured but nothing used it
- `AddControllers()` / `MapControllers()`

One gotcha from dropping MVC: `AddControllers()` had been registering the CORS services implicitly. `UseCors` then failed at startup with `Unable to resolve service for type 'ICorsService'`. `builder.Services.AddCors()` is now explicit.
