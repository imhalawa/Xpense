# What stays shared

Slices are about where *endpoint* code lives. They are not an argument against having a domain.

## The line

| Belongs in a slice | Belongs in the domain |
|---|---|
| Route, HTTP verb, status codes | Invariants that must hold regardless of caller |
| Request shape and its validation | Money arithmetic |
| Reading and projecting to a response | Entity behaviour (`Deposit`, `Withdraw`, `MarkAsDeleted`) |
| Straightforward EF queries | Logic depended on by more than one feature |

The test: **if it would still be true with no HTTP layer at all, it is domain.**

## The worked example: transfers

`CreateTransfer.cs` is the slice. It owns the route, the request, the validator, loading the two accounts, and the atomic boundary:

```csharp
await using var scope = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
try
{
    var transfer = MoneyTransfer.Between(source, destination, amount, reason, occurredAt);
    db.Transfers.Add(transfer);
    await db.SaveChangesAsync(ct);
    await scope.CommitAsync(ct);
    ...
}
catch { await scope.RollbackAsync(ct); throw; }
```

`MoneyTransfer.Between` is the domain. It enforces that the amount is positive, that the accounts differ, and that the source can cover it; then it moves the balances and builds the debit and credit legs. It has no EF dependency and no HTTP dependency, so its tests need neither — `MoneyTransferTests` is a genuine unit test, unlike the SQLite-backed "unit" tests it replaced.

The invariants live in `MoneyTransfer` **as well as** in `CreateTransfer.Validator`. That is not redundancy to remove: the validator produces a good 400 for a bad request, and the domain guard makes it impossible to move money incorrectly through any future caller.

## OptionResolver

`OptionResolver<T>` resolves a client-supplied merchant or tag to a persisted entity — match by id, fall back to label, undelete a soft-deleted row, or create when asked.

It survived as a shared service rather than being inlined because the rules are subtle, two things depend on them, and getting them wrong silently duplicates merchants. It is the one piece of the old repository layer that was earning its keep; it was `OptionRepository<T>.GetOrCreateIfMissing`.

This is the honest counterexample to "delete all the abstractions". Most of them were indirection. This one was not.

## Soft deletes

`Delete` on the old repository did not remove rows — it set `IsDeleted` and touched `LastUpdated`, and a global query filter in `XpenseDbContext` hides them. Every delete slice preserves this:

```csharp
entity.MarkAsDeleted();
entity.Touch();
```

It is easy to miss when reading a slice in isolation, which is exactly why it is written here.

## Shared per feature, not per slice

`TagResponse`, `AccountResponse` and `CategoryResponse` each sit in their feature folder rather than inside one slice, because several endpoints return them and a resource should look the same however it was fetched.

Requests are the opposite — they stay nested in their slice even when two look identical today, because create and update requests diverge as soon as anything real happens.
