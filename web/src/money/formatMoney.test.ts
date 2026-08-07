import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { formatMoney, toMajorUnits } from "./formatMoney";

describe("toMajorUnits", () => {
  it("shifts euro minor units by two places", () => {
    expect(toMajorUnits({ minorUnits: 4218, currency: Currency.EUR })).toBe(42.18);
  });

  it("handles a whole amount", () => {
    expect(toMajorUnits({ minorUnits: 240000, currency: Currency.EUR })).toBe(2400);
  });

  it("handles zero", () => {
    expect(toMajorUnits({ minorUnits: 0, currency: Currency.EUR })).toBe(0);
  });

  it("keeps a negative amount negative", () => {
    expect(toMajorUnits({ minorUnits: -4218, currency: Currency.EUR })).toBe(-42.18);
  });
});

describe("formatMoney", () => {
  it("formats euros with a symbol and two decimals", () => {
    const formatted = formatMoney({ minorUnits: 4218, currency: Currency.EUR });
    expect(formatted).toMatch(/42[.,]18/);
    expect(formatted).toMatch(/€/);
  });

  it("groups thousands", () => {
    expect(formatMoney({ minorUnits: 118060, currency: Currency.EUR })).toMatch(/1[.,\s]180[.,]60/);
  });

  it("formats dollars with a dollar sign", () => {
    expect(formatMoney({ minorUnits: 999, currency: Currency.USD })).toMatch(/\$/);
  });
});
