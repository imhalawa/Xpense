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

## The worked example: creating a transaction

`CreateTransaction.cs` is the slice. It owns the route, the request, the validator, loading the accounts, category and merchant, and the atomic boundary:

```csharp
await using var scope = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
try
{
    var transaction = source is not null && destination is not null
        ? Transaction.Transfer(source, destination, amount, request.Reason, tags, occurredAt)
        : await OneSided(...);   // Transaction.Income or Transaction.Expense
    db.Transactions.Add(transaction);
    await db.SaveChangesAsync(ct);
    await scope.CommitAsync(ct);
    ...
}
catch { await scope.RollbackAsync(ct); throw; }
```

The three static factories on `Transaction` are the domain. Each enforces that the amount is positive and that the currencies agree; `Transfer` additionally enforces that the accounts differ and that the source can cover it. Then they move the balances and build the row. They have no EF dependency and no HTTP dependency, so their tests need neither — `TransactionTests` is a genuine unit test, unlike the SQLite-backed "unit" tests it replaced.

This is also why the factories live on the entity rather than in a separate service. `MoneyTransfer.Between` used to guard the transfer path while income and expense called `Account.Deposit` and `Account.Withdraw` straight from the slice — so two of the three kinds were protected only by an endpoint validator. One place per kind, all three guarded.

The invariants live in the factories **as well as** in `CreateTransaction.Validator`. That is not redundancy to remove: the validator produces a good 400 for a bad request, and the domain guard makes it impossible to move money incorrectly through any future caller. The identical-accounts rule appears in both for exactly that reason.

## OptionResolver

`OptionResolver<T>` resolves a client-supplied merchant or tag to a persisted entity — match by id, fall back to label, undelete a soft-deleted row, or create when asked.

It survived as a shared service rather than being inlined because the rules are subtle, getting them wrong silently duplicates merchants, and it is used generically over two entity types. Only `CreateTransaction` consumes it, with `Merchant` and `Tag` as type arguments. It is the one piece of the old repository layer that was earning its keep; it was `OptionRepository<T>.GetOrCreateIfMissing`.

This is the honest counterexample to "delete all the abstractions". Most of them were indirection. This one was not.

## Soft deletes

`Delete` on the old repository did not remove rows — it set `IsDeleted` and touched `UpdatedAt`, and a global query filter in `XpenseDbContext` hides them. Every delete slice preserves this:

```csharp
entity.MarkAsDeleted();
entity.Touch();
```

It is easy to miss when reading a slice in isolation, which is exactly why it is written here.

## Shared per feature, not per slice

`TagResponse`, `AccountResponse` and `TransactionResponse` each sit in their feature folder rather than inside one slice, because several endpoints return them and a resource should look the same however it was fetched.

Contracts returned by **more than one feature** go one level further out, into `Xpense.API.Contracts`, which is outside `Features` and therefore always allowed by `SliceIsolationTests`. That is `CategoryResponse` (analytics embeds a category), `MoneyResponse` (accounts, transactions and analytics all return money) and `Timestamps` (everything returns timestamps and they must be formatted identically).

Requests are the opposite — they stay nested in their slice even when two look identical today, because create and update requests diverge as soon as anything real happens. That is why three separate `MoneyRequest` records exist and should stay.
