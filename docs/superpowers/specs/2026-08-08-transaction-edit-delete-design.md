# Editing and deleting transactions

Date: 2026-08-08
Status: agreed, not yet implemented
Scope: API only. `UpdateTransaction` and `DeleteTransaction`, and the domain logic that keeps account
balances correct when a recorded movement of money is changed or withdrawn.

## Why

The web app's transactions grid needs per-row edit and delete actions. The API cannot support them:
`Features/Transactions/` contains only `CreateTransaction`, `GetTransactionById`, `ListTransactions`,
`TransactionQueries` and `TransactionResponse`.

This is not a missing CRUD endpoint. **A transaction moves money.** `Transaction.Expense()` calls
`source.Withdraw(amount)`, `Income()` calls `destination.Deposit(amount)`, and `Transfer()` does both.
Removing or changing a transaction without undoing its effect leaves every downstream balance wrong,
silently and permanently.

## What the codebase already gives us

Verified before designing, not assumed:

- **A global soft-delete filter exists.** `XpenseDbContext` applies
  `ApplyGlobalQueryFilter(modelBuilder, entity => !entity.IsDeleted)` to every entity, and
  `BaseEntity` carries `IsDeleted` plus a method that sets it. A soft-deleted transaction therefore
  disappears from every existing query with no per-query change.
- **Budget spend is recomputed on read.** `ListBudgets` queries `dbContext.Transactions` for the
  period and sums them; nothing is cached. So once a transaction is soft-deleted, budgets, the
  `Exceeded` flag and `Uncounted` all correct themselves on the next read.
- **Serializable transactions are the established pattern.** `CreateTransaction` already opens
  `BeginTransactionAsync(IsolationLevel.Serializable)` and rolls back on any exception.
- **Domain rules live on the entity**, not in the endpoint: positive amount, currency match,
  different accounts for a transfer, sufficient funds for a transfer.

## Decision: reverse and reapply

**Delete** reverses the original movement, then soft-deletes the row.

**Edit** reverses the original movement and applies the new one, inside a single serializable
transaction. If any rule rejects the new values, the whole thing rolls back and the original stands.

Rejected alternatives, for the record:

- *Hard delete* — loses history, and `IsDeleted` exists precisely so it need not.
- *Soft delete without reversal* — the row vanishes from reports while the balance it caused stays.
  Worse than no delete, because the corruption is invisible.
- *Immutable ledger with compensating entries* — defensible bookkeeping, and genuinely the safest
  option, but it means a typo is fixed by entering a second transaction and the grid shows both. The
  user chose reverse-and-reapply with that trade-off understood.

## Domain design

A new method on `Transaction`:

```
public void Reverse()
```

It undoes exactly what construction did, using the same account objects:

| Kind | Created by | Reversed by |
|---|---|---|
| Income | `destination.Deposit(amount)` | `destination.Withdraw(amount)` |
| Expense | `source.Withdraw(amount)` | `source.Deposit(amount)` |
| Transfer | `source.Withdraw`, `destination.Deposit` | `destination.Withdraw`, `source.Deposit` |

`Reverse()` must not enforce the sufficient-funds rule. Reversing an income can legitimately take an
account negative if the money has since been spent — refusing would make a recorded mistake
permanent. This is a deliberate asymmetry with `Transfer()`, which does enforce it, and it needs a
test naming that intent so nobody "fixes" it later.

Reversal is currency-safe for free: `Deposit` and `Withdraw` already throw
`CurrencyMismatchException` unless the amount matches the account.

## Delete

`DELETE /api/v1/transactions/{id}`

1. Load the transaction with both accounts tracked.
2. `Reverse()`.
3. Soft-delete the row.
4. Emit a `TransactionReversed` event.
5. Commit.

Returns `204`. A second delete of the same id returns `404`, because the global filter has already
hidden the row — no special handling needed.

## Edit

`PUT /api/v1/transactions/{id}`

Body is the same shape as `CreateTransaction.Request`: amount, source and destination account
numbers, category, merchant, tags, reason, occurredAt.

1. Load the existing transaction with both accounts tracked.
2. `Reverse()` it.
3. Resolve the new accounts, category, merchant and tags exactly as `CreateTransaction` does,
   including inline merchant and tag creation through `OptionResolver`.
4. Apply the new movement, running every rule `CreateTransaction` runs.
5. Overwrite the transaction's fields and `Touch()` it.
6. Emit a `TransactionAmended` event.
7. Commit — or roll back entirely.

**A kind change is allowed and is the interesting case.** Turning an expense into a transfer means
reversing a withdrawal on one account and then withdrawing from one account and depositing to
another. Because both steps happen inside one serializable transaction, a partial application cannot
survive. The validator's existing rules apply unchanged: one-sided transactions require a category
and a merchant, transfers must have neither and may carry a reason.

## Events

`TransactionRecorded` already exists and drives the notification worker. Two more are needed so the
worker sees a complete history rather than an unexplained gap:

- `TransactionReversed` — id, kind, amount, currency, occurredAt, the account numbers and their
  resulting balances.
- `TransactionAmended` — the same, before and after.

Notifications already emitted for a since-deleted transaction are not retracted. A `BudgetExceeded`
alert records that the budget *was* exceeded at that moment, which remained true when it fired. The
next read of the budget shows the corrected figure. Retraction is deliberately out of scope; if it
is ever wanted, the `UQ_Notifications_Event_Payload` unique index is where to start.

## Testing

Written test-first. The balance assertions are the point of the whole spec.

- Reversing an expense returns the source account to its prior balance, exactly.
- Reversing an income returns the destination account to its prior balance.
- Reversing a transfer restores both accounts.
- Reversing an income is allowed to take an account negative, and does not throw.
- Delete then read returns 404; the account balance matches the pre-transaction figure.
- Delete removes the amount from the owning budget's `Spent` on the next `ListBudgets`.
- Edit that only changes the amount moves the balance by exactly the difference.
- Edit that changes the source account restores the old account and debits the new one.
- Edit that changes an expense into a transfer leaves all three accounts correct.
- Edit rejected by a rule — insufficient funds, currency mismatch, same source and destination,
  category on a transfer — leaves every balance and the stored transaction completely unchanged.
  This is the test that proves the rollback works, and it needs one case per rule.
- Editing a soft-deleted transaction returns 404.
- Two concurrent edits of the same transaction do not interleave; serializable isolation means one
  retries or fails rather than both applying.

## Out of scope

- Retracting notifications already sent.
- Restoring a soft-deleted transaction. The row survives, so an endpoint could be added later.
- Bulk edit or bulk delete.
- Any web UI. This spec unblocks the grid's row actions; the UI belongs to a separate plan.
