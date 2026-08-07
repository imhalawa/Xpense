import { Currency } from "..";

export interface IMoney {
  cents: number;
  currency: Currency;
}

export const createMoney = (cents: number, currency: Currency): IMoney => ({
  cents: cents,
  currency,
});

export const toSingle = (money: IMoney): number => {
  return +(money.cents / 100).toFixed(2);
};

export const toCents = (money: IMoney): IMoney => {
  return { ...money, cents: money.cents * 100 };
};
