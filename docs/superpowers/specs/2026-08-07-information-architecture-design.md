# Xpense information architecture and analytics

Date: 2026-08-07
Status: partially superseded; navigation and transaction-filter transport now follow
`2026-08-08-web-shell-and-filtering-design.md`. API extension 3 is cancelled because transaction
filters now run inside the local vault projection.
Depends on: `2026-08-07-design-standard-design.md`
Scope: page structure, navigation, and which analytics exist. Includes the API additions each
analytic needs. Excludes how transactions get entered — separate spec.

## Why

The web app has three pages. `Settings` is nearly empty. The Dashboard renders three of its four
charts from a hardcoded `areaChartData` constant, and its one real chart calls an endpoint that is
hardcoded to today.

Meanwhile the API implements two complete features the web app never calls: **Budgets** (create,
list, update, delete, recurrence, alert thresholds, per-period spend) and **Notifications**
(list, unread count, mark read, mark all read). The largest available improvement is not new
analytics — it is using what already exists.

## The eight questions

The app exists to answer these. Anything that answers none of them does not get built.

| | Question |
|---|---|
| Q1 | Am I on track this month? |
| Q2 | Where does my money go? |
| Q3 | Is my spending trending up? |
| Q4 | What exactly did I spend on X? |
| Q5 | How essential is my spending? |
| Q6 | How much do I actually have? |
| Q7 | Am I about to break a budget? |
| Q8 | What are my fixed monthly commitments? |

## Structure: five pages, one job each

Chosen over a four-page variant that folds analysis into Overview, and a three-page tabbed
variant. The deciding reason is **shipping order**: eight questions will not land at once, and
this is the only structure where each page is independently useful the day it ships. The
alternatives need their big page nearly finished before it stops looking broken.

| Page | Contents |
|---|---|
| **Overview** | Balances per account (Q6) · budget meters with burn rate (Q1, Q7) · cashflow sparkline (Q3) · last few transactions |
| **Transactions** | Filter by date, category, merchant, tag, account, amount (Q4) · entry and edit |
| **Insights** | A period switcher governs everything below: by category and by merchant (Q2) · trend over time (Q3) · essential vs discretionary (Q5) · recurring commitments (Q8) |
| **Budgets** | Create, edit, delete · per-budget progress and alerts (Q1, Q7) |
| **Manage** | Accounts, categories with their priorities, merchants, tags · theme toggle |

Navigation consolidates. Today there are three components — `NavigationBar`, `SideBar`,
`UtilitiesBar` — serving three pages. One remains: a top bar on desktop, a bottom bar on mobile.

A **notification bell with an unread count** lives in the top bar. The API supports it fully and
the web app calls none of it.

Known risk of this structure: Insights is the page that unclaimed features get dumped on. The
period switcher is its organising rule — anything that does not respond to the period switcher
does not belong on Insights.

## Form and API per question

Forms follow the data's job, not habit.

| Q | Form | Why that form | API |
|---|---|---|---|
| Q6 | KPI row of stat tiles, one per account, grouped per currency | Single current values, never a one-bar chart | `ListAccounts` — no change |
| Q1 | Meter per budget, plus days remaining and uncounted | A single ratio against a limit — not a two-slice pie | `ListBudgets` — no change |
| Q7 | Same meter with a projected marker, plus a delta chip | Δ to target | No change; burn rate is client-side |
| Q5 | One stacked horizontal bar, five-step ordinal ramp | Priority is an *ordered* scale, so one hue light→dark — not five identities | Add priority to the by-category response |
| Q2 | Horizontal bars sorted high→low; a table past ~7 categories | Magnitude comparison; part-to-whole with many classes is a table job | Extend by-category with `from`/`to`; add by-merchant |
| Q4 | A table | Not every answer is a chart | Extend `ListTransactions` filters |
| Q3 | Line; income vs expense as two series in the diverging pair | Trend over time | **New** time-series endpoint |
| Q8 | A table plus one stat tile for total committed | More than ~7 rows that all carry meaning | **New** endpoint, possibly a domain change |

### Q5 ordinal ramp

Five priorities are seeded. They were renamed on 2026-08-07 from urgency words to a necessity
scale, because "Extreme" is a strange label for rent and this scale is what drives the
essential-versus-discretionary reading. Weight ascends as necessity descends.

The rename also fixed a data bug. The original weights were Extreme 1, High 2, Medium 3, Low 4 and
**None 0** — not monotone, so `ORDER BY "Weight"` put None ahead of Extreme and this ordinal ramp
would have been applied in the wrong order. Nothing read Weight at the time, so the correction was
safe. See migration `RenamePrioritiesToNecessityScale`.

| Priority | Weight | Light | Dark |
|---|---|---|---|
| Essential | 1 | `#104281` | `#9ec5f4` |
| Important | 2 | `#1c5cab` | `#6da7ec` |
| Useful | 3 | `#2a78d6` | `#3987e5` |
| Optional | 4 | `#5598e7` | `#256abf` |
| Avoidable | 5 | `#86b6ef` | `#184f95` |

Because the scale is ordered, a five-hue categorical palette would be wrong — it implies identity
where the data means rank. One hue, monotone lightness.

Both directions validated as ordinal ramps: lightness monotone, every adjacent gap ≥ 0.06 ΔL,
light end 2.11:1 against its surface, hue spread 3°.

## Domain rules the UI must honour

**No currency conversion, anywhere.** There is no exchange rate in the domain. The analytics
response returns one total per currency and documents why: "adding amounts in different
currencies produces a number that is true of nothing." Every total, balance and budget figure is
per-currency. There is no net worth number and no all-currency total.

**Uncounted spending must be visible.** `BudgetPeriodResponse.Uncounted` is spending on a
budget's category, inside its period, in a different currency — reported rather than dropped
because "money that silently vanishes from a report is the failure this makes visible." A budget
meter that shows only spent and remaining breaks this rule. Uncounted gets its own line.

**Transfers are already excluded from spending.** `GetSpendingByCategory` filters
`SourceAccountId != null && DestinationAccountId == null`, so account-to-account movement never
inflates expenses. Any new analytics endpoint must repeat that filter.

**A budget period can be null.** `BudgetPeriodResponse` is null for a budget measuring nothing at
the moment asked about — before it starts, after it ends, or a one-off outside its window. The UI
needs a state for "this budget is not currently measuring", not a zeroed meter.

## API delta

Nothing needed:

- Q6 — `ListAccounts` returns label, number, balance, currency, default flag.
- Q1, Q7 — `ListBudgets` and `GetBudgetById` already embed `BudgetPeriodResponse` with `Spent`,
  `Remaining`, `Exceeded` and `Uncounted`. Burn rate is `Spent ÷ days elapsed` against the
  period's `From` and `ToExclusive`, computed client-side.
- Notifications — all four endpoints exist.

Small extensions:

1. `GET /api/v1/analytics/spending/by-category` — accept `from` and `to`. Currently hardcoded to
   `OccurredAt.Date == today`, which is why it cannot answer "last month".
2. Same endpoint — include each category's priority in the response. The query already loads it
   via `ThenInclude(category => category.Priority)` and discards it.
3. `GET /api/v1/transactions` — accept `categoryId`, `merchantId`, `tagId`, `accountId`,
   `minAmount`, `maxAmount`. Paging and date range already work.

New:

4. `GET /api/v1/analytics/spending/by-merchant` — mirrors by-category, same currency rules.
5. `GET /api/v1/analytics/spending/over-time` — buckets by day, week or month over a range, and
   returns income and expense separately so cashflow is one call. Nothing today can produce this.
6. A threshold notification. `Budget.AlertThreshold` is computed with a 75% default, but
   `NotificationKind` contains only `BudgetExceeded` — so the threshold is calculated and never
   acted on. Adding a kind for crossing it makes an existing unused field earn its place. Optional:
   Q7's on-screen meter works without it.
7. Recurring commitments (Q8). `Recurrence` exists on `Budget` but **not** on `Transaction`, so
   this is either heuristic inference (same merchant, similar amount, monthly cadence) or a domain
   change. See open questions.

## Deletions

- `XpensePieChart` and its use on the Dashboard. Spending by category is part-to-whole across
  more than seven classes, which is a bars-or-table job; a many-sliced pie is an anti-pattern.
  Sorted horizontal bars replace it.
- `areaChartData` and the three fake charts it feeds.

## Shipping order

Cheapest real value first, each step useful on its own:

1. Overview with balances and budget meters, plus the notification bell — **no API work**.
2. Budgets page — **no API work**.
3. Transactions filtering — extension 3.
4. Insights: by category and by merchant over a period, essential split — extensions 1, 2, 4.
5. Insights: trend and cashflow — new endpoint 5.
6. Threshold notification — 6.
7. Recurring commitments — 7.

## Out of scope

- How transactions get entered, and how form components stay relevant alongside AI. Separate spec.
- Visual tokens, colour, type, spacing, density. Settled in the design standard spec.

## Open questions

- **Q8 recurring detection.** Heuristic inference needs no schema change but will be wrong
  sometimes, and a wrong "your fixed costs are X" is worse than no answer. A `Recurrence` field on
  `Transaction` is accurate but means a migration and manual tagging at entry time. Not decided;
  it is last in the shipping order, so it can be decided later. It also overlaps the data-entry
  spec, since marking a transaction recurring is an entry-time act.
- ~~**Priority labels.**~~ **Resolved 2026-08-07.** Renamed to Essential, Important, Useful,
  Optional, Avoidable, with monotone weights 1–5. See the Q5 ordinal ramp section above.
