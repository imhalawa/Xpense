---
status: accepted
date: 2026-08-04
---

# One Transaction entity with two nullable sides

Xpense recorded money movement in two different shapes: a `Transaction` naming one
account, and a `Transfer` naming two accounts plus two `TransferLeg` rows. We are
replacing all three with a single `Transaction` that has a nullable
`SourceAccountId` and a nullable `DestinationAccountId`. A `null` side means the
money came from, or went to, somewhere outside Xpense — and the `Merchant` says who
that was. Every row therefore names both sides of the movement, and the kind of
movement is derived rather than stored.

| Kind | SourceAccountId | DestinationAccountId |
| -------- | ------------------ | -------------------- |
| Income | `null` | an account |
| Expense | an account | `null` |
| Transfer | an account | another account |

A cross-user transfer needs no new concept: the destination account simply belongs
to a different owner. Whether a transfer is internal or cross-user is derived by
comparing the two accounts' owners.

## Context that drove this

Two facts decided it, and neither is visible in the code:

1. **Xpense holds no money.** It records movement that already happened somewhere
   else — a real bank, or a payment app such as Tikkie. It is not a custodian, so
   there is no double-spend risk, no need for holds or pending states, and no
   external system to reconcile against.
2. **Users will move money to each other on the platform.** Two people then read
   one shared record and can disagree about it. That is the only part of the system
   where the data is authoritative rather than a personal note.

## Considered options

**A balancing double-entry ledger** — an insert-only entry table where every event's
entries sum to zero, with counterparty accounts for the outside world. This is what
Modern Treasury, Square Books, Firefly III and Fowler's accounting patterns all do,
and all four use the same three objects: an event, an **entry** per account, and an
account whose balance is the sum of its entries. Rejected because it solves a problem
Xpense does not have. Double-entry protects against bookkeeping errors, which matter
enormously to a custodian; Xpense's errors are missing or mistyped input, which
double-entry cannot catch. It also required turning either `Merchant` or `Category`
into account rows, which forced `AccountNumber` to become nullable, changed what
`GET /api/v1/accounts` returns, and raised an unanswerable question about whether
those accounts are shared between users or duplicated per user.

**A movement log** — one insert-only table recording every balance change, with no
counterparty and no zero-sum rule. Rejected as a half-measure: it makes a balance
recomputable but still cannot say where money went, and querying transactions and
transfers together already gives most of that.

**A single "External" counterparty account** — cheap double-entry where everything
outside Xpense posts to one system account. Rejected once we understood that
`Merchant` already fills that role. `External` would have been a second, less
informative answer to a question the model already answers.

**Renaming `TransferLeg` to `TransferEntry`** — the original goal of the session, since
"leg" is trading vocabulary rather than bookkeeping vocabulary. Rejected because the
legs carried nothing that `Transfer` did not already hold; the honest fix was deleting
the entity, not naming it better.

## Consequences

**Deleted:** `Transfer`, `TransferLeg`, `TransferLegDirection`, `TransferEntityTypeConfiguration`,
`TransferLegEntityTypeConfiguration`, `TransferResponse`, `TransferLegResponse`, the
`Transfers` and `TransferLegs` tables, and the `TransactionType` column. Also
`Features/Transfers/` in its entirety — one entity means one resource, so `POST /transfers`
and `GET /transfers/{id}` fold into `/transactions`, and transfers appear in the transaction
list.

**`MoneyTransfer` is replaced by static factories on `Transaction`** — one per kind. The
transfer path had a domain guard while income and expense were validated only by the
endpoint; now all three are guarded in the domain.

The full list of resulting renames and deletions is in
[docs/model-rename-pass.md](../model-rename-pass.md). Two related decisions came out of the
same session: [ADR 0002](0002-account-number-is-the-public-identifier.md) on the public
identifier and [ADR 0003](0003-generated-openapi-is-the-contract.md) on the API description.

**Merchant is a counterparty, not decoration.** It is the only thing that says who was
on the other side when that side is outside Xpense. That is why `/merchants` is
list-only and why merchants are created on the fly through `MerchantOption`.

**`Category` and `Merchant` become nullable.** They are required when exactly one side
is an account, and must be absent when both sides are accounts — a transfer between
your own accounts has no shop and no spending class. One validation rule enforces both
halves, so the data cannot hold a transfer that claims a merchant.

**The kind is derived, never stored.** `Transaction.Kind` is computed from which side is
`null`, the same way `Account.Balance` is already computed from `BalanceCents` and
`Currency`, and it is mapped with `builder.Ignore`. A row can therefore never contradict
itself. If filtering by kind ever becomes slow, a Postgres generated column adds an index
without changing the model.

**`MoneyTransfer.cs:39` rejects a transfer when the source balance is too low.** For two
of your own accounts that is right. For a cross-user settlement it is probably wrong: the
real payment already happened in Tikkie, so the sender's Xpense balance can legitimately
be short if they never recorded the income. Unresolved.

**Nothing stops one user writing into another user's history.** In this model, creating a
transaction with someone else's destination account changes their balance without their
agreement. A confirmation step is needed. Unresolved, and a workflow feature rather than
a modelling one.

## Sources

- [Modern Treasury — How to Scale a Ledger, Part I](https://www.moderntreasury.com/journal/how-to-scale-a-ledger-part-i) and [Part V](https://www.moderntreasury.com/journal/how-to-scale-a-ledger-part-v)
- [Square — Books, an immutable double-entry accounting database service](https://developer.squareup.com/blog/books-an-immutable-double-entry-accounting-database-service/)
- [Martin Fowler — Accounting Patterns](https://martinfowler.com/apsupp/accounting.pdf)
- [Firefly III — Transactions and Journals](https://deepwiki.com/firefly-iii/firefly-iii/2.2-transactions-and-journals)
- [Firefly III is not double-entry accounting](https://www.kennethballard.com/?p=9483) — on why its one-to-many restrictions fall short of real double-entry
