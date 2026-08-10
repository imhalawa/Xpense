import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { spendingByCategory } from "./spendingByCategory";

describe("spendingByCategory", () => {
  it("groups expenses by category and currency without a cross-currency total", () => {
    expect(
      spendingByCategory([
        { kind: "expense", categoryId: "food", amount: { minorUnits: 1000, currency: Currency.EUR } },
        { kind: "expense", categoryId: "food", amount: { minorUnits: 200, currency: Currency.USD } },
        { kind: "expense", categoryId: "travel", amount: { minorUnits: 500, currency: Currency.EUR } },
        { kind: "income", categoryId: "food", amount: { minorUnits: 900, currency: Currency.EUR } },
      ]),
    ).toEqual([
      { categoryId: "food", amount: { minorUnits: 1000, currency: Currency.EUR } },
      { categoryId: "food", amount: { minorUnits: 200, currency: Currency.USD } },
      { categoryId: "travel", amount: { minorUnits: 500, currency: Currency.EUR } },
    ]);
  });
});
