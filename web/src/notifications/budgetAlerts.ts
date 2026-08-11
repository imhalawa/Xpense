import type { INotificationResponse } from "../clients/types";
import { formatMoney } from "../money/formatMoney";
import type { BudgetView } from "../vault/VaultProjection";

const storageKey = "xpense.notifications.readBudgetAlerts";

const alertId = (budget: BudgetView, periodName: string): number => {
  let hash = 2166136261;
  for (const character of `${budget.id}:${periodName}`) {
    hash = Math.imul(hash ^ character.charCodeAt(0), 16777619);
  }

  return hash >>> 0;
};

const readTimestamps = (): Record<string, string> => {
  try {
    const stored = window.localStorage.getItem(storageKey);
    if (stored === null) return {};

    const parsed: unknown = JSON.parse(stored);
    if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) return {};

    return parsed as Record<string, string>;
  } catch {
    return {};
  }
};

export const markBudgetAlertsRead = (ids: number[]): void => {
  const readAt = new Date().toISOString();
  const stored = { ...readTimestamps(), ...Object.fromEntries(ids.map((id) => [id, readAt])) };

  try {
    window.localStorage.setItem(storageKey, JSON.stringify(stored));
  } catch {
    return;
  }
};

export const budgetAlerts = (budgets: BudgetView[]): INotificationResponse[] => {
  const stored = readTimestamps();

  return budgets.flatMap((budget) => {
    const period = budget.period;
    if (period === null || !period.exceeded) return [];

    const id = alertId(budget, period.name);
    const label = budget.category.label;
    const over = {
      minorUnits: period.spent.minorUnits - budget.amount.minorUnits,
      currency: budget.amount.currency,
    };

    return [
      {
        id,
        kind: "BudgetExceeded",
        title: `${label} is over budget`,
        message:
          `You have spent ${formatMoney(period.spent)} of your ${formatMoney(budget.amount)} `
          + `budget for ${label} in ${period.name}, which is ${formatMoney(over)} over.`,
        payload: { budgetId: budget.id, categoryLabel: label, period: period.name },
        readAt: stored[id] ?? null,
        createdAt: period.from,
      },
    ];
  });
};
