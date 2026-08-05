# Model rename pass

Decided and implemented 2026-08-04, merged as PR #44. Written down so the decisions do not
have to be made twice.

The language itself lives in [UBIQUITOUS_LANGUAGE.md](../UBIQUITOUS_LANGUAGE.md).
The reasoning behind the model change is in
[ADR 0001](adr/0001-one-transaction-entity-with-two-nullable-sides.md), the public
identifier change in [ADR 0002](adr/0002-account-number-is-the-public-identifier.md),
and the API description change in [ADR 0003](adr/0003-generated-openapi-is-the-contract.md).

## Governing rules

1. **One word per concept, everywhere.** Domain, persistence, DTOs, validators,
   exceptions, tests, wire and docs all use the same word for the same thing.
2. **Full pass.** All models, not only entities.
3. The wire changes with the code. There are no external consumers yet, so this is the
   cheapest it will ever be.

## Domain

### Account

| Before | After | Note |
| ------ | ----- | ---- |
| `Name` | `Label` | Wire already said `label`; four other entities already use it |
| `BalanceCents` | `BalanceMinorUnits` | |
| `IsDefaultAccount` | `IsDefault` | Wire already said `isDefault` |
| `Balance` | unchanged | Computed from `BalanceMinorUnits` + `Currency` |
| `AccountNumber` | unchanged | Now the public identifier — see ADR 0002 |
| `Equals`, `GetHashCode`, `IEquatable<Account>` | **deleted** | Dead code: only `Seeder.Seed<T>` needs `IEquatable`, and `Program.cs:44` seeds `Priority` only |
| `Deposit`, `Withdraw` | unchanged | The verbs that change a balance. Kept deliberately — a transfer changes balances while being neither income nor expense |
| `Transactions` collection | **deleted** | Two foreign keys now point at `Account`, so one collection cannot express the relationship. Read slices query by either foreign key instead of navigating |

### BaseEntity

| Before | After |
| ------ | ----- |
| `CreatedOn` | `CreatedAt` |
| `LastUpdated` | `UpdatedAt` |

Touches every table. `CreatedAt` means when the row was written and is never set from a
request — that is what `Transaction.OccurredAt` is for.

### Transaction

The one entity that records all money movement.

| Before | After | Note |
| ------ | ----- | ---- |
| `Amount` (long) | `AmountMinorUnits` (long) | Plus a computed `Money Amount`, mirroring `Account.Balance` |
| `TransactionType` (stored) | **column dropped** | Replaced by a computed `Kind` |
| `AccountId`, `Account` | `SourceAccountId`, `SourceAccount`, `DestinationAccountId`, `DestinationAccount` | Both nullable; at least one required |
| `CategoryId`, `Category` | same names, **nullable** | |
| `MerchantId`, `Merchant` | same names, **nullable** | |
| — | `OccurredAt` (DateTime) | **new column**: when the money moved |
| — | `Reason` (string?) | **new column**, inherited from the deleted `Transfer` |
| — | `Kind` (computed) | `builder.Ignore`, like `Balance` |
| — | `Income`, `Expense`, `Transfer` static factories | **new**; they enforce the invariants and move the balances |

```csharp
public TransactionKind Kind =>
    SourceAccountId is null      ? TransactionKind.Income
  : DestinationAccountId is null ? TransactionKind.Expense
  :                                TransactionKind.Transfer;
```

One rule covers classification, enforced in a single place:

- exactly one side is an account → `Category` and `Merchant` both **required**
- both sides are accounts → `Category` and `Merchant` both **must be null**

### Enums

| Before | After |
| ------ | ----- |
| `TransactionType { Credit, Debit, Transfer }` | `TransactionKind { Income, Expense, Transfer }`, computed only, never stored |
| `TransferLegDirection { Debit, Credit }` | **deleted** |
| `Currency`, `CurrencyParser` | unchanged |

`Credit` and `Debit` disappear from the codebase. Every remaining reference is in code
this pass deletes.

### Money

| Before | After |
| ------ | ----- |
| `Cents` | `MinorUnits` |
| `OfCents(long cents, Currency)` | `OfMinorUnits(long minorUnits, Currency)` |
| `Money(long value, Currency)` | `Money(long minorUnits, Currency)` |
| `Zero`, `ToDecimal`, operators | unchanged |

### Deleted outright

- `Transfer`, `TransferLeg`
- `MoneyTransfer` and the whole `Xpense.Domain/Transfers/` folder — its job moves to the
  `Transaction` factories, which now also guard income and expense
- `TransferLegDirection`

### Untouched by choice

The `Option` family stays exactly as it is: `IOptionEntity`, `IOption<T>`,
`MerchantOption`, `TagOption`, `OptionResolver<T>`, `OptionRequest`. **Option** is a
deliberate glossary term, none of it reaches the wire, and a reader who reads the glossary
is not misled.

## Persistence

- `TransferEntityTypeConfiguration`, `TransferLegEntityTypeConfiguration` — **deleted**
- `TransactionEntityTypeConfiguration` — two nullable foreign keys to `Account`,
  nullable `Category` and `Merchant`, `Ignore(e => e.Kind)`, `Ignore(e => e.Amount)`
- `AccountEntityTypeConfiguration` — renamed properties; `AccountNumber` keeps its unique
  index and fixed length
- **Migrations squashed**: delete `20260727153514_InitialCreate` and
  `20260728140935_AddAccountCurrency`, add one `InitialCreate` for the new shape. Valid
  only because no deployed database exists. Local development databases must be dropped
  and recreated.
- Expand/contract applies from the first real deployment onwards, not to this pass.

## API

### Routes

| Before | After |
| ------ | ----- |
| `/accounts/{id}` | `/accounts/{accountNumber}` |
| `POST /transfers`, `GET /transfers/{id}` | **deleted** — folded into `/transactions` |
| `/transactions`, `/transactions/{id}` | unchanged; now returns transfers too |
| `/categories`, `/tags`, `/merchants` | unchanged |

`Features/Transfers/` is deleted entirely: `CreateTransfer`, `GetTransferById`,
`TransferResponse`, `TransferLegResponse`, `TransferMoneyResponse`.

### Requests

```csharp
// CreateAccount
Request(string Label, MoneyRequest Balance)
MoneyRequest(long MinorUnits, string Currency)

// UpdateAccount
Request(string Label, bool IsDefault)

// CreateCategory / UpdateCategory
Request(string Label, int PriorityId)

// CreateTransaction — one endpoint, all three kinds
Request(
    MoneyRequest Amount,
    string? SourceAccountNumber,
    string? DestinationAccountNumber,
    int? CategoryId,
    OptionRequest? Merchant,
    IReadOnlyList<OptionRequest> Tags,
    string? Reason,
    DateTimeOffset? OccurredAt)
```

The request loses its `Type` field. Which sides you send already says the kind, so a
separate `type` could only ever contradict them.

Three `MoneyRequest` copies stay, one per slice. That is deliberate per
`docs/vertical-slicing-architecture/03-what-stays-shared.md` and enforced by
`SliceIsolationTests`.

### Responses

- `Contracts/MoneyResponse(long MinorUnits, string Currency)` is the only money response.
  Delete `TransactionMoneyResponse` (`TransactionResponse.cs:41`) and the second
  `MoneyResponse` (`GetSpendingByCategory.cs:65`) — cross-feature response contracts belong
  in `Xpense.API.Contracts` per your own rule.
- `AccountResponse` — `label`, `isDefault`, no `id`, keeps `accountNumber`.
- `TransactionResponse` — `kind` replaces `type`, `sourceAccountNumber` and
  `destinationAccountNumber` replace `accountId`, nullable `merchant` and `category`,
  `reason`, `occurredAt`, `createdAt`, `updatedAt`.
- **Fix two nullability lies**: `TransactionResponse.UpdatedAt` is declared `string` but
  assigned `null` (`TransactionResponse.cs:36-37`), and `TransferResponse.Reason` is
  declared `string` but fed a `string?`. The second dies with the folder; the first must be
  `string?`.

### Timestamps

Every timestamp is an **ISO 8601 UTC string** named with an `At` suffix: `createdAt`,
`updatedAt`, `occurredAt`. This changes `AccountResponse`, `TagResponse`,
`CategoryResponse`, `PriorityResponse` and the merchant response from unix seconds, and
brings them in line with the contract doc's *"Dates use ISO 8601 UTC timestamps"*, which
they currently contradict.

### Queries that must move to OccurredAt

- `ListTransactions.cs:38` — `OrderByDescending(t => t.CreatedOn)`
- `GetSpendingByCategory.cs:34` — `t.CreatedOn.Date == today`

Both intend occurrence time and are only accidentally correct today, because
`CreateTransaction.cs:110` overwrites `CreatedOn` with the client's `OccurredAt`.

### Other API changes

- `Program.cs:42` — remove `context.Database.Migrate()`. Migrations belong in CD. Tests are
  unaffected: `Program.cs:35` already skips it under the `Testing` environment and
  `PostgresFixture` manages its own schema.
- `IoC.cs:70-79` — align the SwaggerGen title, description and contact with
  `docs/contract/api-v1-contract-design.md`.
- `CreateTransaction.cs:158` — `ToOption` becomes `ToMerchantOption`, pairing with
  `ToTagOption`.
- `SliceIsolationTests` — the unused `SharedContracts` constant was deleted and its explanation
  moved onto the test it was documenting.

## Docs

- **Deleted** `docs/api/v1.0/v1.0.yaml` and all four files under `docs/api/v1.0/schemas/`.
  See ADR 0003.
- `docs/contract/api-v1-contract-design.md` — updated. Money as `minorUnits`, accounts addressed
  by `accountNumber`, no `/transfers`, the derived-kind table, the timestamp convention, and the
  testing section corrected from SQLite/SQL Server to the per-test Postgres template.
- `docs/vertical-slicing-architecture/03-what-stays-shared.md` — worked example rewritten around
  `CreateTransaction` and the `Transaction` factories, plus the `Xpense.API.Contracts` rule and the
  corrected note that `OptionResolver` has one consumer with two type arguments.
- `docs/multi-currency.md` and `docs/vertical-slicing-architecture/README.md` — both carry a note
  that their code and payload examples predate this pass. They are records of earlier changes, so
  they are annotated rather than rewritten.
- `AGENTS.md`, `README.md` — project tree listings no longer mention `MoneyTransfer`.

## Settled during implementation

Things this pass surfaced once the code was actually being written:

- **`Account.Transactions` is deleted rather than split.** Two foreign keys point at `Account`,
  so one collection cannot express the relationship. Two navigations
  (`OutgoingTransactions` / `IncomingTransactions`) would only ever be queried separately, so
  read slices query by either foreign key instead.

- **`Category.Label` is now `required`,** matching every sibling entity.

- **The default-account fallback is gone.** `CreateTransaction` used to substitute the default
  account when a request named none. That relied on the `type` field to know *which side* the
  default stood in for, and `type` no longer exists — naming only a destination is already what
  makes a transaction income. The request must now name at least one account, which is a
  validation error otherwise. `Account.IsDefault` survives as advice to a client about which
  account to offer first; `DefaultAccountNotFoundException` was deleted as dead code.

- **`Kind` reads the navigations as well as the foreign keys.** A transaction built by a factory
  carries navigations and no keys until EF saves it, so a `Kind` that looked only at
  `SourceAccountId` would report every unsaved transaction as income. `TransactionTests` pins
  this.

- **Every kind now writes inside a serializable transaction.** All three read a balance and write
  it back; the old transfer endpoint isolated that and the old income/expense endpoint did not.
  One code path means one answer, and the safer one is the correct one.

- **`GetSpendingByCategory` now filters to expenses.** It previously summed every transaction that
  day regardless of direction, so income counted as spending; with one table, transfers would also
  have been included and have no category to group by. The filter is expressed as columns because
  `Kind` is computed and not queryable.

- **Two more dead exceptions deleted:** `DefaultAccountNotFoundException` (see above) and
  `UnsupportedCurrencyException`, which had no caller before this pass either.

- **The identical-accounts rule is stated twice on purpose** — in `CreateTransaction.Validator`
  for a field-level 400, and in `Transaction.Transfer` so no future caller can bypass it. That is
  the pattern `03-what-stays-shared.md` already describes.

## Deliberately out of scope

- **Users and ownership.** `Owner` is in the glossary because the `Transaction` model
  depends on it, but there is no `User` entity, no ownership column and no query scoping.
  There is also no authentication anywhere in `Program.cs`, so ownership could not be
  enforced if it existed. Authentication, `User`, `Account.OwnerId`, query scoping, transfer
  authorisation and receiver confirmation are one piece of work, and it comes next.
- **`CreateAccount.NextAccountNumber`** (`CreateAccount.cs:91-96`) loads every account
  number into memory and takes the maximum. It races under concurrent creates and degrades
  as accounts accumulate. Its own ticket.
- **Sequential account numbers.** They start at `1_000_000_000` and increment. Harmless
  today, because nothing lets you act on an account you do not own. Once a cross-user
  transfer can write to an account named by number, a guessable public identifier lets
  anyone walk the range. Needs non-sequential numbers and an authorisation check, with the
  ownership work.
