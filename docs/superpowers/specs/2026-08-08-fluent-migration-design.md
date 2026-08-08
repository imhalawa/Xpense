# Migrating Xpense.Web to Fluent UI React

Date: 2026-08-08
Status: implemented; its large page-header treatment is superseded by
`2026-08-08-web-shell-and-filtering-design.md`
Scope: `web/` only. No API changes.
Supersedes: most of `2026-08-07-design-standard-design.md`. See "What survives from the design standard".

## Why

The app was rebuilt on MUI with a soft-card character. Once real screens existed, the verdict was
that it "feels like bootstrap 3": no visual hierarchy, a flat grey background, a table that reads as
a slab, and chrome that does not match the cards it contains.

The decision is not to patch that. It is to adopt **Fluent Design end to end**, keeping the brand
colour. Fluent brings the thing the current app lacks: a defined type ramp, layered neutral
surfaces, a shadow ladder, and a prescribed navigation pattern — instead of per-component taste.

This is a rewrite of the web client, not a restyle. It was chosen with that cost stated.

## Verified before committing to it

Checked against installed packages, not documentation:

| Package | Version | React peer | Verdict |
|---|---|---|---|
| `@fluentui/react-components` | 9.74.5 | `>=16.14.0 <20.0.0` | React 19.2.8 supported |
| `@fluentui/react-nav` | 9.4.3 | `>=16.14.0 <20.0.0` | Stable, **not** preview |
| `@fluentui/react-icons` | 2.0.335 | — | ~2000 icons |
| `@fluentui/react-datepicker-compat` | 0.6.35 | `>=16.8.0 <20.0.0` | Works, but see risks |

One trap avoided: `@fluentui/react-nav-preview` 0.13.9 caps at **`<19.0.0`** and would not work here.
The stable bundle depends on `@fluentui/react-nav`, not on the preview package, so `NavDrawer` is
available without it. Do not install the preview package.

Components confirmed present in the stable package by parsing its type declarations: `NavDrawer`,
`Drawer`, `InlineDrawer`, `TabList`, `Tab`, `DataGrid`, `DataGridRow`, `Combobox`, `Option`,
`Dialog`, `Toolbar`, `Badge`, `CounterBadge`, `Field`, `Switch`, `ToggleButton`, `ProgressBar`,
`Divider`, `Card`, `CardHeader`, `Menu`, `Checkbox`, `FluentProvider`, `webLightTheme`,
`webDarkTheme`, `createLightTheme`, `createDarkTheme`.

`DatePicker` is **absent** from the stable package. It exists only in the compat build.

## Decisions

**MUI is removed entirely.** `@mui/material`, `@mui/x-date-pickers`, `@mui/x-charts`,
`@emotion/react` and `@emotion/styled` all go. `@mui/x-charts` is used by exactly one file,
`XpenseLineCharts.tsx`, which nothing imports — deleting it removes charts as a dependency concern
today.

**Charts come from `@fluentui/react-charts` 9.3.23**, which is v9-native and allows React
`<20.0.0`. This was researched after the first draft of this spec claimed Fluent had no chart story;
that claim was wrong. It covers every form the information-architecture spec calls for:

| Analytic | Component |
|---|---|
| Q2 spending by category and merchant | `HorizontalBarChartWithAxis` |
| Q3 trend and cashflow | `LineChart`, `AreaChart` |
| Q5 essential versus discretionary | `VerticalStackedBarChart` |
| Overview cashflow sparkline | `Sparkline` |
| Legends for any multi-series chart | `Legends` |

Also available and unused for now: `GaugeChart`, `DonutChart`, `HeatMapChart`, `ScatterChart`,
`FunnelChart`, `SankeyChart`, `GanttChart`, `PolarChart`, `ChartTable`. Series colours are set per
data point, so the validated categorical palette carries over unchanged. `DonutChart` stays
rejected for spending-by-category — the reason was never the library, it was that a many-sliced
pie is the wrong form.

**Preview packages are banned.** `@fluentui/react-nav-preview`, `@fluentui/react-search-preview`
and `@fluentui/react-list-preview` all cap at React `<19.0.0` and cannot be installed here. Check
the `react` peer range of any `@fluentui/*` package before adding it.

**Icons come from `@fluentui/react-icons`.** The 25 custom SVG icons and their base `Icon` component
are deleted, along with `icons.test.tsx`. This is a deliberate loss: the sun, moon and monitor icons
were liked. Consistency with one complete, maintained set is what "Fluent end to end" means.

**Typography uses Fluent's own font stack**, which `webLightTheme` already defines. Fluent's real
typeface is Segoe UI Variable, which is Windows-licensed and cannot be shipped as a webfont. On
macOS the stack resolves to the system face. Manrope is dropped. Selawik, Microsoft's open
metric-compatible Segoe substitute, is the fallback option if the system face proves wrong — noted,
not adopted.

**Navigation becomes a left `NavDrawer`.** The top bar is removed. The theme toggle and the
notification entry point move into the nav. This is Fluent's prescribed pattern and it settles the
open "navbar or sidebar" question.

## Theme

`createLightTheme(brand)` and `createDarkTheme(brand)` each take a `BrandVariants` — sixteen steps
named 10 through 160.

The ramp is generated from **`#1565C0`**, interpolated in OKLCH so the steps are perceptually even,
and anchored so that one step is the brand colour exactly rather than approximately. The generated
ramp is then checked with the same contrast helper already in the repo, so the accent stays legible
on both Fluent surfaces.

**The data-visualisation palette does not change.** The eight categorical hues, the five-step
priority ordinal ramp and the blue/red diverging pair were validated for colour-blind separation and
contrast against specific surfaces. Fluent has no opinion on chart colour, and re-deriving them
would discard measured work. They stay in `tokens.ts` as plain data, with the surface values updated
to Fluent's neutral layers so the validation still refers to the surfaces actually used.

Radii, spacing, elevation and type sizes all come from Fluent tokens. The design standard's 16px
cards, pill buttons, 8px spacing scale and Manrope ramp are replaced.

**Hierarchy comes from Fluent's typography components, not from ad-hoc `sx` sizes.** The stable
package exports `Display`, `LargeTitle`, `Title1`, `Title2`, `Title3`, `Subtitle1`, `Subtitle2`,
`Body1`, `Body2`, `Caption1` and a generic `Text`. Using them is what fixes the flatness complaint:
a page title is `Title2`, a card heading is `Subtitle1`, a stat value is `Title1`, supporting text is
`Caption1`. The rule is that no component sets a font size by hand — if a size is needed that the
ramp does not have, the ramp is the thing to question.

## Shell and page structure

- `FluentProvider` at the root, switching between the generated light and dark themes. It replaces
  `ThemeProvider` **and** `CssBaseline`.
- `NavDrawer` with the four destinations: Overview, Transactions, Budgets, Manage.
- A page header component: title, optional supporting context, optional actions — replacing the
  current `Page`, whose header treatment is the specific thing that read as unstyled.
- Content sits on Fluent's layered surfaces rather than one flat grey plane.

## Component mapping

| Today | Becomes |
|---|---|
| `ThemeProvider` + `CssBaseline` | `FluentProvider` |
| `AppBar` + `Toolbar` nav | `NavDrawer` |
| `Page` | new page header + content region |
| `Paper`, `Card` | `Card`, `CardHeader` |
| `Table` and the custom `DataGrid` | `DataGrid` |
| `Autocomplete` (merchant, tag, category, account) | `Combobox` with `freeform` |
| `TextField` | `Field` + `Input` |
| `Select` | `Field` + `Dropdown` |
| `@mui/x-date-pickers` `DatePicker` | `@fluentui/react-datepicker-compat` `DatePicker` |
| `Dialog` | `Dialog` |
| `LinearProgress` | `ProgressBar` |
| `Badge` | `CounterBadge` |
| `Chip` | `Badge` |
| `ToggleButtonGroup` | `ToggleButton` group or `TabList` |
| `Popover` | `Popover` or `Menu` |
| `Alert` | `MessageBar` |
| `Pagination` | built from `Button` + icons; Fluent has no pagination component |

## Accessibility must not regress

The current theme carries a focus ring and a `prefers-reduced-motion` block in
`MuiCssBaseline.styleOverrides`. **Both are currently dead** — `CssBaseline` was mounted outside
`ThemeProvider`, so it never read the theme and never applied them. The same bug is why page
headings render black in dark mode.

The migration removes the cause, but the requirements carry over and must be reimplemented against
`FluentProvider`:

- A visible focus indicator on every interactive element. Fluent supplies one by default; confirm it
  is not suppressed rather than assuming.
- `prefers-reduced-motion` honoured.
- Colour never the only signal: the delta chip's arrow, the notification kind icon's title, and the
  unread count's accessible name all survive.

## Notifications become an actionable list

Checked against `Features/Notifications/` rather than assumed:

| Action | Endpoint | Buildable now |
|---|---|---|
| Mark one read | `PATCH /api/v1/notifications/{id}/read` | Yes |
| Mark all read | `POST /api/v1/notifications/read-all` | Yes |
| Select several and mark read | none | Yes — N calls to the single endpoint |
| Paging | `GET /api/v1/notifications?page&pageSize` | Yes |
| **Dismiss or delete** | **none** | **No — needs an endpoint** |
| **Mark as unread** | **none** | **No — needs an endpoint** |

So the list ships with per-row read, multi-select with a "mark selected read" action, mark-all, and
paging. Dismiss and un-read are **not stubbed**; they are absent until the API supports them, and
that belongs in its own spec.

## What survives from the design standard

Kept: the brand colour `#1565C0`, the categorical palette, the priority ordinal ramp, the
income/expense diverging pair, the two-red-step rule for ordinary versus alert expenses, the delta
chip's meaning, the never-sum-across-currencies rule, and every accessibility requirement.

Replaced: character, radii, spacing scale, elevation model, density tokens, typography, and the
component library itself.

## Testing

**A Vitest setup file is a prerequisite.** jsdom does not implement `window.matchMedia`, and Fluent
v9 needs it — as does any colour-scheme detection. The project currently has no `setupFiles` entry
at all, which is also why `@testing-library/jest-dom` was installed months ago and never wired up.
Add `web/vitest.setup.ts` mocking `matchMedia` and importing `@testing-library/jest-dom`, and
reference it from `vitest.config.ts`. Without it the component suite fails at import time, not with
a useful assertion error.

Eight test files are library-agnostic and must pass **unchanged** — treat any edit to them as a
signal that behaviour was broken: `budgetFormRules`, `budgetProgress`, `formatMoney`, `contrast`,
`density`, `useDebouncedValue`, `clients/options`, `clients/transactions`.

Reworked: `theme.test.ts` and `tokens.test.ts` assert MUI theme shape and surface values.
Deleted: `icons.test.tsx`.
Rewritten: `AccountBalances`, `BudgetForm`, `BudgetMeter`, `DeltaChip`, `NotificationBell`,
`ThemeModeToggle`.

The 178 API tests are untouched.

## Risks

- **The date picker is the weakest dependency.** Version 0.6.35, a v8 compatibility layer rather than
  a native v9 component. It works on React 19, but it is the piece most likely to look or behave
  slightly off, and the least likely to improve.
- **No pagination component.** It gets built from primitives.
- **`@fluentui/react-charts` is unproven in this codebase.** It resolves the chart question on
  paper, but nothing here renders one yet. The Insights plan should build the smallest real chart
  early rather than discovering its API at the end.
- **A big-bang branch means a long unreviewable stretch.** Chosen deliberately over incremental.
  The mitigation is that the API and the logic tests are untouched, so the blast radius is the view
  layer only.

## Out of scope

- Any API change.
- Transaction edit and delete — already specced separately.
- The Insights page and its analytics endpoints.
- Auth and users.
- Notification dismiss and mark-as-unread.

## Open questions

- ~~Insights chart library.~~ **Resolved:** `@fluentui/react-charts` 9.3.23.
- **Whether the system font is acceptable on macOS**, or whether Selawik should be shipped to get
  closer to Segoe metrics. Answerable only by looking at it.
