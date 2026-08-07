import { Currency } from "../enums/Currency";

export interface IMoney {
  minorUnits: number;
  currency: Currency;
}

export const createMoney = (minorUnits: number, currency: Currency): IMoney => ({
  minorUnits,
  currency,
});

export const toSingle = (money: IMoney): number => +(money.minorUnits / 100).toFixed(2);

export const toMinorUnits = (amount: number, currency: Currency): IMoney =>
  createMoney(Math.round(amount * 100), currency);
