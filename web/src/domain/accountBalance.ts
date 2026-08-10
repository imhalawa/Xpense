import type { Currency } from "../typings/enums/Currency";

export interface AccountBalanceSource {
  id?: string;
  currency: Currency;
  openingBalanceMinorUnits?: number;
}

export interface AccountBalance {
  currency: Currency;
  minorUnits: number;
}

export interface BalanceTransaction {
  kind: "income" | "expense" | "transfer";
  accountId: string;
  counterpartyAccountId: string | null;
  minorUnits: number;
  currency: Currency;
}

export const accountBalances = (
  accounts: Iterable<AccountBalanceSource>,
  transactions: Iterable<BalanceTransaction> = [],
): AccountBalance[] => {
  const accountBalancesById = new Map<string, { currency: Currency; minorUnits: number }>();
  const balancesByCurrency = new Map<Currency, number>();

  for (const account of accounts) {
    const minorUnits = account.openingBalanceMinorUnits ?? 0;
    const current = balancesByCurrency.get(account.currency) ?? 0;
    balancesByCurrency.set(account.currency, current + minorUnits);
    if (account.id !== undefined) {
      accountBalancesById.set(account.id, { currency: account.currency, minorUnits });
    }
  }

  const apply = (accountId: string, minorUnits: number): void => {
    const account = accountBalancesById.get(accountId);
    if (account === undefined) return;
    account.minorUnits += minorUnits;
    balancesByCurrency.set(account.currency, (balancesByCurrency.get(account.currency) ?? 0) + minorUnits);
  };

  for (const transaction of transactions) {
    const account = accountBalancesById.get(transaction.accountId);
    if (account !== undefined && account.currency !== transaction.currency) {
      throw new Error("The transaction currency must match the account currency.");
    }
    if (transaction.kind === "income") apply(transaction.accountId, transaction.minorUnits);
    else if (transaction.kind === "expense") apply(transaction.accountId, -transaction.minorUnits);
    else if (transaction.counterpartyAccountId !== null) {
      const counterparty = accountBalancesById.get(transaction.counterpartyAccountId);
      if (counterparty !== undefined && counterparty.currency !== transaction.currency) {
        throw new Error("The transaction currency must match the account currency.");
      }
      apply(transaction.accountId, -transaction.minorUnits);
      apply(transaction.counterpartyAccountId, transaction.minorUnits);
    }
  }

  return Array.from(balancesByCurrency, ([currency, minorUnits]) => ({ currency, minorUnits }));
};
