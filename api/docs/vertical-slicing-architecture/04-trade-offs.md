# Trade-offs

Written after doing the migration, not before. Some of this only became visible on the way through.

## What got better

**Adding an endpoint is one file.** It was eight, across two projects, one of which was a 143-line DI registration file you had to remember to edit.

**Deleting a feature is deleting a file.** The layered version made orphans easy to leave behind — the migration found four response types with zero references and a `GetAccountByNumberUseCase` that no controller called but that was still registered in DI.

**Wrong code is harder to write.** Three status-code bugs existed because the mapping lived far from the thing it described: category create *and* update failures both returned 404, and account creation failure returned 400. With one handler per exception type, the mapping is stated once.

**Fewer moving parts.** 31 hand-written `AddScoped` registrations became one open generic. 4 use-case marker interfaces and 9 single-implementation repository interfaces are gone.

## What got worse

**Duplication is real.** Five slices build a similar `Ok(...)` projection. Two write near-identical colour validation. `TryParseCurrency` exists in both `CreateTransaction` and `CreateTransfer`. This is the deliberate trade — independence over DRY — but it is a genuine cost, and it grows with the number of CRUD-shaped resources.

**The compiler no longer enforces layering.** Previously `Xpense.Domain` (then `Xpense.Services`) *could not* reference the API project; the build stopped you. Now nothing stops a slice from importing another slice except review. The rule is in [README](README.md); the rule is not in the type system.

**Routes are not statically traceable.** "Find all references" no longer answers "what serves `POST /api/v1/tags`". Discovery is a reflection scan, so the answer comes from grepping the route string. `WithName(nameof(CreateTag))` mitigates this a little.

**Cross-cutting changes touch more files.** Adding auth metadata per endpoint is N edits where a controller base class was one. Middleware-level concerns are unaffected; per-endpoint metadata is not.

## When this is the wrong choice

Be honest about the shape of the work:

- **Mostly CRUD over many similar resources** → layering shares that shape once. Slices will copy it per resource. Xpense has five CRUD-ish resources today, and the duplication above is the visible cost of that.
- **A large team with strict boundaries** → compiler-enforced project references are worth more than convention.
- **A genuinely rich domain** → most of the code is domain logic, and where the *endpoint* lives stops being the interesting question.

Xpense's planned work is distinct features — budgets, forecasting, receipt OCR, bank sync — rather than more CRUD, which is what makes slices the better fit here.

## Known gaps this migration introduced

- **Rollback-on-save-failure is no longer directly unit tested.** The old suite injected a failing repository to prove partial balance changes never persist. With the repository gone there is no injection point. Rollback *on domain failure* is still covered by two integration tests that assert balances are unchanged and no transfer row exists. Rollback on an infrastructure failure is not directly covered.
- **`TryParseCurrency` is duplicated** in the two slices that accept money. Small enough to leave, worth watching.
- **The `Dependencies/DependencyValidation1.layerdiagram`** is now meaningless and should be deleted with its `AdditionalFiles` entries.
