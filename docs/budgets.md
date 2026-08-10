# Budgets

A budget sets a limit for one category over a repeating period and reports how you are doing
against it.

![Budgets](images/budgets.png)

## A budget reports, and never blocks

Nothing is ever refused because a budget would be exceeded. The money already moved — Xpense is
recording history, not authorising payments. Going over a budget changes what you are told, never
what you can record. See
[ADR 0006](../api/docs/adr/0006-a-budget-reports-and-never-blocks.md).

## Fields

| Field | Notes |
| --- | --- |
| Category | Exactly one. Cannot be changed after creation |
| Amount | Required, positive, with a currency |
| Recurrence | `Weekly`, `Monthly`, `Yearly` or `None` |
| Starts on | Required |
| Ends on | Optional, unless recurrence is `None` |
| Alert threshold | Optional percentage, 1–100. Defaults to 75 |

Two rules the form enforces:

- A budget that does not repeat **must** say when it ends. A one-off limit with no end is not a
  budget, it is a wish.
- The end may not come before the start.

## Periods

Each budget measures inside a named window, so the client, the reports and the notifications all
agree on which window they mean:

| Recurrence | Window | Name |
| --- | --- | --- |
| Weekly | ISO week, starting Monday | `2026-W32` |
| Monthly | Calendar month | `2026-08` |
| Yearly | Calendar year | `2026` |
| None | The stated start to the stated end | `2026-08-01..2026-08-31` |

## What counts

Only **expenses** in the budget's category. Income never counts. Transfers never count — moving
your own money between your own accounts is not spending.

Spending is measured by when the money moved, not when you typed it in. A receipt entered today for
a purchase made last month counts against last month.

## What you are told

- **Spent** — the total so far in this period.
- **Remaining** — the limit minus what you spent. It goes negative once you pass the limit, and it
  is deliberately not called a balance.
- **Over budget** — shown once spending passes the limit.
- **Days left** — how much of the period remains.

## Uncounted in another currency

A budget is denominated in one currency. Spending on the same category in a *different* currency
cannot count against it, because Xpense never converts. Rather than dropping that spending
silently, the budget reports it separately:

> Uncounted in another currency: US$7.00

That line is the point of the feature. Money that vanishes from a report without a word is the
failure this exists to prevent. If you want that spending tracked, create a second budget in that
currency — two currencies of groceries means two budgets.

## Budgets are independent

Several budgets may cover the same category at once, and none of them knows about the others. A
tight weekly coffee budget and a loose yearly one can coexist and each reports on its own terms.
See [ADR 0007](../api/docs/adr/0007-budgets-are-independent-of-one-another.md).

## Notifications

Crossing the limit raises a notification, once, on the crossing itself — not on every later
transaction that stays over. See [Notifications](notifications.md).

The **alert threshold** field is stored and reported but does **not** raise a notification yet.
Only the over-limit crossing notifies today.
