# Trade-offs

Written after doing the migration, not before. Some of this only became visible on the way through.

## What got better

**Adding an endpoint is one file.** It was eight, across two projects, one of which was a 143-line DI registration file you had to remember to edit.

**Deleting a feature is deleting a file.** The layered version made orphans easy to leave behind — the migration found four response types with zero references and a `GetAccountByNumberUseCase` that no controller called but that was still registered in DI.

**Wrong code is harder to write.** Three status-code bugs existed because the mapping lived far from the thing it described: category create *and* update failures both returned 404, and account creation failure returned 400. With one handler per exception type, the mapping is stated once.

**Fewer moving parts.** 31 hand-written `AddScoped` registrations became one open generic. 4 use-case marker interfaces and 9 single-implementation repository interfaces are gone.

## What got worse

**Duplication is real.** Five slices build a similar `Ok(...)` projection. Two write near-identical colour validation. This is the deliberate trade — independence over DRY — but it is a genuine cost, and it grows with the number of CRUD-shaped resources.

Judgement is still required about *which* duplication to keep. Currency parsing was duplicated across the two money slices and got pulled down into `Xpense.Domain.Enums.CurrencyParser`, because it encodes a real rule (reject numeric input, which `Enum.TryParse` would otherwise accept — a client could post `"currency": "0"` and silently get EUR). Similar-looking EF queries stay duplicated. The line is whether the duplicated thing is a *rule* or a *shape*.

**The compiler no longer enforces layering — so we put the enforcement back by hand.** Previously `Xpense.Domain` (then `Xpense.Services`) *could not* reference the API project; the build stopped you. Nothing in the type system stops a slice importing another slice.

`Xpense.Tests/Architecture/SliceIsolationTests.cs` closes that hole: it reads the compiled assembly with Mono.Cecil and fails if any `Features.X` type references a `Features.Y` type, or if a slice catches a `Xpense.Domain.Exceptions` type. It is a test rather than a compiler rule, so it fails a build later than the compiler would — but it fails one.

It earned its keep immediately: it caught `Analytics` reaching into `Categories.CategoryResponse`, which is why cross-feature response contracts now live in `Xpense.API/Contracts/`.

**Routes are not statically traceable.** "Find all references" no longer answers "what serves `POST /api/v1/tags`". Discovery is a reflection scan, so the answer comes from grepping the route string. `WithName(nameof(CreateTag))` mitigates this a little.

**Cross-cutting changes touch more files.** Adding auth metadata per endpoint is N edits where a controller base class was one. Middleware-level concerns are unaffected; per-endpoint metadata is not.

## When this is the wrong choice

Be honest about the shape of the work:

- **Mostly CRUD over many similar resources** → layering shares that shape once. Slices will copy it per resource. Xpense has five CRUD-ish resources today, and the duplication above is the visible cost of that.
- **A large team with strict boundaries** → compiler-enforced project references are worth more than convention.
- **A genuinely rich domain** → most of the code is domain logic, and where the *endpoint* lives stops being the interesting question.

Xpense's planned work is distinct features — budgets, forecasting, receipt OCR, bank sync — rather than more CRUD, which is what makes slices the better fit here.

## Gaps this migration introduced, and how they were closed

All of these were real at the point the slices first landed. Listed with their resolution rather than deleted, because the sequence is the useful part.

| Gap | Resolution |
|---|---|
| Rollback-on-save-failure lost its test when the injectable repository went | `FailOnSaveInterceptor<T>` fails persistence one level lower; the test asserts 500 with no balance change and no transfer or leg rows |
| `TryParseCurrency` duplicated across two slices | Moved to `Xpense.Domain.Enums.CurrencyParser` — it is a rule, not a shape |
| No enforcement of slice isolation | `SliceIsolationTests` reads the IL and fails on cross-slice references or domain-exception catches |
| `POST /api/v1/transfers` returned 201 with no `Location` | `GET /api/v1/transfers/{id}` added, so the header points somewhere real — and a test follows it |
| `Dependencies/DependencyValidation1.layerdiagram` meaningless under slices | Deleted with its `AdditionalFiles` entries |
| `docs/api/v1.0/v1.0.yaml` described the pre-v1 contract | Deleted; Swashbuckle generates the spec from the code |

## Still open

- **Cross-currency transfers move unit-less decimals.** `Account.Balance` has no currency, so a USD transfer between EUR accounts succeeds. Whether Xpense is multi-currency is a product decision, not a bug fix.
- **Responses return Unix seconds** where [`docs/contract/api-v1-contract-design.md`](../contract/api-v1-contract-design.md) specifies ISO 8601, and account balances are bare decimals where it specifies `{cents, currency}`.
- **Concurrent transfers are untested.** Integration tests now run on real Postgres, so the `Serializable` isolation the transfer requests is actually honoured rather than quietly ignored by SQLite. What is still missing is a test that runs two transfers against the same account at once to prove no update is lost. `Account` also has no rowversion.
