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
