import type { Currency } from "../typings/enums/Currency";

const mismatchMessage = "The transaction currency must match the account currency.";

export interface CurrencyAccount {
  id: string;
  currency: Currency;
}

export interface CurrencyTransaction {
  currency: Currency;
  accountId: string;
  counterpartyAccountId?: string | null;
}

export const validateTransactionCurrencies = (
  transaction: CurrencyTransaction,
  accounts: Iterable<CurrencyAccount>,
): void => {
  const currencies = new Map<string, Currency>();
  for (const account of accounts) currencies.set(account.id, account.currency);

  for (const accountId of [transaction.accountId, transaction.counterpartyAccountId]) {
    if (accountId !== null && accountId !== undefined && currencies.get(accountId) !== transaction.currency) {
      throw new Error(mismatchMessage);
    }
  }
};
