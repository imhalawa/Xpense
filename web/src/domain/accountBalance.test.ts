import { describe, expect, it } from "vitest";
import { accountBalances } from "./accountBalance";
import { Currency } from "../typings/enums/Currency";

describe("accountBalances", () => {
  it("keeps one balance per currency and never creates a total", () => {
    expect(
      accountBalances([
        { currency: Currency.EUR, openingBalanceMinorUnits: 1250 },
        { currency: Currency.USD, openingBalanceMinorUnits: 2000 },
        { currency: Currency.EUR, openingBalanceMinorUnits: -300 },
      ]),
    ).toEqual([
      { currency: Currency.EUR, minorUnits: 950 },
      { currency: Currency.USD, minorUnits: 2000 },
    ]);
  });

  it("applies income, expense, and transfer effects before grouping balances", () => {
    expect(
      accountBalances(
        [
          { id: "cash", currency: Currency.EUR, openingBalanceMinorUnits: 2000 },
          { id: "savings", currency: Currency.EUR, openingBalanceMinorUnits: 300 },
        ],
        [
          { kind: "expense", accountId: "cash", counterpartyAccountId: null, minorUnits: 250, currency: Currency.EUR },
          { kind: "income", accountId: "cash", counterpartyAccountId: null, minorUnits: 100, currency: Currency.EUR },
          { kind: "transfer", accountId: "cash", counterpartyAccountId: "savings", minorUnits: 500, currency: Currency.EUR },
        ],
      ),
    ).toEqual([{ currency: Currency.EUR, minorUnits: 2150 }]);
  });
});
