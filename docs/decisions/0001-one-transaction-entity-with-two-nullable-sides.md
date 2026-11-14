---
status: accepted
date: 2026-08-04
---

# One transaction entity with two nullable sides

## Decision

Use one `Transaction` for income, expense, and transfer. Its source and destination accounts are nullable; a missing side represents a party outside Xpense. Derive the transaction kind from the two sides.

| Kind | Source | Destination |
| --- | --- | --- |
| Income | Outside Xpense | Account |
| Expense | Account | Outside Xpense |
| Transfer | Account | Account |

## Context

Xpense records money already moved elsewhere. The previous model used a one-sided transaction and a separate transfer with two legs. The intended model also needed to represent transfers between users as one shared record.

## Alternatives considered

- **Double-entry ledger:** rejected because Xpense is not a custodian and double-entry would not prevent missing or mistyped input. It also required modelling outside parties as accounts.
- **Movement log:** rejected because it could reconstruct balances but did not identify the counterparty.
- **One external account:** rejected because the merchant already identifies the outside party.
- **Rename transfer legs:** rejected because the legs duplicated information in the transfer.

## Consequences

The separate transfer and leg entities, tables, responses, and endpoints were replaced by transactions. Domain factories guard all three transaction kinds. A one-sided transaction requires a merchant and category; a transfer has neither.

At the time of the decision, source-balance validation for cross-user transfers and consent before changing another user's history remained unresolved. Subsequent access rules must be considered separately.

## Sources
- [Modern Treasury — How to Scale a Ledger, Part I](https://www.moderntreasury.com/journal/how-to-scale-a-ledger-part-i) and [Part V](https://www.moderntreasury.com/journal/how-to-scale-a-ledger-part-v)
- [Square — Books, an immutable double-entry accounting database service](https://developer.squareup.com/blog/books-an-immutable-double-entry-accounting-database-service/)
- [Martin Fowler — Accounting Patterns](https://martinfowler.com/apsupp/accounting.pdf)
- [Firefly III — Transactions and Journals](https://deepwiki.com/firefly-iii/firefly-iii/2.2-transactions-and-journals)
- [Firefly III is not double-entry accounting](https://www.kennethballard.com/?p=9483) — on why its one-to-many restrictions fall short of real double-entry
