# Overview Page and Notification Bell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fake-data Dashboard with a real Overview page showing account balances, budget meters with burn-rate projection, and a notification bell — using **only endpoints that already exist**.

**Architecture:** Thin typed axios clients mirror the API contracts exactly. All arithmetic lives in pure functions with unit tests — `budgetProgress` and `formatMoney` — so components stay dumb and the maths is provable. Components read theme roles and tokens, never raw hex.

**Tech Stack:** React 19, MUI 9.3.1, axios, dayjs, lucide-react, Vitest 4.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-07-information-architecture-design.md`. Depends on the design standard already merged into this branch's ancestry.
- **No comments in any file.** Project-wide rule.
- **Explicit names.** No `ct`, `db`, `cfg`, `acc`, `bal`.
- **No barrel value imports in `src/typings`.** `npm run check:init` must pass — it catches circular initialisation that `tsc` and `vite build` both miss.
- **Never sum across currencies.** One figure per currency. The API returns per-currency totals and documents why: "adding amounts in different currencies produces a number that is true of nothing." There is no FX rate in the domain.
- **`Uncounted` must be visible** wherever a budget is shown. It is spending in the budget's category and period but a different currency, reported rather than dropped because "money that silently vanishes from a report is the failure this makes visible."
- **A budget period can be null** — before a budget starts, after it ends, or a one-off outside its window. That is a distinct UI state, not a zeroed meter.
- Colour reach: every amount is coloured. Ordinary expenses use the muted red token, alerts the saturated one. Colour never carries meaning alone — always a sign or an arrow.

## API surface used — all pre-existing, zero API changes

| Endpoint | Returns |
|---|---|
| `GET /api/v1/accounts` | `AccountResponse[]` — `accountNumber`, `label`, `balance {minorUnits, currency}`, `isDefault` |
| `GET /api/v1/budgets?on=<iso>` | `BudgetResponse[]` — includes `period` with `spent`, `remaining`, `exceeded`, `uncounted[]`, `name`, `from`, `toExclusive`. `period` is nullable |
| `GET /api/v1/notifications?page&pageSize` | `NotificationPageResponse` — `notifications[]`, `page`, `pageSize`, `totalItems`, `totalPages`, `unreadItems` |
| `GET /api/v1/notifications/unread-count` | `{ unread: number }` |
| `PATCH /api/v1/notifications/{id}/read` | marks one read |
| `POST /api/v1/notifications/read-all` | marks all read |

## File Structure

| File | Responsibility |
|---|---|
| `web/src/clients/types.ts` | Modify: add budget and notification response interfaces |
| `web/src/clients/budgets.ts` | `listBudgets({ on })` |
| `web/src/clients/notifications.ts` | list, unread count, mark read, mark all read |
| `web/src/money/formatMoney.ts` | minor units to a display string, using the currency's real exponent |
| `web/src/money/formatMoney.test.ts` | Its tests |
| `web/src/budgets/budgetProgress.ts` | Pure burn-rate and state calculation |
| `web/src/budgets/budgetProgress.test.ts` | Its tests — the most important suite here |
| `web/src/components/StatTile/StatTile.tsx` | One headline figure with optional delta chip |
| `web/src/components/BudgetMeter/BudgetMeter.tsx` | One budget as a meter |
| `web/src/components/BudgetMeter/BudgetMeter.test.tsx` | Its tests |
| `web/src/components/NotificationBell/NotificationBell.tsx` | Bell, badge, popover list |
| `web/src/components/NotificationBell/NotificationBell.test.tsx` | Its tests |
| `web/src/pages/Overview/Overview.tsx` | Assembles the page |
| `web/src/pages/Dashboard/` | **Delete** — replaced by Overview |
| `web/src/components/Charts/XpensePie/` | **Delete** — wrong form for the data |

---

### Task 1: Budget and notification types and clients

**Files:**
- Modify: `web/src/clients/types.ts`
- Create: `web/src/clients/budgets.ts`
- Create: `web/src/clients/notifications.ts`

**Interfaces:**
- Consumes: `IMoneyResponse`, `ICategoryResponse` already in `types.ts`.
- Produces: `IBudgetResponse`, `IBudgetPeriodResponse`, `INotificationResponse`, `INotificationPageResponse`, `IUnreadCountResponse`; and functions `listBudgets`, `listNotifications`, `getUnreadCount`, `markNotificationRead`, `markAllNotificationsRead`.

- [ ] **Step 1: Add the response interfaces**

Append to `web/src/clients/types.ts`:

```ts
export interface IBudgetPeriodResponse {
  name: string;
  from: string;
  toExclusive: string;
  spent: IMoneyResponse;
  remaining: IMoneyResponse;
  exceeded: boolean;
  uncounted: IMoneyResponse[];
}

export interface IBudgetResponse {
  id: number;
  category: ICategoryResponse;
  amount: IMoneyResponse;
  recurrence: string;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
  period: IBudgetPeriodResponse | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface INotificationResponse {
  id: number;
  kind: string;
  title: string;
  message: string;
  payload: unknown;
  readAt: string | null;
  createdAt: string;
}

export interface INotificationPageResponse {
  notifications: INotificationResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  unreadItems: number;
}

export interface IUnreadCountResponse {
  unread: number;
}
```

- [ ] **Step 2: Create the budgets client**

`web/src/clients/budgets.ts`:

```ts
import axios from "axios";
import { Dayjs } from "dayjs";
import { IBudgetResponse } from "./types";

export const listBudgets = async (on?: Dayjs | null): Promise<IBudgetResponse[]> => {
  const response = await axios.get<IBudgetResponse[]>("/api/v1/budgets", {
    params: { on: on ? on.toISOString() : undefined },
  });
  return response.data;
};
```

- [ ] **Step 3: Create the notifications client**

`web/src/clients/notifications.ts`:

```ts
import axios from "axios";
import { INotificationPageResponse, IUnreadCountResponse } from "./types";

export const listNotifications = async (
  page: number,
  pageSize: number
): Promise<INotificationPageResponse> => {
  const response = await axios.get<INotificationPageResponse>("/api/v1/notifications", {
    params: { page, pageSize },
  });
  return response.data;
};

export const getUnreadCount = async (): Promise<number> => {
  const response = await axios.get<IUnreadCountResponse>("/api/v1/notifications/unread-count");
  return response.data.unread;
};

export const markNotificationRead = async (id: number): Promise<void> => {
  await axios.patch(`/api/v1/notifications/${id}/read`);
};

export const markAllNotificationsRead = async (): Promise<void> => {
  await axios.post("/api/v1/notifications/read-all");
};
```

- [ ] **Step 4: Verify it compiles and initialises**

Run: `cd web && npm run build && npm run check:init`
Expected: both exit 0.

- [ ] **Step 5: Commit**

```bash
git add web/src/clients/
git commit -m "feat: add budget and notification api clients"
```

---

### Task 2: formatMoney

`toSingle` in `src/typings/models/IMoney.ts` divides by 100 unconditionally. That is correct for EUR and USD and wrong for any currency without two decimal places — precisely what the API contract warns about. This replaces it for display purposes.

**Files:**
- Create: `web/src/money/formatMoney.ts`
- Create: `web/src/money/formatMoney.test.ts`

**Interfaces:**
- Consumes: `IMoneyResponse` from `clients/types.ts`.
- Produces: `formatMoney(money: IMoneyResponse): string` and `toMajorUnits(money: IMoneyResponse): number`.

- [ ] **Step 1: Write the failing test**

`web/src/money/formatMoney.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { formatMoney, toMajorUnits } from "./formatMoney";

describe("toMajorUnits", () => {
  it("shifts euro minor units by two places", () => {
    expect(toMajorUnits({ minorUnits: 4218, currency: Currency.EUR })).toBe(42.18);
  });

  it("handles a whole amount", () => {
    expect(toMajorUnits({ minorUnits: 240000, currency: Currency.EUR })).toBe(2400);
  });

  it("handles zero", () => {
    expect(toMajorUnits({ minorUnits: 0, currency: Currency.EUR })).toBe(0);
  });

  it("keeps a negative amount negative", () => {
    expect(toMajorUnits({ minorUnits: -4218, currency: Currency.EUR })).toBe(-42.18);
  });
});

describe("formatMoney", () => {
  it("formats euros with a symbol and two decimals", () => {
    const formatted = formatMoney({ minorUnits: 4218, currency: Currency.EUR });
    expect(formatted).toMatch(/42[.,]18/);
    expect(formatted).toMatch(/€/);
  });

  it("groups thousands", () => {
    expect(formatMoney({ minorUnits: 118060, currency: Currency.EUR })).toMatch(/1[.,\s]180[.,]60/);
  });

  it("formats dollars with a dollar sign", () => {
    expect(formatMoney({ minorUnits: 999, currency: Currency.USD })).toMatch(/\$/);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/money/formatMoney.test.ts`
Expected: FAIL — cannot resolve `./formatMoney`.

- [ ] **Step 3: Write the implementation**

`web/src/money/formatMoney.ts`:

```ts
import { IMoneyResponse } from "../clients/types";

const displayLocale = "en-GB";

const fractionDigitsFor = (currency: string): number =>
  new Intl.NumberFormat(displayLocale, { style: "currency", currency }).resolvedOptions()
    .maximumFractionDigits ?? 2;

export const toMajorUnits = (money: IMoneyResponse): number => {
  const digits = fractionDigitsFor(money.currency);
  return money.minorUnits / 10 ** digits;
};

export const formatMoney = (money: IMoneyResponse): string =>
  new Intl.NumberFormat(displayLocale, {
    style: "currency",
    currency: money.currency,
  }).format(toMajorUnits(money));
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/money/
git commit -m "feat: format money using the currency's real exponent"
```

---

### Task 3: budgetProgress

The most important task in this plan. Every number the Overview shows about budgets comes from here, and none of it is testable through the UI.

Burn rate is `spent / daysElapsed`, projected to the whole period. Days elapsed counts the current day as started, so a period's first day gives `daysElapsed = 1` rather than 0 — dividing by zero on day one is the obvious bug this avoids.

**Files:**
- Create: `web/src/budgets/budgetProgress.ts`
- Create: `web/src/budgets/budgetProgress.test.ts`

**Interfaces:**
- Consumes: `IBudgetResponse`, `IBudgetPeriodResponse` from `clients/types.ts`; `toMajorUnits` from `money/formatMoney.ts`; `dayjs`.
- Produces:
```ts
type BudgetState = "not-measuring" | "on-track" | "projected-over" | "threshold-passed" | "exceeded";
interface BudgetProgress {
  state: BudgetState;
  spentRatio: number;
  projectedRatio: number;
  daysElapsed: number;
  daysTotal: number;
  daysRemaining: number;
  hasUncounted: boolean;
}
budgetProgress(budget: IBudgetResponse, now: Dayjs): BudgetProgress
```

- [ ] **Step 1: Write the failing test**

`web/src/budgets/budgetProgress.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import dayjs from "dayjs";
import { Currency } from "../typings/enums/Currency";
import { IBudgetResponse } from "../clients/types";
import { budgetProgress } from "./budgetProgress";

const euros = (minorUnits: number) => ({ minorUnits, currency: Currency.EUR });

const budgetWith = (
  period: IBudgetResponse["period"],
  amountMinorUnits = 100000,
  alertThresholdPercent: number | null = 75
): IBudgetResponse => ({
  id: 1,
  category: {
    id: 1,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 1, createdAt: "", updatedAt: null },
    createdAt: "",
    updatedAt: null,
  },
  amount: euros(amountMinorUnits),
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent,
  period,
  createdAt: "",
  updatedAt: null,
});

const monthPeriod = (spent: number, remaining: number, exceeded = false, uncounted = []) => ({
  name: "2026-08",
  from: "2026-08-01T00:00:00Z",
  toExclusive: "2026-09-01T00:00:00Z",
  spent: euros(spent),
  remaining: euros(remaining),
  exceeded,
  uncounted,
});

describe("budgetProgress", () => {
  it("reports not-measuring when the budget has no period", () => {
    const progress = budgetProgress(budgetWith(null), dayjs("2026-08-15T12:00:00Z"));
    expect(progress.state).toBe("not-measuring");
  });

  it("counts the first day of a period as one day elapsed, never zero", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(1000, 99000)),
      dayjs("2026-08-01T06:00:00Z")
    );
    expect(progress.daysElapsed).toBe(1);
    expect(Number.isFinite(progress.projectedRatio)).toBe(true);
  });

  it("counts the days in the period", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(1000, 99000)),
      dayjs("2026-08-10T00:00:00Z")
    );
    expect(progress.daysTotal).toBe(31);
    expect(progress.daysElapsed).toBe(10);
    expect(progress.daysRemaining).toBe(21);
  });

  it("computes the spent ratio against the budget amount", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(25000, 75000)),
      dayjs("2026-08-10T00:00:00Z")
    );
    expect(progress.spentRatio).toBeCloseTo(0.25, 5);
  });

  it("projects the full period from the burn rate so far", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(10000, 90000)),
      dayjs("2026-08-10T00:00:00Z")
    );
    expect(progress.projectedRatio).toBeCloseTo((10000 / 10) * 31 / 100000, 5);
  });

  it("is on-track when spending and projection are both comfortable", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(10000, 90000)),
      dayjs("2026-08-10T00:00:00Z")
    );
    expect(progress.state).toBe("on-track");
  });

  it("is projected-over when the burn rate overshoots but nothing has been breached yet", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(20000, 80000)),
      dayjs("2026-08-05T00:00:00Z")
    );
    expect(progress.projectedRatio).toBeGreaterThan(1);
    expect(progress.state).toBe("projected-over");
  });

  it("is threshold-passed once spending crosses the alert threshold", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(80000, 20000)),
      dayjs("2026-08-30T00:00:00Z")
    );
    expect(progress.state).toBe("threshold-passed");
  });

  it("is exceeded when the API says it is exceeded, whatever the projection", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(120000, -20000, true)),
      dayjs("2026-08-20T00:00:00Z")
    );
    expect(progress.state).toBe("exceeded");
  });

  it("treats a null alert threshold as no threshold rather than zero", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(80000, 20000), 100000, null),
      dayjs("2026-08-30T00:00:00Z")
    );
    expect(progress.state).not.toBe("threshold-passed");
  });

  it("flags uncounted spending so the UI can never hide it", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(1000, 99000, false, [euros(500)] as never)),
      dayjs("2026-08-10T00:00:00Z")
    );
    expect(progress.hasUncounted).toBe(true);
  });

  it("clamps the elapsed count to the period length after it ends", () => {
    const progress = budgetProgress(
      budgetWith(monthPeriod(50000, 50000)),
      dayjs("2026-10-15T00:00:00Z")
    );
    expect(progress.daysElapsed).toBe(31);
    expect(progress.daysRemaining).toBe(0);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/budgets/budgetProgress.test.ts`
Expected: FAIL — cannot resolve `./budgetProgress`.

- [ ] **Step 3: Write the implementation**

`web/src/budgets/budgetProgress.ts`:

```ts
import dayjs, { Dayjs } from "dayjs";
import { IBudgetResponse } from "../clients/types";
import { toMajorUnits } from "../money/formatMoney";

export type BudgetState =
  | "not-measuring"
  | "on-track"
  | "projected-over"
  | "threshold-passed"
  | "exceeded";

export interface BudgetProgress {
  state: BudgetState;
  spentRatio: number;
  projectedRatio: number;
  daysElapsed: number;
  daysTotal: number;
  daysRemaining: number;
  hasUncounted: boolean;
}

const notMeasuring: BudgetProgress = {
  state: "not-measuring",
  spentRatio: 0,
  projectedRatio: 0,
  daysElapsed: 0,
  daysTotal: 0,
  daysRemaining: 0,
  hasUncounted: false,
};

export const budgetProgress = (budget: IBudgetResponse, now: Dayjs): BudgetProgress => {
  const period = budget.period;
  if (period === null) return notMeasuring;

  const from = dayjs(period.from);
  const toExclusive = dayjs(period.toExclusive);
  const daysTotal = toExclusive.diff(from, "day");
  const daysElapsedRaw = now.diff(from, "day") + 1;
  const daysElapsed = Math.min(Math.max(daysElapsedRaw, 1), daysTotal);
  const daysRemaining = Math.max(daysTotal - daysElapsed, 0);

  const limit = toMajorUnits(budget.amount);
  const spent = toMajorUnits(period.spent);
  const spentRatio = limit === 0 ? 0 : spent / limit;
  const projectedRatio = limit === 0 ? 0 : ((spent / daysElapsed) * daysTotal) / limit;

  const thresholdRatio =
    budget.alertThresholdPercent === null ? null : budget.alertThresholdPercent / 100;

  const state: BudgetState = period.exceeded
    ? "exceeded"
    : thresholdRatio !== null && spentRatio >= thresholdRatio
      ? "threshold-passed"
      : projectedRatio > 1
        ? "projected-over"
        : "on-track";

  return {
    state,
    spentRatio,
    projectedRatio,
    daysElapsed,
    daysTotal,
    daysRemaining,
    hasUncounted: period.uncounted.length > 0,
  };
};
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS. If the `projected-over` and `threshold-passed` cases fight each other, the precedence order in the ternary chain is the thing to reason about — `exceeded` beats everything, then a crossed threshold, then a projection.

- [ ] **Step 5: Commit**

```bash
git add web/src/budgets/
git commit -m "feat: compute budget burn rate and state as pure logic"
```

---

### Task 4: StatTile and account balances

**Files:**
- Create: `web/src/components/StatTile/StatTile.tsx`
- Create: `web/src/components/AccountBalances/AccountBalances.tsx`
- Create: `web/src/components/AccountBalances/AccountBalances.test.tsx`

**Interfaces:**
- Consumes: `formatMoney`, `IAccountResponse`, `tokens`, `DeltaChip`.
- Produces: `StatTile({ label, value, hint, children })`; `AccountBalances({ accounts })`.

- [ ] **Step 1: Write the failing test**

`web/src/components/AccountBalances/AccountBalances.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import { Currency } from "../../typings/enums/Currency";
import { IAccountResponse } from "../../clients/types";
import AccountBalances from "./AccountBalances";

const account = (
  label: string,
  minorUnits: number,
  currency: Currency,
  isDefault = false
): IAccountResponse => ({
  accountNumber: label.toUpperCase(),
  label,
  balance: { minorUnits, currency },
  isDefault,
  createdAt: "",
  updatedAt: null,
});

const renderBalances = (accounts: IAccountResponse[]) =>
  render(
    <ThemeProvider theme={theme}>
      <AccountBalances accounts={accounts} />
    </ThemeProvider>
  );

describe("AccountBalances", () => {
  it("shows one tile per account", () => {
    renderBalances([account("ING", 240000, Currency.EUR), account("Revolut", 5000, Currency.USD)]);
    expect(screen.getByText("ING")).toBeDefined();
    expect(screen.getByText("Revolut")).toBeDefined();
  });

  it("formats each balance in its own currency", () => {
    renderBalances([account("ING", 240000, Currency.EUR)]);
    expect(screen.getByText(/2[.,]400[.,]00/)).toBeDefined();
  });

  it("never shows a combined total across currencies", () => {
    renderBalances([account("ING", 100000, Currency.EUR), account("Revolut", 100000, Currency.USD)]);
    expect(screen.queryByText(/2[.,]000[.,]00/)).toBeNull();
  });

  it("marks the default account", () => {
    renderBalances([account("ING", 1000, Currency.EUR, true)]);
    expect(screen.getByText("Default")).toBeDefined();
  });

  it("says so when there are no accounts", () => {
    renderBalances([]);
    expect(screen.getByText(/no accounts/i)).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/AccountBalances/AccountBalances.test.tsx`
Expected: FAIL — cannot resolve `./AccountBalances`.

- [ ] **Step 3: Write StatTile**

`web/src/components/StatTile/StatTile.tsx`:

```tsx
import { ReactNode } from "react";
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";

interface StatTileProps {
  label: string;
  value: string;
  hint?: ReactNode;
  badge?: ReactNode;
}

const StatTile = ({ label, value, hint, badge }: StatTileProps) => (
  <Paper elevation={1} sx={{ padding: 2, display: "flex", flexDirection: "column", gap: 0.5 }}>
    <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
      <Typography variant="overline" color="text.secondary">
        {label}
      </Typography>
      {badge}
    </Box>
    <Typography variant="heroNumber">{value}</Typography>
    {hint && (
      <Typography variant="body2" color="text.secondary">
        {hint}
      </Typography>
    )}
  </Paper>
);

export default StatTile;
```

- [ ] **Step 4: Write AccountBalances**

`web/src/components/AccountBalances/AccountBalances.tsx`:

```tsx
import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import Typography from "@mui/material/Typography";
import { IAccountResponse } from "../../clients/types";
import { formatMoney } from "../../money/formatMoney";
import StatTile from "../StatTile/StatTile";

interface AccountBalancesProps {
  accounts: IAccountResponse[];
}

const AccountBalances = ({ accounts }: AccountBalancesProps) => {
  if (accounts.length === 0)
    return (
      <Typography variant="body2" color="text.secondary">
        No accounts yet.
      </Typography>
    );

  return (
    <Box
      sx={{
        display: "grid",
        gap: 2,
        gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", lg: "repeat(4, 1fr)" },
      }}
    >
      {accounts.map((account) => (
        <StatTile
          key={account.accountNumber}
          label={account.label}
          value={formatMoney(account.balance)}
          hint={account.balance.currency}
          badge={account.isDefault ? <Chip size="small" label="Default" /> : undefined}
        />
      ))}
    </Box>
  );
};

export default AccountBalances;
```

- [ ] **Step 5: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/src/components/StatTile/ web/src/components/AccountBalances/
git commit -m "feat: show account balances as one tile per account per currency"
```

---

### Task 5: BudgetMeter

**Files:**
- Create: `web/src/components/BudgetMeter/BudgetMeter.tsx`
- Create: `web/src/components/BudgetMeter/BudgetMeter.test.tsx`

**Interfaces:**
- Consumes: `budgetProgress`, `formatMoney`, `tokens`, `DeltaChip`, `IBudgetResponse`.
- Produces: `BudgetMeter({ budget, now })`.

- [ ] **Step 1: Write the failing test**

`web/src/components/BudgetMeter/BudgetMeter.test.tsx`:

```tsx
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import dayjs from "dayjs";
import theme from "../../theme/theme";
import { Currency } from "../../typings/enums/Currency";
import { IBudgetResponse } from "../../clients/types";
import BudgetMeter from "./BudgetMeter";

const euros = (minorUnits: number) => ({ minorUnits, currency: Currency.EUR });

const budget = (
  spent: number,
  remaining: number,
  exceeded = false,
  uncounted: { minorUnits: number; currency: Currency }[] = [],
  period: unknown = undefined
): IBudgetResponse => ({
  id: 1,
  category: {
    id: 1,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 1, createdAt: "", updatedAt: null },
    createdAt: "",
    updatedAt: null,
  },
  amount: euros(100000),
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
  period:
    period === undefined
      ? {
          name: "2026-08",
          from: "2026-08-01T00:00:00Z",
          toExclusive: "2026-09-01T00:00:00Z",
          spent: euros(spent),
          remaining: euros(remaining),
          exceeded,
          uncounted,
        }
      : (period as IBudgetResponse["period"]),
  createdAt: "",
  updatedAt: null,
});

const renderMeter = (item: IBudgetResponse, now = dayjs("2026-08-10T00:00:00Z")) =>
  render(
    <ThemeProvider theme={theme}>
      <BudgetMeter budget={item} now={now} />
    </ThemeProvider>
  );

describe("BudgetMeter", () => {
  it("names the category", () => {
    renderMeter(budget(10000, 90000));
    expect(screen.getByText("Groceries")).toBeDefined();
  });

  it("shows spent and remaining", () => {
    renderMeter(budget(25000, 75000));
    expect(screen.getByText(/250[.,]00/)).toBeDefined();
    expect(screen.getByText(/750[.,]00/)).toBeDefined();
  });

  it("shows how many days are left", () => {
    renderMeter(budget(10000, 90000));
    expect(screen.getByText(/21 days left/i)).toBeDefined();
  });

  it("exposes progress to assistive technology, not just as a colour", () => {
    renderMeter(budget(25000, 75000));
    const meter = screen.getByRole("progressbar");
    expect(meter.getAttribute("aria-valuenow")).toBe("25");
  });

  it("says the budget is not measuring when it has no period", () => {
    renderMeter(budget(0, 0, false, [], null));
    expect(screen.getByText(/not measuring/i)).toBeDefined();
  });

  it("never hides uncounted spending in another currency", () => {
    renderMeter(budget(10000, 90000, false, [{ minorUnits: 500, currency: Currency.USD }]));
    expect(screen.getByText(/uncounted/i)).toBeDefined();
    expect(screen.getByText(/\$5[.,]00/)).toBeDefined();
  });

  it("warns when the burn rate projects an overspend", () => {
    renderMeter(budget(20000, 80000), dayjs("2026-08-05T00:00:00Z"));
    expect(screen.getByText(/projected/i)).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/BudgetMeter/BudgetMeter.test.tsx`
Expected: FAIL — cannot resolve `./BudgetMeter`.

- [ ] **Step 3: Write the component**

`web/src/components/BudgetMeter/BudgetMeter.tsx`:

```tsx
import { Dayjs } from "dayjs";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import { IBudgetResponse } from "../../clients/types";
import { budgetProgress, BudgetState } from "../../budgets/budgetProgress";
import { formatMoney } from "../../money/formatMoney";
import { tokens } from "../../theme/tokens";
import DeltaChip from "../DeltaChip/DeltaChip";

interface BudgetMeterProps {
  budget: IBudgetResponse;
  now: Dayjs;
}

const barColour: Record<BudgetState, string> = {
  "not-measuring": tokens.ink.light.muted,
  "on-track": tokens.brand[500],
  "projected-over": tokens.money.light.expenseOrdinary,
  "threshold-passed": tokens.money.light.expenseOrdinary,
  exceeded: tokens.money.light.expenseAlert,
};

const BudgetMeter = ({ budget, now }: BudgetMeterProps) => {
  const progress = budgetProgress(budget, now);

  return (
    <Paper elevation={1} sx={{ padding: 2, display: "flex", flexDirection: "column", gap: 1 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 1 }}>
        <Typography variant="h3">{budget.category.label}</Typography>
        {progress.state === "exceeded" && (
          <DeltaChip direction="up" tone="overBudget">
            Over budget
          </DeltaChip>
        )}
        {progress.state === "projected-over" && (
          <DeltaChip direction="up" tone="overBudget">
            Projected over
          </DeltaChip>
        )}
      </Box>

      {progress.state === "not-measuring" ? (
        <Typography variant="body2" color="text.secondary">
          Not measuring right now.
        </Typography>
      ) : (
        <>
          <LinearProgress
            variant="determinate"
            value={Math.min(progress.spentRatio * 100, 100)}
            aria-valuenow={Math.round(progress.spentRatio * 100)}
            sx={{
              height: 8,
              borderRadius: `${tokens.radius.pill}px`,
              "& .MuiLinearProgress-bar": {
                backgroundColor: barColour[progress.state],
                borderRadius: `${tokens.radius.pill}px`,
              },
            }}
          />
          <Typography variant="body2" color="text.secondary">
            {formatMoney(budget.period!.spent)} spent of {formatMoney(budget.amount)} ·{" "}
            {formatMoney(budget.period!.remaining)} left · {progress.daysRemaining} days left
          </Typography>
          {progress.hasUncounted && (
            <Typography variant="body2" sx={{ color: tokens.money.light.expenseOrdinary }}>
              Uncounted in another currency:{" "}
              {budget.period!.uncounted.map((amount) => formatMoney(amount)).join(" · ")}
            </Typography>
          )}
        </>
      )}
    </Paper>
  );
};

export default BudgetMeter;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/components/BudgetMeter/
git commit -m "feat: add a budget meter that never hides uncounted spending"
```

---

### Task 6: NotificationBell

**Files:**
- Create: `web/src/components/NotificationBell/NotificationBell.tsx`
- Create: `web/src/components/NotificationBell/NotificationBell.test.tsx`

**Interfaces:**
- Consumes: `listNotifications`, `getUnreadCount`, `markNotificationRead`, `markAllNotificationsRead` from `clients/notifications.ts`; `INotificationResponse`.
- Produces: `NotificationBell({ notifications, unreadCount, onMarkRead, onMarkAllRead })` — a presentational component. Data fetching lives in the page, so the component stays testable without mocking axios.

- [ ] **Step 1: Write the failing test**

`web/src/components/NotificationBell/NotificationBell.test.tsx`:

```tsx
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import { INotificationResponse } from "../../clients/types";
import NotificationBell from "./NotificationBell";

const notification = (id: number, readAt: string | null): INotificationResponse => ({
  id,
  kind: "BudgetExceeded",
  title: `Budget exceeded ${id}`,
  message: "Groceries is over budget",
  payload: null,
  readAt,
  createdAt: "2026-08-07T10:00:00Z",
});

const renderBell = (
  notifications: INotificationResponse[],
  unreadCount: number,
  onMarkRead = vi.fn(),
  onMarkAllRead = vi.fn()
) =>
  render(
    <ThemeProvider theme={theme}>
      <NotificationBell
        notifications={notifications}
        unreadCount={unreadCount}
        onMarkRead={onMarkRead}
        onMarkAllRead={onMarkAllRead}
      />
    </ThemeProvider>
  );

describe("NotificationBell", () => {
  it("labels the bell with the unread count so it is not colour alone", () => {
    renderBell([notification(1, null)], 1);
    expect(screen.getByRole("button", { name: /1 unread notification/i })).toBeDefined();
  });

  it("uses the plural form for more than one", () => {
    renderBell([notification(1, null), notification(2, null)], 2);
    expect(screen.getByLabelText(/2 unread notifications/i)).toBeDefined();
  });

  it("says there is nothing unread when the count is zero", () => {
    renderBell([], 0);
    expect(screen.getByLabelText(/no unread notifications/i)).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/NotificationBell/NotificationBell.test.tsx`
Expected: FAIL — cannot resolve `./NotificationBell`.

- [ ] **Step 3: Write the component**

`web/src/components/NotificationBell/NotificationBell.tsx`:

```tsx
import { useState } from "react";
import Badge from "@mui/material/Badge";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import IconButton from "@mui/material/IconButton";
import Popover from "@mui/material/Popover";
import Typography from "@mui/material/Typography";
import { Bell } from "lucide-react";
import { INotificationResponse } from "../../clients/types";

interface NotificationBellProps {
  notifications: INotificationResponse[];
  unreadCount: number;
  onMarkRead: (id: number) => void;
  onMarkAllRead: () => void;
}

const describeCount = (unreadCount: number): string =>
  unreadCount === 0
    ? "No unread notifications"
    : unreadCount === 1
      ? "1 unread notification"
      : `${unreadCount} unread notifications`;

const NotificationBell = ({
  notifications,
  unreadCount,
  onMarkRead,
  onMarkAllRead,
}: NotificationBellProps) => {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  return (
    <>
      <IconButton
        aria-label={describeCount(unreadCount)}
        onClick={(event) => setAnchor(event.currentTarget)}
      >
        <Badge badgeContent={unreadCount} color="error" overlap="circular">
          <Bell size={20} />
        </Badge>
      </IconButton>

      <Popover
        open={anchor !== null}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box sx={{ width: 340, padding: 1.5, display: "flex", flexDirection: "column", gap: 1 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Typography variant="overline">Notifications</Typography>
            {unreadCount > 0 && (
              <Button size="small" onClick={onMarkAllRead}>
                Mark all read
              </Button>
            )}
          </Box>

          {notifications.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Nothing yet.
            </Typography>
          )}

          {notifications.map((item) => (
            <Box
              key={item.id}
              onClick={() => item.readAt === null && onMarkRead(item.id)}
              sx={{
                padding: 1,
                borderRadius: 1.5,
                cursor: item.readAt === null ? "pointer" : "default",
                backgroundColor: item.readAt === null ? "action.hover" : "transparent",
              }}
            >
              <Typography variant="body1" fontWeight={item.readAt === null ? 700 : 400}>
                {item.title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {item.message}
              </Typography>
            </Box>
          ))}
        </Box>
      </Popover>
    </>
  );
};

export default NotificationBell;
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/components/NotificationBell/
git commit -m "feat: add a notification bell whose unread count is announced, not just coloured"
```

---

### Task 7: The Overview page

**Files:**
- Create: `web/src/pages/Overview/Overview.tsx`
- Modify: `web/src/App.tsx`
- Delete: `web/src/pages/Dashboard/Dashboard.tsx`
- Delete: `web/src/components/Charts/XpensePie/XpensePieChart.tsx`

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Produces: the `/` route rendering `Overview`.

- [ ] **Step 1: Write the page**

`web/src/pages/Overview/Overview.tsx`:

```tsx
import { useEffect, useState } from "react";
import dayjs from "dayjs";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import Page from "../../components/Page/Page";
import AccountBalances from "../../components/AccountBalances/AccountBalances";
import BudgetMeter from "../../components/BudgetMeter/BudgetMeter";
import { listAccounts } from "../../clients/options";
import { listBudgets } from "../../clients/budgets";
import { IAccountResponse, IBudgetResponse } from "../../clients/types";

const Overview = () => {
  const [accounts, setAccounts] = useState<IAccountResponse[]>([]);
  const [budgets, setBudgets] = useState<IBudgetResponse[]>([]);
  const now = dayjs();

  useEffect(() => {
    listAccounts().then(setAccounts);
    listBudgets(now).then(setBudgets);
  }, []);

  return (
    <Page title="Overview" headerColor="primary.dark" headerBackgroundColor="white">
      <Box sx={{ display: "flex", flexDirection: "column", gap: 3, width: "100%" }}>
        <Box>
          <Typography variant="h2" sx={{ marginBottom: 1.5 }}>
            Balances
          </Typography>
          <AccountBalances accounts={accounts} />
        </Box>

        <Box>
          <Typography variant="h2" sx={{ marginBottom: 1.5 }}>
            Budgets
          </Typography>
          {budgets.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No budgets yet.
            </Typography>
          ) : (
            <Box
              sx={{
                display: "grid",
                gap: 2,
                gridTemplateColumns: { xs: "1fr", md: "repeat(2, 1fr)" },
              }}
            >
              {budgets.map((budget) => (
                <BudgetMeter key={budget.id} budget={budget} now={now} />
              ))}
            </Box>
          )}
        </Box>
      </Box>
    </Page>
  );
};

export default Overview;
```

- [ ] **Step 2: Point the route at it**

In `web/src/App.tsx`, replace the `Dashboard` import with `Overview` from `./pages/Overview/Overview.tsx` and change `<Route index element={<Dashboard />} />` to `<Route index element={<Overview />} />`.

- [ ] **Step 3: Delete what it replaces**

```bash
cd web && rm -rf src/pages/Dashboard src/components/Charts/XpensePie
```

Then check nothing still imports them:

```bash
grep -rn "Dashboard\|XpensePie" src || echo "clean"
```

Expected: `clean`. If `XpenseLineCharts` is now unreferenced too, leave it — the Insights page uses line charts.

- [ ] **Step 4: Verify**

Run: `cd web && npm test && npm run build && npm run check:init`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add -A web/src
git commit -m "feat: replace the fake-data dashboard with a real overview page"
```

---

### Task 8: Consolidate navigation and mount the bell

Today there are three navigation components for three pages: `NavigationBar`, `SideBar` and `UtilitiesBar`. The spec collapses these to one.

**Files:**
- Modify: `web/src/components/NavigationBar/NavigationBar.tsx`
- Modify: `web/src/pages/Layout.tsx`

**Interfaces:**
- Consumes: `NotificationBell`, the notification clients.
- Produces: a single navigation surface with the five destinations from the spec and the bell in the top bar.

- [ ] **Step 1: Read what exists before changing it**

Read `web/src/pages/Layout.tsx`, `NavigationBar.tsx`, `SideBar.tsx`, `UtilitiesBar.tsx` and `MenuLinksDrawer.tsx` in full. `Layout` currently branches on a `md` breakpoint and swaps `UtilitiesBar` for `SideBar`. Understand that branch before replacing it.

- [ ] **Step 2: Put the bell in the top bar with its data**

In `NavigationBar.tsx`, fetch the unread count and the first page of notifications, and render `NotificationBell`. Wire `onMarkRead` to `markNotificationRead` followed by a refetch, and `onMarkAllRead` to `markAllNotificationsRead` followed by a refetch.

- [ ] **Step 3: Reduce the navigation to one surface**

Destinations, per the spec: Overview `/`, Transactions `/transactions`, Insights `/insights`, Budgets `/budgets`, Manage `/settings`. Insights and Budgets have no page yet — link them and let them render nothing rather than inventing placeholder content, or omit them until their plans land. Prefer omitting: a link to an empty page is worse than no link.

So this step ships **Overview, Transactions and Manage** only.

- [ ] **Step 4: Verify by hand as well as by test**

Run: `cd web && npm run dev` and confirm the bell renders, the nav has no duplicate surfaces, and the theme toggle still works on Manage. Then run `npm test && npm run build && npm run check:init`.

- [ ] **Step 5: Commit**

```bash
git add -A web/src
git commit -m "refactor: collapse three navigation bars into one and mount the notification bell"
```

---

## Self-review

**Spec coverage.** Q6 balances in Task 4. Q1 and Q7 budget meters with burn rate in Tasks 3 and 5. The notification bell in Tasks 6 and 8. The five-destination navigation in Task 8, reduced honestly to the three destinations that exist. `Uncounted` visibility is asserted in both Task 3 and Task 5. The never-sum-across-currencies rule is asserted directly in Task 4.

**Deliberately not here.** Q2, Q3, Q5 and Q8 need endpoints that do not exist and belong to a later plan. Budget create, edit and delete belong to the Budgets page plan. Transactions filtering is its own plan. The `compact` density token is unused until the transactions grid exists.

**Type consistency.** `IBudgetResponse.period` is nullable everywhere and every consumer handles null. `budgetProgress(budget, now)` keeps that signature in Tasks 3 and 5. `formatMoney(money)` and `toMajorUnits(money)` keep theirs in Tasks 2, 4 and 5. `tokens` is imported from `theme/tokens`, never redefined.

**Two risks for the executor.**
1. `BudgetMeter` uses `budget.period!` after checking `progress.state === "not-measuring"`. That is sound today because `not-measuring` is returned if and only if `period === null`, but it is an invariant held in two files. If `budgetProgress` ever returns `not-measuring` for another reason, the meter throws. Narrowing on `budget.period !== null` directly would be safer.
2. `barColour` uses light-mode tokens unconditionally, so the bar keeps light colours in dark mode. The correct fix is theme CSS variables rather than raw tokens. Left as written to keep the task small — worth a follow-up before the Insights page adds more charts.
