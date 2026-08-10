# Fluent UI Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace MUI with Fluent UI React across the whole web client, keeping the brand colour, the validated data-visualisation palette and every accessibility guarantee.

**Architecture:** One `FluentProvider` at the root switching between a light and dark theme generated from a brand ramp anchored on `#1565C0`. Hierarchy comes from Fluent's typography components, never hand-set font sizes. Navigation is a left `NavDrawer`. All chrome tokens come from Fluent; only the chart palette stays ours, because it was validated for colour-blind separation.

**Tech Stack:** React 19.2.8, `@fluentui/react-components` 9.74.5, `@fluentui/react-icons` 2.0.335, `@fluentui/react-charts` 9.3.23, `@fluentui/react-datepicker-compat` 0.6.35, Vite 8, TypeScript 7, Vitest 4.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-08-fluent-migration-design.md`.
- **No comments in any file.** Project-wide rule.
- Explicit names. No `ct`, `db`, `el`, `val`, single letters.
- **Never install an `@fluentui/*-preview` package.** `react-nav-preview`, `react-search-preview` and `react-list-preview` all cap at React `<19.0.0`. Check the `react` peer range before adding any Fluent package.
- **No hand-set font sizes.** Use `Display`, `LargeTitle`, `Title1/2/3`, `Subtitle1/2`, `Body1/2`, `Caption1`. If the ramp lacks a size you want, question the design, not the ramp.
- **No hardcoded colours.** Use Fluent theme tokens via `tokens.*` from `@fluentui/react-components`, or the validated chart palette in `src/theme/tokens.ts`.
- **Never sum across currencies.** One figure per currency; the API has no exchange rate.
- **`Uncounted` budget spending stays visible** wherever a budget is shown.
- Styling uses Griffel `makeStyles`, not inline style objects, not Emotion.
- Never use MUI. By the end, `grep -rn "@mui\|@emotion" src package.json` must be empty.

## Already done — do not redo

Commit `505455b` proved the toolchain and left these in place:

- All four Fluent packages installed.
- `web/vitest.setup.ts` mocking `window.matchMedia` and stubbing `HTMLCanvasElement.getContext`. **The canvas stub is required** — Fluent charts measure label widths through a canvas context that jsdom does not implement, and without it every chart test floods the output with "Not implemented" errors.
- `vitest.config.ts` references the setup file.
- `src/fluent/spike.test.tsx` — 4 passing tests covering `FluentProvider`, theme generation from a brand ramp, the typography and card components, and a real `LineChart`. **Delete this file in the final task**, once real components cover the same ground.
- A draft 16-step brand ramp inside the spike, anchored so step 80 is exactly `#1565C0`.

Baseline: **268 web tests passing, 178 API tests passing.** The API is not touched by this plan.

## Test files that must pass UNCHANGED

Any edit to these means behaviour broke, not that the test needs updating:

`src/budgets/budgetFormRules.test.ts`, `src/budgets/budgetProgress.test.ts`,
`src/money/formatMoney.test.ts`, `src/theme/contrast.test.ts`, `src/theme/density.test.ts`,
`src/hooks/useDebouncedValue.test.ts`, `src/clients/options.test.ts`,
`src/clients/transactions.test.ts`.

---

### Task 1: The brand ramp and themes

**Files:**
- Create: `web/src/fluent/brand.ts`, `web/src/fluent/brand.test.ts`
- Create: `web/src/fluent/theme.ts`, `web/src/fluent/theme.test.ts`

**Interfaces:**
- Produces: `brandRamp: BrandVariants`, `lightTheme`, `darkTheme`. Every later task consumes the themes.

- [ ] **Step 1: Write the failing test**

`brand.test.ts` asserts the properties that matter rather than exact hexes:

```ts
import { describe, expect, it } from "vitest";
import { contrastRatio } from "../theme/contrast";
import { brandRamp } from "./brand";

const steps = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160] as const;

describe("brandRamp", () => {
  it("has all sixteen Fluent steps", () => {
    steps.forEach((step) => expect(brandRamp[step]).toMatch(/^#[0-9a-fA-F]{6}$/));
  });

  it("contains the brand colour exactly", () => {
    expect(Object.values(brandRamp).map((hex) => hex.toLowerCase())).toContain("#1565c0");
  });

  it("gets lighter as the step number rises", () => {
    const ratios = steps.map((step) => contrastRatio(brandRamp[step], "#ffffff"));
    const descending = [...ratios].sort((first, second) => second - first);
    expect(ratios).toEqual(descending);
  });

  it("keeps the accent readable on a white surface", () => {
    expect(contrastRatio(brandRamp[80], "#ffffff")).toBeGreaterThanOrEqual(4.5);
  });
});
```

`theme.test.ts` asserts both themes build and differ:

```ts
import { describe, expect, it } from "vitest";
import { darkTheme, lightTheme } from "./theme";

describe("themes", () => {
  it("builds a light and a dark theme", () => {
    expect(lightTheme.colorBrandBackground).toBeDefined();
    expect(darkTheme.colorBrandBackground).toBeDefined();
  });

  it("gives them different neutral surfaces", () => {
    expect(lightTheme.colorNeutralBackground1).not.toBe(darkTheme.colorNeutralBackground1);
  });
});
```

- [ ] **Step 2: Run both to verify they fail**

Run: `cd web && npx vitest run src/fluent`
Expected: unresolved imports.

- [ ] **Step 3: Implement**

Move the ramp out of `spike.test.tsx` into `brand.ts`. If the monotonic-lightness test fails, re-derive the ramp by interpolating in OKLCH between a near-black and a near-white anchored so one step is exactly `#1565C0` — do not hand-tweak individual steps until the test passes, since that is how a ramp ends up perceptually uneven.

`theme.ts` is `createLightTheme(brandRamp)` and `createDarkTheme(brandRamp)`.

- [ ] **Step 4: Run to verify they pass, then commit**

```bash
git add web/src/fluent/
git commit -m "feat: generate fluent themes from the xpense brand ramp"
```

---

### Task 2: Root provider and colour scheme

**Files:**
- Modify: `web/src/App.tsx`
- Create: `web/src/fluent/useColorScheme.ts`, `web/src/fluent/useColorScheme.test.ts`

**Interfaces:**
- Produces: `useColorScheme()` returning `{ mode, setMode, resolved }` where mode is `"light" | "dark" | "system"`.

This replaces MUI's `ThemeProvider`, `CssBaseline`, `useColorScheme` and `InitColorSchemeScript` in one move.

**Note the bug being fixed:** `CssBaseline` currently sits *outside* `ThemeProvider`, so it never read the theme. That is why headings render black in dark mode and why the focus ring and reduced-motion rules never applied. Do not reproduce the shape — `FluentProvider` must wrap everything that renders.

- [ ] **Step 1: Write the failing test** — mode defaults to `system`, `setMode` persists to `localStorage`, and a stored mode is read back on init.
- [ ] **Step 2: Run it, confirm it fails.**
- [ ] **Step 3: Implement the hook, then wrap `App` in `FluentProvider theme={resolved === "dark" ? darkTheme : lightTheme}`.** Keep `LocalizationProvider` out — it is MUI and goes away with the date pickers in Task 6.
- [ ] **Step 4: Verify and commit.**

---

### Task 3: The shell — NavDrawer and page header

**Files:**
- Create: `web/src/shell/AppShell.tsx`, `web/src/shell/AppShell.test.tsx`
- Create: `web/src/shell/PageHeader.tsx`, `web/src/shell/PageHeader.test.tsx`
- Modify: `web/src/pages/Layout.tsx`
- Delete: `web/src/components/Page/Page.tsx`

`NavDrawer` with four destinations — Overview `/`, Transactions `/transactions`, Budgets `/budgets`, Manage `/settings` — using `@fluentui/react-icons`. The drawer is `InlineDrawer` open on wide screens and an overlay `Drawer` on narrow ones.

`PageHeader` takes `title`, optional `description`, optional `actions`. Title renders as `Title2`. **This replaces the old `Page`, whose unstyled header was the specific thing that read as Bootstrap.**

Tests: all four destinations render as links, the active one carries `aria-current="page"`, and `PageHeader` renders its title and actions.

- [ ] Steps: failing test, confirm red, implement, verify, commit.

---

### Task 4: Shared primitives

**Files:**
- Rewrite: `web/src/components/DeltaChip/DeltaChip.tsx` and its test
- Rewrite: `web/src/components/StatTile/StatTile.tsx`
- Rewrite: `web/src/components/BudgetMeter/BudgetMeter.tsx` and its test
- Rewrite: `web/src/components/AccountBalances/AccountBalances.tsx` and its test

`DeltaChip` becomes a Fluent `Badge` with an arrow icon. `StatTile` becomes a `Card` with `Caption1` label and `Title1` value. `BudgetMeter` uses `ProgressBar`. `AccountBalances` is a grid of `StatTile`.

**Behaviour that must survive, asserted by the existing tests:** the delta chip's direction arrow has an accessible name; the budget meter exposes progress to assistive technology, shows uncounted spending, and states when a budget is not measuring; account balances never render a cross-currency total.

- [ ] Steps per component: adapt the test to Fluent's DOM, confirm it fails, implement, verify, commit.

---

### Task 5: Overview and Budgets pages

**Files:**
- Rewrite: `web/src/pages/Overview/Overview.tsx`
- Rewrite: `web/src/pages/Budgets/Budgets.tsx`
- Rewrite: `web/src/components/BudgetForm/BudgetForm.tsx` and its test

`BudgetForm` maps to Fluent: `Field` + `Input` for amount, `Field` + `Dropdown` for category and currency, `ToggleButton` group for the threshold quick picks, `DatePicker` from `@fluentui/react-datepicker-compat` for the dates, `Dialog` for the container.

`budgetFormRules.ts` does **not** change — its 16 tests must pass untouched.

- [ ] Steps: failing tests, confirm red, implement, verify, commit.

---

### Task 6: Transactions page

**Files:**
- Rewrite: `web/src/pages/Transactions/Transactions.tsx`
- Rewrite: `web/src/pages/Transactions/TransactionsGrid/TransactionsGrid.tsx`
- Rewrite: `web/src/pages/Transactions/TransactionsForm/TransactionsForm.tsx`
- Rewrite: the four `web/src/components/Forms/AutoComplete/*` components
- Delete: `web/src/components/DataGrid/DataGrid.tsx`

Fluent `DataGrid` replaces both the custom `DataGrid` and MUI's `Table`. The autocompletes become `Combobox` with `freeform`, keeping inline merchant and tag creation and the debounced server-side search added earlier — `useDebouncedValue` and the client tests must not change.

**Fluent has no `Pagination` component.** Build one from `Button` plus chevron icons.

Amount column right-aligned with tabular figures; that is what makes a money column readable.

- [ ] Steps: failing tests, confirm red, implement, verify, commit.

---

### Task 7: Notifications as an actionable list

**Files:**
- Rewrite: `web/src/components/NotificationBell/NotificationBell.tsx` and its test
- Create: `web/src/components/NotificationList/NotificationList.tsx` and its test

Per-row read, multi-select with a "mark selected read" action, mark-all, and paging. Selection uses `Checkbox`; "mark selected read" calls the existing single-notification endpoint once per selected id.

**Dismiss and mark-as-unread do not exist in the API and must NOT be rendered**, not even disabled. Build only what `Features/Notifications/` supports: `ListNotifications`, `GetUnreadNotificationCount`, `MarkNotificationRead`, `MarkAllNotificationsRead`.

Keep the coloured kind icon with its accessible title and the divider between rows.

- [ ] Steps: failing tests, confirm red, implement, verify, commit.

---

### Task 8: Remove MUI and clean up

**Files:**
- Modify: `web/package.json`
- Delete: `web/src/icons/`, `web/src/theme/theme.ts`, `web/src/theme/theme.test.ts`, `web/src/components/Charts/`, `web/src/fluent/spike.test.tsx`
- Modify: `web/src/theme/tokens.ts` and its test — keep the chart palette, drop MUI-shaped chrome tokens, update surfaces to the Fluent neutrals actually used

- [ ] **Step 1: Uninstall**

```bash
cd web && npm uninstall @mui/material @mui/x-charts @mui/x-date-pickers @emotion/react @emotion/styled @fontsource/manrope
```

- [ ] **Step 2: Prove it is gone**

```bash
grep -rn "@mui\|@emotion\|lucide\|manrope" src package.json
```

Must be empty. A single missed import keeps the whole library in the bundle.

- [ ] **Step 3: Accessibility check, which the old code silently failed**

Assert in a test that a visible focus indicator exists and that `prefers-reduced-motion` is honoured. Fluent supplies focus styling by default — **confirm it renders rather than assuming it, because the previous implementation looked correct and did nothing.**

- [ ] **Step 4: Full verification**

```bash
cd web && npm test && npm run build && npm run check:init
cd ../api && dotnet test src/Xpense/Xpense.sln
```

- [ ] **Step 5: Commit.**

---

## Self-review

**Spec coverage.** Theme and brand ramp in Task 1. `FluentProvider` replacing the broken `CssBaseline` in Task 2. `NavDrawer` and the page header in Task 3. Component mapping across Tasks 4 to 7. Icon and MUI removal in Task 8. Charts are installed and proven but not yet used — Insights does not exist, and building a chart with no page to hold it would be speculative.

**Deliberately not here.** The Insights page. Transaction edit and delete, which is specced separately and needs API work first. Auth. Notification dismiss and mark-as-unread, which have no endpoint.

**Type consistency.** `brandRamp` is the only export of `brand.ts`; `lightTheme` and `darkTheme` the only exports of `theme.ts`. `budgetProgress`, `formatMoney`, `validateBudgetForm`, `toCreateRequest` and `useDebouncedValue` keep their existing signatures — none of them is a view concern.

**Two risks for the executor.**
1. **The date picker is the weakest dependency** at 0.6.35 and a v8 compat layer. If it fights React 19 or looks wrong, say so rather than working around it silently — a native `Input` with a date mask is an acceptable fallback and should be reported, not chosen quietly.
2. **The "unchanged tests" list is the safety net for the whole migration.** It is the only thing separating a UI rewrite from a behaviour rewrite. If one of those eight files needs editing, stop and report why — that is a signal the rewrite changed logic it should not have touched.
