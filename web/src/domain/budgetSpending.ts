import type { Currency } from "../typings/enums/Currency";

export type BudgetRecurrence = "None" | "Weekly" | "Monthly" | "Yearly";

export interface MoneyAmount {
  minorUnits: number;
  currency: Currency;
}

export interface BudgetSpendingInput {
  spaceId: string;
  categoryId: string;
  amount: MoneyAmount;
  recurrence: BudgetRecurrence;
  startsOn: string;
  endsOn: string | null;
}

export interface BudgetTransaction {
  kind: "income" | "expense" | "transfer";
  categoryId: string | null;
  accountId: string | null;
  amount: MoneyAmount;
  occurredAt: string;
}

export interface BudgetAccount {
  id: string;
  spaceId: string;
}

export interface BudgetPeriod {
  name: string;
  from: string;
  toExclusive: string;
}

export interface BudgetSpending {
  period: BudgetPeriod | null;
  spent: MoneyAmount;
  uncounted: MoneyAmount[];
  remaining: MoneyAmount;
  exceeded: boolean;
}

const millisecondsPerDay = 24 * 60 * 60 * 1000;

const atUtcMidnight = (calendarDate: string): Date => new Date(`${calendarDate}T00:00:00.000Z`);

const calendarDate = (date: Date): Date => atUtcMidnight(date.toISOString().slice(0, 10));

const isoWeek = (instant: Date): { year: number; week: number; monday: Date } => {
  const date = calendarDate(instant);
  const dayOfWeek = (date.getUTCDay() + 6) % 7;
  const thursday = new Date(date.getTime() + (3 - dayOfWeek) * millisecondsPerDay);
  const year = thursday.getUTCFullYear();
  const firstThursday = new Date(Date.UTC(year, 0, 4));
  const firstMonday = new Date(
    firstThursday.getTime() - ((firstThursday.getUTCDay() + 6) % 7) * millisecondsPerDay,
  );
  const monday = new Date(date.getTime() - dayOfWeek * millisecondsPerDay);
  const week = Math.round((monday.getTime() - firstMonday.getTime()) / millisecondsPerDay / 7) + 1;
  return { year, week, monday };
};

const periodOn = (budget: BudgetSpendingInput, instant: Date): BudgetPeriod => {
  if (budget.recurrence === "Monthly") {
    const from = new Date(Date.UTC(instant.getUTCFullYear(), instant.getUTCMonth(), 1));
    const toExclusive = new Date(Date.UTC(instant.getUTCFullYear(), instant.getUTCMonth() + 1, 1));
    return { name: `${from.getUTCFullYear()}-${String(from.getUTCMonth() + 1).padStart(2, "0")}`, from: from.toISOString(), toExclusive: toExclusive.toISOString() };
  }
  if (budget.recurrence === "Yearly") {
    const from = new Date(Date.UTC(instant.getUTCFullYear(), 0, 1));
    const toExclusive = new Date(Date.UTC(instant.getUTCFullYear() + 1, 0, 1));
    return { name: String(instant.getUTCFullYear()), from: from.toISOString(), toExclusive: toExclusive.toISOString() };
  }
  const week = isoWeek(instant);
  const toExclusive = new Date(week.monday.getTime() + 7 * millisecondsPerDay);
  return { name: `${String(week.year).padStart(4, "0")}-W${String(week.week).padStart(2, "0")}`, from: week.monday.toISOString(), toExclusive: toExclusive.toISOString() };
};

export const validateBudgetWindow = (
  budget: Pick<BudgetSpendingInput, "recurrence" | "startsOn" | "endsOn">,
): void => {
  if (budget.recurrence === "None" && budget.endsOn === null) {
    throw new Error("A budget that does not repeat must state when it ends.");
  }
  if (budget.endsOn !== null && atUtcMidnight(budget.endsOn) < atUtcMidnight(budget.startsOn)) {
    throw new Error("A budget cannot end before it starts.");
  }
};

export const budgetPeriodOn = (
  budget: BudgetSpendingInput,
  on: string | Date,
): BudgetPeriod | null => {
  validateBudgetWindow(budget);
  const instant = new Date(on);
  const startsOn = atUtcMidnight(budget.startsOn);
  const lifeToExclusive = budget.endsOn === null
    ? null
    : new Date(atUtcMidnight(budget.endsOn).getTime() + millisecondsPerDay);

  if (budget.recurrence === "None") {
    if (instant < startsOn || instant >= lifeToExclusive!) return null;
    return {
      name: `${budget.startsOn}..${budget.endsOn}`,
      from: startsOn.toISOString(),
      toExclusive: lifeToExclusive!.toISOString(),
    };
  }

  const period = periodOn(budget, instant);
  if (new Date(period.toExclusive) <= startsOn || (lifeToExclusive !== null && new Date(period.from) >= lifeToExclusive)) {
    return null;
  }
  return period;
};

export const budgetSpending = (
  budget: BudgetSpendingInput,
  transactions: Iterable<BudgetTransaction>,
  accounts: Iterable<BudgetAccount>,
  on: string | Date,
): BudgetSpending => {
  const period = budgetPeriodOn(budget, on);
  const spent = { minorUnits: 0, currency: budget.amount.currency };
  if (period === null) {
    return { period, spent, uncounted: [], remaining: { ...budget.amount }, exceeded: false };
  }

  const accountSpace = new Map<string, string>();
  for (const account of accounts) accountSpace.set(account.id, account.spaceId);
  const uncounted = new Map<Currency, number>();
  const from = Date.parse(period.from);
  const toExclusive = Date.parse(period.toExclusive);

  for (const transaction of transactions) {
    const occurredAt = Date.parse(transaction.occurredAt);
    if (
      transaction.kind !== "expense" ||
      transaction.categoryId !== budget.categoryId ||
      transaction.accountId === null ||
      accountSpace.get(transaction.accountId) !== budget.spaceId ||
      occurredAt < from ||
      occurredAt >= toExclusive
    ) {
      continue;
    }

    if (transaction.amount.currency === budget.amount.currency) {
      spent.minorUnits += transaction.amount.minorUnits;
    } else {
      uncounted.set(
        transaction.amount.currency,
        (uncounted.get(transaction.amount.currency) ?? 0) + transaction.amount.minorUnits,
      );
    }
  }

  const remaining = { minorUnits: budget.amount.minorUnits - spent.minorUnits, currency: budget.amount.currency };
  return {
    period,
    spent,
    uncounted: Array.from(uncounted, ([currency, minorUnits]) => ({ currency, minorUnits })),
    remaining,
    exceeded: remaining.minorUnits < 0,
  };
};
