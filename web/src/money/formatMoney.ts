import { IMoneyResponse } from "../clients/types";

const displayLocale = "en-GB";

const fractionDigitsFor = (currency: string): number =>
  new Intl.NumberFormat(displayLocale, { style: "currency", currency }).resolvedOptions()
    .maximumFractionDigits ?? 2;

export const toMajorUnits = (money: IMoneyResponse): number => {
  const digits = fractionDigitsFor(money.currency);
  return money.minorUnits / 10 ** digits;
};

export const formatMoney = (money: IMoneyResponse): string =>
  new Intl.NumberFormat(displayLocale, {
    style: "currency",
    currency: money.currency,
  }).format(toMajorUnits(money));
