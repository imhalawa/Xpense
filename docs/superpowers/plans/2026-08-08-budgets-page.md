# Budgets Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Budgets page where budgets are created, edited, deleted and tracked — using **only endpoints that already exist**.

**Architecture:** Validation is a pure function mirroring the API's FluentValidation rules exactly, so the form catches every error before the request rather than rendering a 400. The list reuses the existing `BudgetMeter`. Delete is confirmed, never immediate.

**Tech Stack:** React 19, MUI 9.3.1, react-hook-form, axios, dayjs, Vitest 4.

## Global Constraints

- Source spec: `docs/superpowers/specs/2026-08-07-information-architecture-design.md`, the Budgets page.
- **No comments in any file.** Project-wide rule.
- **Explicit names.** No `ct`, `db`, `cfg`, `req`, `val`.
- **No barrel value imports.** Import `Currency` from `"../typings/enums/Currency"`, never `"../typings"`. `npm run check:init` must pass.
- **MUI 9 dropped Typography and Box system props.** `<Typography fontWeight={700}>` does not compile — use `sx={{ fontWeight: 700 }}`.
- **Never sum across currencies.** A budget's amount and its spend are one currency; there is no total across budgets.
- **Money crosses the wire as `{ minorUnits, currency }`.** Convert with the existing `toMajorUnits` / `formatMoney`; the form edits major units and converts on submit.
- Dates cross the wire as `yyyy-MM-dd` (the API takes `DateOnly`), never as ISO timestamps. `BudgetResponse.startsOn` and `endsOn` are already in that form.

## API surface used — all pre-existing, zero API changes

| Endpoint | Shape |
|---|---|
| `GET /api/v1/budgets?on=<iso>` | `BudgetResponse[]` |
| `POST /api/v1/budgets` | `{ categoryId, amount {minorUnits, currency}, recurrence, startsOn, endsOn, alertThresholdPercent }` |
| `PUT /api/v1/budgets/{id}` | same minus `categoryId` — the category cannot be changed after creation |
| `DELETE /api/v1/budgets/{id}` | — |
| `GET /api/v1/categories` | `ICategoryResponse[]`, already wired as `listCategories` |

## The API's validation rules, to be mirrored exactly

Read from `CreateBudget.Validator`:

| Rule | Message |
|---|---|
| `categoryId > 0` | The category must be a valid selection. |
| amount `minorUnits > 0` | The amount in minor units must be positive. |
| currency parses | The currency must be a supported currency name. |
| recurrence in None, Weekly, Monthly, Yearly | The recurrence must be one of None, Weekly, Monthly or Yearly. |
| `endsOn` required when recurrence is None | A budget that does not repeat must state when it ends. |
| `endsOn >= startsOn` when present | The end date cannot be before the start date. |
| `alertThresholdPercent` between 1 and 100 when present | The alert threshold must be between 1 and 100 percent. |

## File Structure

| File | Responsibility |
|---|---|
| `web/src/clients/types.ts` | Modify: add budget request interfaces |
| `web/src/clients/budgets.ts` | Modify: add create, update, delete |
| `web/src/budgets/budgetFormRules.ts` | Pure validation mirroring the API |
| `web/src/budgets/budgetFormRules.test.ts` | Its tests |
| `web/src/components/BudgetForm/BudgetForm.tsx` | Create and edit form |
| `web/src/components/BudgetForm/BudgetForm.test.tsx` | Its tests |
| `web/src/pages/Budgets/Budgets.tsx` | The page |
| `web/src/App.tsx` | Modify: add the `/budgets` route |
| `web/src/components/NavigationBar/NavigationBar.tsx` | Modify: add the Budgets destination |

---

### Task 1: Budget write clients

**Files:**
- Modify: `web/src/clients/types.ts`
- Modify: `web/src/clients/budgets.ts`

**Interfaces:**
- Produces: `ICreateBudgetRequest`, `IUpdateBudgetRequest`, and `createBudget`, `updateBudget`, `deleteBudget`.

- [ ] **Step 1: Add the request interfaces**

Append to `web/src/clients/types.ts`:

```ts
export type Recurrence = "None" | "Weekly" | "Monthly" | "Yearly";

export interface IBudgetAmountRequest {
  minorUnits: number;
  currency: Currency;
}

export interface ICreateBudgetRequest {
  categoryId: number;
  amount: IBudgetAmountRequest;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
}

export type IUpdateBudgetRequest = Omit<ICreateBudgetRequest, "categoryId">;
```

- [ ] **Step 2: Add the client functions**

Append to `web/src/clients/budgets.ts`:

```ts
export const createBudget = async (request: ICreateBudgetRequest): Promise<IBudgetResponse> => {
  const response = await axios.post<IBudgetResponse>("/api/v1/budgets", request);
  return response.data;
};

export const updateBudget = async (
  id: number,
  request: IUpdateBudgetRequest
): Promise<IBudgetResponse> => {
  const response = await axios.put<IBudgetResponse>(`/api/v1/budgets/${id}`, request);
  return response.data;
};

export const deleteBudget = async (id: number): Promise<void> => {
  await axios.delete(`/api/v1/budgets/${id}`);
};
```

Extend the existing import from `./types` to include `ICreateBudgetRequest` and `IUpdateBudgetRequest`.

- [ ] **Step 3: Verify**

Run: `cd web && npm run build && npm run check:init`
Expected: both exit 0.

- [ ] **Step 4: Commit**

```bash
git add web/src/clients/
git commit -m "feat: add budget create update and delete clients"
```

---

### Task 2: budgetFormRules

The important task. Every rule below exists in the API; a mismatch here means the user gets a 400 instead of a field error.

**Files:**
- Create: `web/src/budgets/budgetFormRules.ts`
- Create: `web/src/budgets/budgetFormRules.test.ts`

**Interfaces:**
- Produces:
```ts
interface BudgetFormValues {
  categoryId: number | null;
  amountMajorUnits: string;
  currency: Currency;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
}
type BudgetFormErrors = Partial<Record<keyof BudgetFormValues, string>>;
validateBudgetForm(values: BudgetFormValues): BudgetFormErrors
toCreateRequest(values: BudgetFormValues): ICreateBudgetRequest
```

- [ ] **Step 1: Write the failing test**

`web/src/budgets/budgetFormRules.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { BudgetFormValues, toCreateRequest, validateBudgetForm } from "./budgetFormRules";

const valid: BudgetFormValues = {
  categoryId: 1,
  amountMajorUnits: "250.00",
  currency: Currency.EUR,
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
};

describe("validateBudgetForm", () => {
  it("accepts a valid monthly budget", () => {
    expect(validateBudgetForm(valid)).toEqual({});
  });

  it("requires a category", () => {
    expect(validateBudgetForm({ ...valid, categoryId: null }).categoryId).toMatch(/category/i);
  });

  it("rejects a zero amount", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "0" }).amountMajorUnits).toMatch(
      /positive/i
    );
  });

  it("rejects a negative amount", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "-5" }).amountMajorUnits).toMatch(
      /positive/i
    );
  });

  it("rejects an amount that is not a number", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "abc" }).amountMajorUnits).toBeDefined();
  });

  it("requires an end date when the budget does not repeat", () => {
    const errors = validateBudgetForm({ ...valid, recurrence: "None", endsOn: null });
    expect(errors.endsOn).toMatch(/does not repeat/i);
  });

  it("accepts a one-off budget that states its end", () => {
    const errors = validateBudgetForm({
      ...valid,
      recurrence: "None",
      endsOn: "2026-08-31",
    });
    expect(errors.endsOn).toBeUndefined();
  });

  it("rejects an end date before the start date", () => {
    const errors = validateBudgetForm({ ...valid, endsOn: "2026-07-01" });
    expect(errors.endsOn).toMatch(/before the start/i);
  });

  it("accepts an end date equal to the start date", () => {
    const errors = validateBudgetForm({ ...valid, endsOn: "2026-08-01" });
    expect(errors.endsOn).toBeUndefined();
  });

  it("rejects a threshold below one", () => {
    expect(validateBudgetForm({ ...valid, alertThresholdPercent: 0 }).alertThresholdPercent)
      .toMatch(/between 1 and 100/i);
  });

  it("rejects a threshold above one hundred", () => {
    expect(validateBudgetForm({ ...valid, alertThresholdPercent: 101 }).alertThresholdPercent)
      .toMatch(/between 1 and 100/i);
  });

  it("accepts no threshold at all", () => {
    expect(validateBudgetForm({ ...valid, alertThresholdPercent: null }).alertThresholdPercent)
      .toBeUndefined();
  });
});

describe("toCreateRequest", () => {
  it("converts major units to minor units", () => {
    expect(toCreateRequest(valid).amount).toEqual({ minorUnits: 25000, currency: Currency.EUR });
  });

  it("rounds rather than truncating a fractional minor unit", () => {
    const request = toCreateRequest({ ...valid, amountMajorUnits: "10.005" });
    expect(request.amount.minorUnits).toBe(1001);
  });

  it("keeps dates as plain day strings", () => {
    const request = toCreateRequest({ ...valid, endsOn: "2026-12-31" });
    expect(request.startsOn).toBe("2026-08-01");
    expect(request.endsOn).toBe("2026-12-31");
  });

  it("passes the category through", () => {
    expect(toCreateRequest(valid).categoryId).toBe(1);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/budgets/budgetFormRules.test.ts`
Expected: FAIL — cannot resolve `./budgetFormRules`.

- [ ] **Step 3: Write the implementation**

`web/src/budgets/budgetFormRules.ts`:

```ts
import { Currency } from "../typings/enums/Currency";
import { ICreateBudgetRequest, Recurrence } from "../clients/types";

export interface BudgetFormValues {
  categoryId: number | null;
  amountMajorUnits: string;
  currency: Currency;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
}

export type BudgetFormErrors = Partial<Record<keyof BudgetFormValues, string>>;

const minorUnitsPerMajor = 100;

const parseAmount = (amountMajorUnits: string): number => Number(amountMajorUnits.trim());

export const validateBudgetForm = (values: BudgetFormValues): BudgetFormErrors => {
  const errors: BudgetFormErrors = {};

  if (values.categoryId === null || values.categoryId <= 0)
    errors.categoryId = "The category must be a valid selection.";

  const amount = parseAmount(values.amountMajorUnits);
  if (values.amountMajorUnits.trim() === "" || Number.isNaN(amount))
    errors.amountMajorUnits = "The amount must be a number.";
  else if (amount <= 0) errors.amountMajorUnits = "The amount must be positive.";

  if (values.recurrence === "None" && values.endsOn === null)
    errors.endsOn = "A budget that does not repeat must state when it ends.";
  else if (values.endsOn !== null && values.endsOn < values.startsOn)
    errors.endsOn = "The end date cannot be before the start date.";

  if (
    values.alertThresholdPercent !== null &&
    (values.alertThresholdPercent < 1 || values.alertThresholdPercent > 100)
  )
    errors.alertThresholdPercent = "The alert threshold must be between 1 and 100 percent.";

  return errors;
};

export const toCreateRequest = (values: BudgetFormValues): ICreateBudgetRequest => ({
  categoryId: values.categoryId!,
  amount: {
    minorUnits: Math.round(parseAmount(values.amountMajorUnits) * minorUnitsPerMajor),
    currency: values.currency,
  },
  recurrence: values.recurrence,
  startsOn: values.startsOn,
  endsOn: values.endsOn,
  alertThresholdPercent: values.alertThresholdPercent,
});
```

Note the date comparison uses plain string comparison, which is correct precisely because the format is `yyyy-MM-dd` — zero-padded and big-endian, so lexical order equals chronological order. If the format ever changes, this breaks silently, and the test asserting an end date before the start is what would catch it.

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/budgets/budgetFormRules.ts web/src/budgets/budgetFormRules.test.ts
git commit -m "feat: mirror the api budget validation rules in the form"
```

---

### Task 3: BudgetForm

**Files:**
- Create: `web/src/components/BudgetForm/BudgetForm.tsx`
- Create: `web/src/components/BudgetForm/BudgetForm.test.tsx`

**Interfaces:**
- Consumes: `validateBudgetForm`, `toCreateRequest`, `listCategories`, `Currency`.
- Produces: `BudgetForm({ categories, initialValues, onSubmit, onCancel, submitLabel })` — presentational, so it is testable without axios. The page owns fetching and saving.

- [ ] **Step 1: Write the failing test**

`web/src/components/BudgetForm/BudgetForm.test.tsx`:

```tsx
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import { Currency } from "../../typings/enums/Currency";
import { ICategoryResponse } from "../../clients/types";
import { BudgetFormValues } from "../../budgets/budgetFormRules";
import BudgetForm from "./BudgetForm";

const categories: ICategoryResponse[] = [
  {
    id: 1,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 1, createdAt: "", updatedAt: null },
    createdAt: "",
    updatedAt: null,
  },
];

const initialValues: BudgetFormValues = {
  categoryId: 1,
  amountMajorUnits: "250",
  currency: Currency.EUR,
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
};

const renderForm = (onSubmit = vi.fn(), values = initialValues) => {
  render(
    <ThemeProvider theme={theme}>
      <BudgetForm
        categories={categories}
        initialValues={values}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        submitLabel="Save"
      />
    </ThemeProvider>
  );
  return onSubmit;
};

describe("BudgetForm", () => {
  it("submits valid values", () => {
    const onSubmit = renderForm();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it("does not submit when the amount is zero, and says why", () => {
    const onSubmit = renderForm(vi.fn(), { ...initialValues, amountMajorUnits: "0" });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/must be positive/i)).toBeDefined();
  });

  it("does not submit a one-off budget with no end date, and says why", () => {
    const onSubmit = renderForm(vi.fn(), {
      ...initialValues,
      recurrence: "None",
      endsOn: null,
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/does not repeat/i)).toBeDefined();
  });

  it("offers every recurrence the api accepts", () => {
    renderForm();
    expect(screen.getByLabelText(/recurrence/i)).toBeDefined();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && npx vitest run src/components/BudgetForm/BudgetForm.test.tsx`
Expected: FAIL — cannot resolve `./BudgetForm`.

- [ ] **Step 3: Write the component**

Build a controlled form with local `useState` seeded from `initialValues`. On submit, run `validateBudgetForm`; if the error object has any key, set errors into state and return without calling `onSubmit`; otherwise call `onSubmit(values)`.

Fields:
- Category — MUI `TextField` with `select`, one `MenuItem` per category, disabled when `initialValues.categoryId` is set and the form is in edit mode (the API's `UpdateBudget` has no `categoryId`, so the category cannot change after creation).
- Amount — `TextField` with `inputMode="decimal"`, helper text showing the error.
- Currency — `TextField` with `select` over `Currency`.
- Recurrence — `TextField` with `select` over `None`, `Weekly`, `Monthly`, `Yearly`, labelled "Recurrence".
- Starts on and Ends on — native `<input type="date">` via `TextField type="date"` with `slotProps={{ inputLabel: { shrink: true } }}`. Use the native control rather than a picker library: it is already available, keyboard accessible, and localised by the browser.
- Alert threshold — `TextField type="number"`, allowed empty meaning null.

Every error message renders as the field's `helperText` with `error` set, so it is announced, not merely coloured.

- [ ] **Step 4: Run it to verify it passes**

Run: `cd web && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/components/BudgetForm/
git commit -m "feat: add a budget form that validates before it calls the api"
```

---

### Task 4: The Budgets page

**Files:**
- Create: `web/src/pages/Budgets/Budgets.tsx`
- Modify: `web/src/App.tsx`

**Interfaces:**
- Consumes: `listBudgets`, `createBudget`, `updateBudget`, `deleteBudget`, `listCategories`, `BudgetMeter`, `BudgetForm`.

- [ ] **Step 1: Build the page**

State: `budgets`, `categories`, `editing` (a budget or `"new"` or `null`), and `pendingDelete` (a budget or `null`).

Layout:
- A heading row with a "New budget" button.
- The list of budgets, each rendered as an existing `BudgetMeter` with Edit and Delete buttons beside it.
- A `Dialog` holding `BudgetForm` when `editing` is not null.
- A confirmation `Dialog` when `pendingDelete` is not null, naming the category and stating that the budget's history is not deleted with it. Delete is destructive and must never be one click.

After any successful create, update or delete, refetch `listBudgets(dayjs())` rather than mutating local state, so the recomputed `period` from the server is what gets displayed.

Empty state: "No budgets yet." plus the same New budget button.

- [ ] **Step 2: Add the route**

In `web/src/App.tsx`, add `<Route path="/budgets" element={<Budgets />} />` beside the existing routes.

- [ ] **Step 3: Verify**

Run: `cd web && npm test && npm run build && npm run check:init`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add web/src/pages/Budgets/ web/src/App.tsx
git commit -m "feat: add a budgets page with create edit and confirmed delete"
```

---

### Task 5: Add Budgets to navigation

**Files:**
- Modify: `web/src/components/NavigationBar/NavigationBar.tsx`

- [ ] **Step 1: Add the destination**

Add Budgets `/budgets` to the existing destination list, between Transactions and Manage. Keep the existing `aria-current="page"` treatment for the active link and the below-`sm` label hiding.

Do NOT add Insights — that page still does not exist.

- [ ] **Step 2: Verify**

Run: `cd web && npm test && npm run build && npm run check:init`

Then start the dev server and confirm `/budgets` serves without a module error.

- [ ] **Step 3: Commit**

```bash
git add web/src/components/NavigationBar/NavigationBar.tsx
git commit -m "feat: link the budgets page from the navigation bar"
```

---

## Self-review

**Spec coverage.** The Budgets page from the IA spec: create, edit, delete, and per-budget progress with alerts. Progress reuses `BudgetMeter`, which already shows spent, remaining, days left, projection and uncounted spending.

**Deliberately not here.** The Insights page and its analytics endpoints. Transactions filtering. Anything needing an API change — this plan adds none.

**Type consistency.** `BudgetFormValues` is defined once in `budgetFormRules.ts` and imported by the form and the page. `ICreateBudgetRequest` and `IUpdateBudgetRequest` live in `clients/types.ts`. `Recurrence` is a string union matching the API's enum names exactly — `None`, `Weekly`, `Monthly`, `Yearly` — and is used by both the request type and the form values.

**Three risks for the executor.**
1. `toCreateRequest` uses `values.categoryId!`. That is only safe because the page never submits without `validateBudgetForm` returning clean, and that function rejects a null category. The invariant spans two functions in one file, which is the least bad option, but it is still an assertion rather than a proof.
2. `minorUnitsPerMajor` is hardcoded at 100, matching the existing `toSingle`. It is correct for EUR and USD and wrong for any currency without two decimal places — the same latent issue the money formatter already works around. When a third currency appears, this constant and `toSingle` both need replacing with the exponent lookup that `formatMoney` already does properly.
3. The category select must be disabled in edit mode, because `UpdateBudget.Request` has no `categoryId` — changing it in the UI would silently do nothing. There is no test for this in Task 3; add one if the form's edit mode grows.
