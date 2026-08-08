# Transaction Edit and Delete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a recorded transaction be edited or deleted without ever leaving an account balance wrong.

**Architecture:** A single `Reverse()` method on the `Transaction` entity undoes exactly what construction did. Delete reverses then soft-deletes. Edit reverses, reapplies, and rolls back entirely if any rule rejects the new values — all inside one serializable database transaction, the pattern `CreateTransaction` already uses.

**Tech Stack:** .NET 10, EF Core 10, Npgsql, FluentValidation, xUnit with Testcontainers.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-08-transaction-edit-delete-design.md`.
- **No comments in any file.** Project-wide rule. XML `<summary>` docs are allowed **only** where Swagger reads them — response and request records — matching what `BudgetResponse` and `NotificationResponse` already do.
- Explicit names. No `ct`, `db`, `req`. The codebase uses `cancellationToken`, `dbContext`, `request`.
- Validation messages are prose sentences ending in a full stop, matching `CreateTransaction.Validator`.
- Money never crosses currencies. `Deposit` and `Withdraw` already throw `CurrencyMismatchException`.
- Migrations run in CD, never on startup. **This plan adds no migration** — `IsDeleted` already exists on `BaseEntity` and the global query filter is already applied.

## What already exists — verified, do not re-derive

- `BaseEntity` has `IsDeleted`, `MarkAsDeleted()`, `Touch()`.
- `XpenseDbContext` applies `ApplyGlobalQueryFilter(modelBuilder, entity => !entity.IsDeleted)` to every entity, so a soft-deleted transaction disappears from all queries, including the budget spend query in `ListBudgets`.
- `Transaction.Kind` is derived from which account ids are set: source only → Expense, destination only → Income, both → Transfer.
- `Account.Deposit` and `Account.Withdraw` both call `Touch()` and enforce matching currency.
- `CreateTransaction` opens `BeginTransactionAsync(IsolationLevel.Serializable)`, resolves merchants and tags through `OptionResolver<T>`, emits an event through `IEventBus`, and rolls back on any exception.
- `TransactionRecorded` is `(int TransactionId, TransactionKind Kind, long AmountMinorUnits, Currency Currency, DateTime OccurredAt, int? CategoryId, int? MerchantId, string? SourceAccountNumber, long? SourceBalanceAfterMinorUnits, string? DestinationAccountNumber, long? DestinationBalanceAfterMinorUnits) : EventBody`.

---

### Task 1: `Transaction.Reverse()`

**Files:**
- Modify: `api/src/Xpense/Xpense.Domain/Entities/Transaction.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Unit/TransactionTests.cs`

**Interfaces:**
- Produces: `void Reverse()` on `Transaction`. Tasks 3 and 4 consume it.

- [ ] **Step 1: Write the failing tests**

Append to `TransactionTests.cs`, following the fixture style already in that file:

```csharp
[Fact]
public void Reversing_an_expense_restores_the_source_balance()
{
    var account = AccountWith(10000);
    var before = account.Balance;
    var transaction = Transaction.Expense(account, Money.OfMinorUnits(2500, Currency.EUR), CategoryFor(), MerchantFor(), null, DateTime.UtcNow);

    transaction.Reverse();

    Assert.Equal(before, account.Balance);
}

[Fact]
public void Reversing_an_income_restores_the_destination_balance()
{
    var account = AccountWith(10000);
    var before = account.Balance;
    var transaction = Transaction.Income(account, Money.OfMinorUnits(2500, Currency.EUR), CategoryFor(), MerchantFor(), null, DateTime.UtcNow);

    transaction.Reverse();

    Assert.Equal(before, account.Balance);
}

[Fact]
public void Reversing_a_transfer_restores_both_balances()
{
    var source = AccountWith(10000);
    var destination = AccountWith(5000);
    var sourceBefore = source.Balance;
    var destinationBefore = destination.Balance;
    var transaction = Transaction.Transfer(source, destination, Money.OfMinorUnits(2500, Currency.EUR), null, null, DateTime.UtcNow);

    transaction.Reverse();

    Assert.Equal(sourceBefore, source.Balance);
    Assert.Equal(destinationBefore, destination.Balance);
}

[Fact]
public void Reversing_an_income_may_take_an_account_negative_because_refusing_would_make_a_mistake_permanent()
{
    var account = AccountWith(0);
    var transaction = Transaction.Income(account, Money.OfMinorUnits(2500, Currency.EUR), CategoryFor(), MerchantFor(), null, DateTime.UtcNow);
    account.Withdraw(Money.OfMinorUnits(2500, Currency.EUR));

    transaction.Reverse();

    Assert.Equal(-2500, account.BalanceMinorUnits);
}
```

Add the `AccountWith`, `CategoryFor` and `MerchantFor` helpers only if the file does not already have equivalents — read it first and reuse what is there.

- [ ] **Step 2: Run them to verify they fail**

Run: `cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~TransactionTests"`
Expected: compile error — `Reverse` is not defined.

- [ ] **Step 3: Implement it**

In `Transaction.cs`:

```csharp
public void Reverse()
{
    if (SourceAccount is not null)
        SourceAccount.Deposit(Amount);

    if (DestinationAccount is not null)
        DestinationAccount.Withdraw(Amount);
}
```

Both accounts must be loaded. `Deposit` and `Withdraw` enforce currency, and neither checks sufficient funds, which is exactly the asymmetry the fourth test pins.

- [ ] **Step 4: Run them to verify they pass**

Run: `cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~TransactionTests"`

- [ ] **Step 5: Commit**

```bash
git add api/src/Xpense/Xpense.Domain/Entities/Transaction.cs api/src/Xpense/Xpense.Tests/Unit/TransactionTests.cs
git commit -m "feat: let a transaction reverse its own movement of money"
```

---

### Task 2: Reversal and amendment events

**Files:**
- Create: `api/src/Xpense/Xpense.Domain/Events/TransactionReversed.cs`
- Create: `api/src/Xpense/Xpense.Domain/Events/TransactionAmended.cs`

**Interfaces:**
- Produces: two `EventBody` records mirroring `TransactionRecorded`'s shape.

- [ ] **Step 1: Write the events**

`TransactionReversed.cs`:

```csharp
using Xpense.Domain.Enums;

namespace Xpense.Domain.Events;

public sealed record TransactionReversed(
    int TransactionId,
    TransactionKind Kind,
    long AmountMinorUnits,
    Currency Currency,
    DateTime OccurredAt,
    int? CategoryId,
    int? MerchantId,
    string? SourceAccountNumber,
    long? SourceBalanceAfterMinorUnits,
    string? DestinationAccountNumber,
    long? DestinationBalanceAfterMinorUnits) : EventBody;
```

`TransactionAmended.cs` carries both sides:

```csharp
using Xpense.Domain.Enums;

namespace Xpense.Domain.Events;

public sealed record TransactionAmended(
    int TransactionId,
    TransactionKind PreviousKind,
    long PreviousAmountMinorUnits,
    Currency PreviousCurrency,
    DateTime PreviousOccurredAt,
    TransactionKind Kind,
    long AmountMinorUnits,
    Currency Currency,
    DateTime OccurredAt,
    int? CategoryId,
    int? MerchantId,
    string? SourceAccountNumber,
    long? SourceBalanceAfterMinorUnits,
    string? DestinationAccountNumber,
    long? DestinationBalanceAfterMinorUnits) : EventBody;
```

- [ ] **Step 2: Check the worker tolerates unknown event kinds**

Read `api/src/Xpense/Xpense.Notifications/EventProcessor.cs` and `Rules/BudgetExceededRule.cs`. Confirm an event body it has no rule for is skipped rather than throwing or dead-lettering. **If it throws on an unrecognised kind, that is a bug this plan must fix** — the new events would jam the pump. Report what you find either way.

- [ ] **Step 3: Verify the build**

Run: `cd api && dotnet build src/Xpense/Xpense.sln`

- [ ] **Step 4: Commit**

```bash
git add api/src/Xpense/Xpense.Domain/Events/
git commit -m "feat: add reversal and amendment transaction events"
```

---

### Task 3: `DELETE /api/v1/transactions/{id}`

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Transactions/DeleteTransaction.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs`

**Interfaces:**
- Consumes: `Transaction.Reverse()`, `TransactionReversed`.
- Produces: the route. Task 5's UI consumes it.

- [ ] **Step 1: Write the failing tests**

Follow the existing style in `ApiEndpointTests.cs` — read how a transaction is seeded there and reuse it.

```
Deleting_a_transaction_returns_no_content
Deleting_an_expense_returns_the_money_to_the_source_account
Deleting_an_income_takes_the_money_back_off_the_destination_account
Deleting_a_transfer_restores_both_accounts
Deleting_a_transaction_removes_it_from_the_budget_it_counted_against
Deleting_a_transaction_twice_returns_not_found
Getting_a_deleted_transaction_returns_not_found
```

The budget one matters most: create a budget and a matching expense, read `ListBudgets` and record `Spent`, delete the transaction, read again, and assert `Spent` dropped by exactly the amount. That proves the global soft-delete filter does the work with no cache to invalidate.

- [ ] **Step 2: Run them to verify they fail**

Run: `cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~Deleting"`
Expected: 404 from an unmapped route.

- [ ] **Step 3: Implement the endpoint**

Model it directly on `CreateTransaction.cs`: `IEndpoint`, `Map` with `MapDelete("/api/v1/transactions/{id:int}", Handle).WithName(nameof(DeleteTransaction))`, a serializable transaction scope, rollback on exception.

The handler must:
1. Load the transaction with `.Include(item => item.SourceAccount).Include(item => item.DestinationAccount)` — **tracked, not `AsNoTracking`**, because the balances are being written.
2. Return `TypedResults.NotFound()` when it is missing.
3. Call `Reverse()`.
4. Call `MarkAsDeleted()` and `Touch()`.
5. `SaveChangesAsync`.
6. Emit `TransactionReversed` through `IEventBus`, then `SaveChangesAsync` again — the same two-save shape `CreateTransaction` uses.
7. Commit and return `TypedResults.NoContent()`.

- [ ] **Step 4: Run them to verify they pass**

Run: `cd api && dotnet test src/Xpense/Xpense.sln`

- [ ] **Step 5: Commit**

```bash
git add api/src/Xpense/Xpense.API/Features/Transactions/DeleteTransaction.cs api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs
git commit -m "feat: delete a transaction and give the money back"
```

---

### Task 4: `PUT /api/v1/transactions/{id}`

The hardest task. The rollback tests are the point.

**Files:**
- Create: `api/src/Xpense/Xpense.API/Features/Transactions/UpdateTransaction.cs`
- Modify: `api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs`

**Interfaces:**
- Consumes: `Transaction.Reverse()`, `OptionResolver<Merchant>`, `OptionResolver<Tag>`, `TransactionAmended`.
- Produces: the route, returning `TransactionResponse`.

- [ ] **Step 1: Write the failing tests**

```
Updating_the_amount_moves_the_balance_by_the_difference
Updating_the_source_account_restores_the_old_account_and_debits_the_new_one
Updating_an_expense_into_a_transfer_leaves_all_three_accounts_correct
Updating_a_transaction_can_create_a_new_merchant_inline
Updating_a_deleted_transaction_returns_not_found
Updating_a_transfer_with_insufficient_funds_leaves_every_balance_unchanged
Updating_a_transfer_to_the_same_account_on_both_sides_leaves_every_balance_unchanged
Updating_a_transfer_to_carry_a_category_leaves_every_balance_unchanged
Updating_a_one_sided_transaction_without_a_merchant_leaves_every_balance_unchanged
```

Every rejection test asserts **three** things: the response status, that the stored transaction still has its original values, and that both account balances are byte-for-byte unchanged. A rejection that leaves a half-applied balance is the failure mode this whole plan exists to prevent.

- [ ] **Step 2: Run them to verify they fail**

Run: `cd api && dotnet test src/Xpense/Xpense.sln --filter "FullyQualifiedName~Updating"`

- [ ] **Step 3: Implement the endpoint**

Reuse `CreateTransaction.Request`'s shape and its `Validator` rules verbatim — same fields, same messages, same one-sided-versus-transfer logic. Extract that validator into a shared class if it can be done without disturbing `CreateTransaction`'s tests; otherwise duplicate it and say so in the commit message.

Handler, inside one serializable scope:
1. Load the transaction tracked, with both accounts and its tags. `NotFound` if missing.
2. `Reverse()`.
3. Resolve the new source and destination accounts, category, merchant and tags exactly as `CreateTransaction.OneSided` does.
4. Apply the new movement using the same domain factories, so every rule runs again.
5. Copy the resulting fields onto the existing entity and `Touch()` it. Do **not** insert a new row — the id must survive.
6. `SaveChangesAsync`, emit `TransactionAmended`, `SaveChangesAsync`, commit.
7. Return `TypedResults.Ok(TransactionResponse.Of(transaction))`.

The kind change case is why step 3 resolves both accounts before step 4 applies anything.

- [ ] **Step 4: Run the whole suite**

Run: `cd api && dotnet test src/Xpense/Xpense.sln`
Expected: everything passes, including the 144 that existed before this plan.

- [ ] **Step 5: Commit**

```bash
git add api/src/Xpense/Xpense.API/Features/Transactions/UpdateTransaction.cs api/src/Xpense/Xpense.Tests/Integration/ApiEndpointTests.cs
git commit -m "feat: edit a transaction by reversing and reapplying it"
```

---

### Task 5: Row actions in the transactions grid

**Files:**
- Modify: `web/src/clients/transactions.ts`
- Modify: `web/src/pages/Transactions/TransactionsGrid/TransactionsGrid.tsx`
- Modify: `web/src/pages/Transactions/Transactions.tsx`

**Interfaces:**
- Consumes: the two new endpoints.
- Produces: `updateTransaction(id, request)` and `deleteTransaction(id)` clients, and an actions column.

- [ ] **Step 1: Add the clients**

```ts
export const updateTransaction = async (
  id: number,
  request: ICreateTransactionRequest
): Promise<ITransactionResponse> => {
  const response = await axios.put<ITransactionResponse>(`/api/v1/transactions/${id}`, request);
  return response.data;
};

export const deleteTransaction = async (id: number): Promise<void> => {
  await axios.delete(`/api/v1/transactions/${id}`);
};
```

- [ ] **Step 2: Add an actions column**

Add a trailing column to the grid with Edit and Delete buttons per row, each carrying an `aria-label` naming the transaction — `Edit the €42.18 Albert Heijn transaction` — so the accessible names are distinct.

- [ ] **Step 3: Wire the dialogs**

Edit opens the same transaction dialog the page already uses for creating, seeded from the row. Delete opens a confirmation dialog stating that **the money moves back to the account** — that is the consequence the user needs to see before confirming, and it is what makes this safe. Never delete on one click.

- [ ] **Step 4: Verify**

Run: `cd web && npm test && npm run build && npm run check:init`

- [ ] **Step 5: Commit**

```bash
git add web/src
git commit -m "feat: edit and delete transactions from the grid"
```

---

## Self-review

**Spec coverage.** `Reverse()` in Task 1 with the negative-balance asymmetry pinned by a named test. Both events in Task 2. Delete in Task 3, including the budget-recomputation test that proves the global filter does the work. Edit in Task 4, including one rollback test per rule. The UI in Task 5.

**Deliberately not here.** Retracting notifications already sent. Restoring a soft-deleted transaction. Bulk operations. All three are listed as out of scope in the spec.

**Type consistency.** `Reverse()` takes no arguments and returns void in Tasks 1, 3 and 4. `TransactionReversed` and `TransactionAmended` both extend `EventBody`, matching `TransactionRecorded`. The update request reuses `CreateTransaction.Request`'s field list exactly, so `ICreateTransactionRequest` on the web side serves both.

**Two risks for the executor.**
1. `Reverse()` requires both account navigations to be loaded. If a caller forgets the `Include`, the reversal silently does nothing for the missing side and the balance stays wrong — the worst possible failure, because it is invisible. Consider throwing when an account id is set but its navigation is null, and add a test for it.
2. Task 4 step 5 copies fields onto the existing entity rather than inserting. If an implementer instead adds a new row and soft-deletes the old one, every test here still passes but the transaction's id changes, breaking any client holding it. The `Updating_the_amount...` test should also assert the id is unchanged.
