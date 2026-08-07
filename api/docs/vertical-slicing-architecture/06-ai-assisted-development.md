# Slices and AI-assisted development

How this structure compares to the layered/hexagonal one when most edits are made with an AI agent in the loop. Written from the experience of doing this migration that way.

## Where slices help

**One file is one complete unit of context.** To change "create a tag", an agent opens `CreateTag.cs` and sees the route, the request shape, the validation rules and the handler. Under the old structure it had to locate and read eight files across two projects before it could safely change anything. Every extra hop is another tool call, more context spent, and another chance to modify four of the five places that mattered.

**There is no registration step to forget.** The old flow was: write the use case, then remember to add `services.AddScoped<CreateTagUseCase>()` in a 143-line `IoC.cs`. That kind of invisible, action-at-a-distance coupling is precisely what an agent drops — the code compiles and the failure is at runtime. Slices are discovered by a scan, so the file *is* the registration.

**Deletion actually completes.** Removing a feature across layers means finding the controller action, the request, the response, the validator, the command, the use case, the DI line, and any now-orphaned repository method. Agents are bad at this, and the evidence was in the repo: four response types with no references, and a use case still registered in DI that no controller called. Deleting a slice is deleting a file.

**Names map to files.** "Where is create transfer" resolves to `Features/Transfers/CreateTransfer.cs`. Under layers the same question needed a guess about which of `Controllers/`, `Commands/`, `Usecases/` or `Models/Requests/` to look in first.

**Smaller, more reviewable diffs.** A feature change touches one file, so the diff is legible and a human can actually check it. That matters more when the code was written quickly.

## Where slices hurt

**Cross-cutting changes have no chokepoint.** "Change how currency is parsed everywhere" is one edit under layers and N edits under slices — `TryParseCurrency` currently exists in two slices. Agents are competent at mechanical sweeps, but each additional site is another chance to miss one, and there is no compiler error when they do.

**The compiler stopped enforcing the architecture.** This is the real loss. Previously `Xpense.Domain` could not reference `Xpense.API` — the project graph made a whole class of mistake impossible, and that guarantee applied to agents exactly as it applied to people. Now "slices must not call each other" and "slices must not catch domain exceptions" live in [README](README.md) and [01-anatomy-of-a-slice.md](01-anatomy-of-a-slice.md). **Prose is a weaker constraint than a build error, for humans and models alike.** An agent that has not read the docs will happily import one slice from another.

**Routes are not statically traceable.** Reflection-based discovery means "find all references to `Map`" returns nothing useful. An agent answering "what handles this URL" must grep the route string rather than follow a symbol.

**Duplication looks like a bug to a model.** An agent reading two similar slices will often "helpfully" extract a shared abstraction, quietly undoing the trade the architecture is making. That tendency needs explicit instruction to resist.

## Honest verdict

Slices are **better for feature work** — which is most work — and **worse for cross-cutting work**. The layered version's genuine advantage was the compiler-enforced project boundary; its abstractions (marker interfaces, single-implementation repositories) were pure indirection and actively hurt, because every extra hop is context an agent spends before it can reason.

Net: worth it here. But the mitigation for the lost compiler boundary is documentation and review, and that is a real downgrade in enforcement, not a neutral swap.

## Making it work in practice

- **Keep these docs current.** They are the substitute for the constraints the compiler used to apply. Stale docs are worse than none.
- **State the rules in `CLAUDE.md` / `AGENTS.md`**, not only here — agents read repo-root instructions reliably and `docs/` only when pointed at it.
- **Say "do not extract" explicitly** when duplication is deliberate. `// ponytail:`-style comments marking intentional simplicity work well as inline signals.
- **Lean on route-level tests.** `ApiEndpointTests` passed unchanged through this entire migration precisely because it asserts behaviour, not structure. Tests coupled to structure would have blocked the refactor; tests coupled to HTTP made it safe.
- **Consider an architecture test** (NetArchTest or similar) asserting that no `Features.X` namespace references `Features.Y`. That converts the most important convention back into a build failure, which is the enforcement level that was lost.
