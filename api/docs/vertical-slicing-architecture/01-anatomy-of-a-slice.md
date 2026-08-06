# Anatomy of a slice

One endpoint, one file. Here is the whole of `Features/Tags/CreateTag.cs`, annotated.

```csharp
public sealed class CreateTag : IEndpoint
{
    // 1. The request. No separate Command type -- the request IS the input.
    public sealed record Request(string Label, string BgColorHex, string FgColorHex);

    // 2. Validation, next to the shape it validates.
    public sealed class Validator : AbstractValidator<Request> { ... }

    // 3. The route.
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/tags", Handle).WithName(nameof(CreateTag)).Validated();

    // 4. The handler.
    private static async Task<Created<TagResponse>> Handle(
        Request request, XpenseDbContext dbContext, CancellationToken cancellationToken) { ... }
}
```

## Why each part is there

**`sealed class`, not `static class`.** A static class cannot implement an interface, and `IEndpoint` is how discovery finds the slice. Nothing is ever instantiated — `Map` is `static abstract` on the interface — so the class is only a namespace with a contract attached.

**Nested `Request`, not a shared DTO.** `CreateTag.Request` and `UpdateTag.Request` are different types even when their fields match today, because they will drift. Nesting also means you never invent names like `CreateTagRequestV2`; the enclosing slice already disambiguates.

**No `Command` type.** The old code mapped `CreateTagRequest` → `CreateTagCommand` → `Tag`. The command existed to carry data across a project boundary that no longer exists. Two of the three hops were pure ceremony; `CreateAccountCommand.cs` was three lines long.

**`Validator` nested in the slice.** `AddValidatorsFromAssemblyContaining` finds nested types fine. Colocating means a new field gets its rule added in the same edit, in the same file, rather than in a `Validators/` folder you have to remember exists.

**`private static` handler.** Private because nothing outside the slice may call it — that is rule 2 from the [README](README.md). Static because it closes over nothing; every dependency arrives as a parameter, which is also what makes it trivially readable.

**Dependencies as handler parameters.** Minimal APIs resolve `XpenseDbContext` and anything else from DI per-parameter. No constructor, no fields, no DI registration for the slice itself.

## Response types

Return `TypedResults`, not `IActionResult`:

```csharp
private static async Task<Created<TagResponse>> Handle(...)
```

The concrete return type is the OpenAPI description — no `[ProducesResponseType]` needed for the success path. Error responses are not declared per-endpoint because they are produced centrally by the exception handlers.

## What a slice must not do

- **Reference another slice.** Not its `Request`, not its handler, not its validator.
- **Catch domain exceptions.** Throw; the handlers in `ExceptionHandlers/` own the HTTP mapping. A `try/catch` in a slice is a bug.
- **Contain money or invariant logic.** That belongs in the domain. See [03-what-stays-shared.md](03-what-stays-shared.md).

## Shared-per-feature contracts

`TagResponse` lives beside the slices in `Features/Tags/`, not inside one of them, because five endpoints return it. The rule of thumb:

| Used by | Where it lives |
|---|---|
| One slice | Nested inside that slice |
| Several slices in one feature | A file in that feature folder |
| Several features | `Xpense.Domain` |

Responses are shared more readily than requests. Requests diverge (create takes a label, update takes a label and an id); responses are the contract clients read, and a resource should look the same however you fetched it.
