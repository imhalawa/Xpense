import type { MoneyAmount } from "./budgetSpending";

export interface CategorySpendingTransaction {
  kind: "income" | "expense" | "transfer";
  categoryId: string | null;
  amount: MoneyAmount;
}

export interface CategorySpending {
  categoryId: string;
  amount: MoneyAmount;
}

export const spendingByCategory = (
  transactions: Iterable<CategorySpendingTransaction>,
): CategorySpending[] => {
  const spending = new Map<string, CategorySpending>();

  for (const transaction of transactions) {
    if (transaction.kind !== "expense" || transaction.categoryId === null) continue;
    const key = `${transaction.categoryId}:${transaction.amount.currency}`;
    const current = spending.get(key);
    if (current === undefined) {
      spending.set(key, { categoryId: transaction.categoryId, amount: { ...transaction.amount } });
    } else {
      current.amount.minorUnits += transaction.amount.minorUnits;
    }
  }

  return Array.from(spending.values());
};
