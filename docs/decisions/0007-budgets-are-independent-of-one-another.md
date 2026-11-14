---
status: accepted
date: 2026-08-05
---

# Budgets are independent

## Decision

Allow multiple budgets for the same category, currency, and period, including overlapping periods. Spent and remaining belong to each budget; no budget overrides another.

## Context

A user may want a weekly limit and a monthly limit for the same category, or an exceptional period alongside a recurring budget. A single remaining amount per category would require precedence rules that do not work consistently for arbitrary date ranges.

## Alternatives considered

- **One budget per period with overrides:** rejected because precedence is ambiguous across different period lengths.
- **Reject overlaps:** rejected because it prevents useful combinations and makes exceptions cumbersome.
- **Materialize a row for every period:** rejected because generation and synchronization add complexity that independent budgets do not need.

## Consequences

Clients list budgets and ask each for its remaining amount. Users can create contradictory budgets; Xpense reports them independently. Warnings could be added without making overlap invalid.
