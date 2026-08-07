---
status: accepted
date: 2026-08-05
---

# A Budget reports and never blocks

Recording an **Expense** that exceeds a **Budget** succeeds. There is no budget check in
`Transaction.Expense`, none in `CreateTransaction`, and no field on the transaction response
saying a **Budget** was crossed. **Budget** is a read-side concept: it owns queries that compute
**Spent** and **Remaining**, and nothing else in the system consults it.

Detecting that a **Budget** was crossed belongs to the notification consumer, which reads a
`TransactionRecorded` event and decides for itself what is noteworthy.

## Why

Xpense is a ledger. `UBIQUITOUS_LANGUAGE.md` opens with it: "It records money that has already
moved and reports on it. Xpense never holds money itself." The money left the bank before Xpense
heard about it. A **Budget** that refused the record would not un-spend anything — it would only
make Xpense disagree with the bank, which is the one thing a ledger must never do. The recorded
**Balance** would drift from the real one, and the fix would be to delete the **Budget**.

That settles blocking. It does not settle where the *warning* lives, and the first answer is
tempting and wrong: put the crossed budgets on the `POST /transactions` response. That couples
two features that have no reason to know about each other, makes every transaction create pay for
a budget query, and builds a worse version of something already planned — a central notifications
feature, decoupled by a queue, where producers raise events and a job consumes, stores and serves
them. Budget-exceeded is one event type among several, next to transaction recorded and account
changed.

Given a queue, the crossing has a natural home that is not this feature. Transactions raise one
event saying a **Transaction** happened — which that epic needs regardless. The consumer reads
it and decides what matters, including asking **Budget** whether anything was crossed. Producers
stay ignorant of who cares, which is the entire point of putting a queue between them.

So **Budget** never touches the write path, now or later. Whether a given **Expense** crossed a
**Budget** is computable from the **Transaction** and the **Budget** alone — the sum before it
against the sum after — so nothing is lost by not capturing the moment today.

## Considered options

**A hard cap that rejects the Expense**, enforced in the domain the way
`InsufficientFundsForTransferException` already is. Genuinely prevents overspending in an app used
with discipline. Rejected because it contradicts the premise above: the money already moved.

**Budget evaluation in the write path, raising a `BudgetExceeded` event.** Captures the exact
moment of crossing, and leaves the consumer dumb — it stores and serves whatever arrives. Rejected
because it designs one event's contract before the epic that owns all of them exists, ships an
outbox nothing reads, and makes every transaction create pay for a budget query. Its only real
advantage, knowing *when* the crossing happened, is recoverable later from the data.

**The crossing stored on the Budget row** — `ExceededAt`, `ExceededBy`, updated on write. Cheapest
way to answer "when did I go over". Rejected because it is derived state stored next to the data
it derives from: edit or delete the **Expense** and the columns lie. That is exactly the
contradiction the derived **Transaction kind** was introduced to make impossible
([ADR 0001](0001-one-transaction-entity-with-two-nullable-sides.md)).

## Consequences

Nothing tells you that you went over until you look, or until the notifications feature exists.
That is the accepted cost of not guessing that feature's shape from inside this one.

`Transaction` keeps its current invariants and `CreateTransaction` its current cost. A future
change that adds a budget guard to either is reversing this decision, not extending it.

"You went over on the 14th" is not answerable yet. It becomes answerable when the notification
consumer starts recording what it observed, without any change here.
