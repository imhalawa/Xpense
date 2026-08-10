# Multi-currency

Xpense holds balances in more than one currency and **never converts between them**. There is no
rate table, no rate provider and no rounding policy, because there is nothing to round.

Supported currencies today: **EUR** and **USD**.

## Why not convert

A converted figure is only true at one instant, at one rate, from one source. Stored in a ledger it
becomes a number nobody can reproduce and nobody can audit. Xpense records what happened; what your
euros were worth in dollars on a Tuesday is a different question, and answering it inside the
ledger would corrupt the record.

So every currency-mixing operation is an error, never a silent conversion.

## The rules

| Operation | Rule |
| --- | --- |
| Creating an account | The opening balance's currency denominates the account for life |
| Income or expense | The amount's currency must equal the account's |
| Transfer | Both accounts *and* the amount must share one currency |
| Comparing two amounts | Different currencies cannot be compared, and the attempt throws |

Every one of these produces a **400**, never a conversion.

## What you see instead of a total

Because nothing is converted, anything that would otherwise be a single total is reported **per
currency**:

- **Balances** — one tile per currency on the overview. Never one combined figure.
- **Budgets** — a budget is denominated in one currency. Spending on its category in another
  currency appears as *uncounted*, so it is visible rather than dropped.
- **Spending by category** — a category spent in two currencies appears twice, once per currency,
  and the totals are one entry per currency.

![Per-currency balances and an uncounted amount](images/overview.png)

Adding amounts in different currencies produces a number that is true of nothing. Xpense would
rather show you two numbers than one wrong one.

## Working with two currencies

If you hold cash in euros and dollars, create two accounts. If you want to track groceries in both,
create two budgets. This feels like duplication, and it is — it is the honest kind, where each
figure means exactly one thing.

## Minor units

Amounts are integers in minor units everywhere — in the code, the database and on the wire. A euro
is 100 minor units. The term is deliberately not "cents": the first currency without cents would
make that name wrong.

On the wire, money is always a pair:

```json
{ "minorUnits": 1250, "currency": "EUR" }
```

## If exchange rates are ever added

They belong **before** the domain, never inside it: a conversion would be recorded as its own
transaction pair with the rate captured at that moment, leaving each account still denominated in
one currency. See [`api/docs/multi-currency.md`](../api/docs/multi-currency.md).
