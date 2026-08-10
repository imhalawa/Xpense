import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { budgetPeriodOn, budgetSpending, validateBudgetWindow } from "./budgetSpending";

const budget = (overrides = {}) => ({
  spaceId: "personal",
  categoryId: "food",
  amount: { minorUnits: 30000, currency: Currency.EUR },
  recurrence: "Monthly" as const,
  startsOn: "2026-08-01",
  endsOn: null,
  ...overrides,
});

describe("budgetPeriodOn", () => {
  it("measures the calendar month containing the instant with an exclusive end", () => {
    const period = budgetPeriodOn(budget(), "2026-08-06T14:00:00Z");

    expect(period).toEqual({
      name: "2026-08",
      from: "2026-08-01T00:00:00.000Z",
      toExclusive: "2026-09-01T00:00:00.000Z",
    });
    expect(budgetPeriodOn(budget(), "2026-07-31T23:59:59Z")).toBeNull();
  });

  it("uses ISO week years at the calendar-year boundary", () => {
    expect(
      budgetPeriodOn({ ...budget(), recurrence: "Weekly", startsOn: "2026-01-01" }, "2027-01-01T12:00:00Z"),
    ).toEqual({
      name: "2026-W53",
      from: "2026-12-28T00:00:00.000Z",
      toExclusive: "2027-01-04T00:00:00.000Z",
    });
  });

  it("measures a non-repeating budget only through its stated inclusive end date", () => {
    const oneOff = { ...budget(), recurrence: "None" as const, startsOn: "2026-08-12", endsOn: "2026-08-26" };

    expect(budgetPeriodOn(oneOff, "2026-08-26T23:00:00Z")?.name).toBe("2026-08-12..2026-08-26");
    expect(budgetPeriodOn(oneOff, "2026-08-27T00:00:00Z")).toBeNull();
  });
});

describe("validateBudgetWindow", () => {
  it("requires a non-repeating budget to state when it ends", () => {
    expect(() => validateBudgetWindow({ recurrence: "None", startsOn: "2026-08-01", endsOn: null })).toThrow(
      "A budget that does not repeat must state when it ends.",
    );
  });
});

describe("budgetSpending", () => {
  it("reports matching-currency spending, separate uncounted currencies, and remaining budget", () => {
    expect(
      budgetSpending(
        budget(),
        [
          { kind: "expense", categoryId: "food", accountId: "cash", amount: { minorUnits: 1250, currency: Currency.EUR }, occurredAt: "2026-08-06T14:00:00Z" },
          { kind: "expense", categoryId: "food", accountId: "cash", amount: { minorUnits: 700, currency: Currency.USD }, occurredAt: "2026-08-06T14:00:00Z" },
          { kind: "income", categoryId: "food", accountId: "cash", amount: { minorUnits: 500, currency: Currency.EUR }, occurredAt: "2026-08-06T14:00:00Z" },
        ],
        [{ id: "cash", spaceId: "personal" }],
        "2026-08-06T14:00:00Z",
      ),
    ).toEqual({
      period: {
        name: "2026-08",
        from: "2026-08-01T00:00:00.000Z",
        toExclusive: "2026-09-01T00:00:00.000Z",
      },
      spent: { minorUnits: 1250, currency: Currency.EUR },
      uncounted: [{ minorUnits: 700, currency: Currency.USD }],
      remaining: { minorUnits: 28750, currency: Currency.EUR },
      exceeded: false,
    });
  });

  it("counts a shared budget only from accounts in the same space, never a private account", () => {
    expect(
      budgetSpending(
        { ...budget(), spaceId: "family" },
        [
          { kind: "expense", categoryId: "food", accountId: "family-cash", amount: { minorUnits: 1000, currency: Currency.EUR }, occurredAt: "2026-08-06T14:00:00Z" },
          { kind: "expense", categoryId: "food", accountId: "private-cash", amount: { minorUnits: 9999, currency: Currency.EUR }, occurredAt: "2026-08-06T14:00:00Z" },
        ],
        [
          { id: "family-cash", spaceId: "family" },
          { id: "private-cash", spaceId: "personal" },
        ],
        "2026-08-06T14:00:00Z",
      ).spent,
    ).toEqual({ minorUnits: 1000, currency: Currency.EUR });
  });
});
