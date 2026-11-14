---
status: accepted
date: 2026-08-05
---

# A budget reports and never blocks

## Decision

Recording an expense succeeds even when it exceeds a budget. Budget spending and remaining amounts are read-side calculations. A notification consumer can decide whether a transaction crossed a limit.

## Context

Xpense records money that has already moved. Rejecting the record would make the ledger incomplete. Checking budgets during transaction creation would also couple the write path to notification policy.

## Alternatives considered

- **Reject an expense over budget:** rejected because the real payment has already happened.
- **Check budgets on the write path and emit a budget event:** rejected because it couples transaction creation to budgets and adds a query to every write.
- **Store the crossing on the budget:** rejected because edits or deletions can make stored derived state inaccurate.

## Consequences

The transaction write path has no budget guard. A notification consumer can calculate a crossing from the transaction and budget. Until a consumer records it, the exact crossing is not available as a stored notification.
