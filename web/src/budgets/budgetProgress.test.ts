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
    expect(progress.projectedRatio).toBeCloseTo(((10000 / 10) * 31) / 100000, 5);
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
