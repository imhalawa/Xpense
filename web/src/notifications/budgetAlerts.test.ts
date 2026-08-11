import { afterEach, describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import type { BudgetView } from "../vault/VaultProjection";
import { budgetAlerts, markBudgetAlertsRead } from "./budgetAlerts";

const euros = (minorUnits: number) => ({ minorUnits, currency: Currency.EUR });

const budget = (overrides: Partial<BudgetView> = {}): BudgetView => ({
  id: "budget-1",
  category: { id: "category-1", label: "Coffee" },
  amount: euros(10000),
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
  period: {
    name: "2026-08",
    from: "2026-08-01T00:00:00Z",
    toExclusive: "2026-09-01T00:00:00Z",
    spent: euros(15750),
    remaining: euros(-5750),
    exceeded: true,
    uncounted: [],
  },
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
  canEdit: true,
  ...overrides,
});

describe("budgetAlerts", () => {
  afterEach(() => {
    localStorage.clear();
  });

  it("describes an exceeded budget the way the notification doc promises", () => {
    const [alert, ...rest] = budgetAlerts([budget()]);

    expect(rest).toHaveLength(0);
    expect(alert.kind).toBe("BudgetExceeded");
    expect(alert.title).toBe("Coffee is over budget");
    expect(alert.message).toBe(
      "You have spent €157.50 of your €100.00 budget for Coffee in 2026-08, which is €57.50 over.",
    );
    expect(alert.readAt).toBeNull();
  });

  it("ignores a budget that is within its limit and one with no period", () => {
    const within = budget({ id: "budget-2", period: { ...budget().period!, exceeded: false } });
    const dormant = budget({ id: "budget-3", period: null });

    expect(budgetAlerts([within, dormant])).toEqual([]);
  });

  it("gives every budget and period its own stable alert", () => {
    const other = budget({ id: "budget-2", category: { id: "category-2", label: "Books" } });
    const alerts = budgetAlerts([budget(), other]);
    const repeated = budgetAlerts([budget(), other]);

    expect(new Set(alerts.map((alert) => alert.id)).size).toBe(2);
    expect(repeated.map((alert) => alert.id)).toEqual(alerts.map((alert) => alert.id));
  });

  it("keeps an alert read while the same period stays over", () => {
    const [alert] = budgetAlerts([budget()]);

    markBudgetAlertsRead([alert.id]);
    const [afterReading] = budgetAlerts([budget()]);

    expect(afterReading.readAt).not.toBeNull();
    expect(budgetAlerts([budget()]).filter((item) => item.readAt === null)).toEqual([]);
  });

  it("does not carry a read mark into the next period", () => {
    const [alert] = budgetAlerts([budget()]);
    markBudgetAlertsRead([alert.id]);

    const next = budget({ period: { ...budget().period!, name: "2026-09" } });

    expect(budgetAlerts([next])[0].readAt).toBeNull();
  });
});
