# Transactions

A transaction records money that has already moved. Xpense stores one kind of record and reads its
shape from which sides are filled in.

![The transactions list](images/transactions.png)

## The three kinds

| Kind | Source | Destination | Needs a category and merchant |
| --- | --- | --- | --- |
| Income | — | an account | Yes |
| Expense | an account | — | Yes |
| Transfer | an account | another account | No — neither is allowed |

A transfer moves your own money between your own accounts. It is not spending, so it never counts
against a budget and never carries a category or a merchant. Income and expense each have one side
outside Xpense, and that side is described by a merchant.

There is a single `Transaction` entity with two nullable sides rather than three types. See
[ADR 0001](../api/docs/adr/0001-one-transaction-entity-with-two-nullable-sides.md).

## Fields

| Field | Notes |
| --- | --- |
| Amount | Required, must be positive. Stored in minor units |
| Currency | Must match the account it touches |
| Type | Income, expense or transfer |
| Account | The source or destination, depending on type |
| Counterparty account | Transfers only |
| Category | Required on income and expense, forbidden on transfers |
| Merchant | Required on income and expense, forbidden on transfers |
| Date and time | Defaults to now |
| Tags | Optional, any number |
| Reason | Optional free text |

![The transaction form](images/transaction-form.png)

## Quick Add

The form opens with a Quick Add field that reads a plain sentence and fills in what it recognises:

```
Spent 5 euros at Albert Heijn #shopping
```

The parser is deterministic — no model, no network call. It recognises kind, amount, currency,
merchant, category, source and destination accounts, tags, date, time and reason, and marks each
span it finds as recognised, unresolved, conflicting or invalid so you can see what it did and did
not understand before saving. Everything it fills stays editable.

## Rules you will meet

| Situation | What happens |
| --- | --- |
| Amount of zero or less | Rejected |
| Transfer with the same account on both sides | Rejected — "Source and destination accounts must be different" |
| Transfer where the two accounts hold different currencies | Rejected |
| Transfer larger than the source balance | Rejected as insufficient funds |
| Category or merchant on a transfer | Rejected |
| Income or expense without a category or merchant | Rejected |
| An amount whose currency differs from the account's | Rejected, never converted |

## Filtering

The toolbar filters by date range and account; the sidebar filters by category, tag and merchant.
Filters combine, and **Clear filters** resets them.

A date range is half-open: the start is included, the end is excluded. Asking for 1 August to
1 September gives you August and nothing of September.

In encrypted mode the filtering happens in your browser, because the server sees only ciphertext
and cannot search it. When a filter refers to something that no longer exists — a deleted tag, say
— it is dropped and the list tells you which facets were removed rather than silently returning
nothing.

## Editing and deleting

Select a transaction to edit it. Editing an amount or an account reverses the old effect on the
balances and applies the new one, so balances stay correct without a recalculation pass.

Deleting is soft: the record is tombstoned rather than erased, which is what lets the encrypted
sync agree across devices about what was removed.

## Ordering and paging

The list is newest first by the date the money moved, not the date you typed it in. A purchase
entered today but dated last month appears in last month's position and counts against last
month's budget.

The server pages at 25 rows by default. In encrypted mode the client holds the decrypted set and
virtualises long lists instead.
