import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { validateTransactionCurrencies } from "./currencyMatch";

describe("validateTransactionCurrencies", () => {
  it("rejects a transaction amount that does not match its account with prose", () => {
    expect(() =>
      validateTransactionCurrencies(
        { currency: Currency.USD, accountId: "cash", counterpartyAccountId: null },
        [{ id: "cash", currency: Currency.EUR }],
      ),
    ).toThrow("The transaction currency must match the account currency.");
  });

  it("accepts matching source and destination accounts", () => {
    expect(() =>
      validateTransactionCurrencies(
        { currency: Currency.EUR, accountId: "cash", counterpartyAccountId: "savings" },
        [
          { id: "cash", currency: Currency.EUR },
          { id: "savings", currency: Currency.EUR },
        ],
      ),
    ).not.toThrow();
  });
});
