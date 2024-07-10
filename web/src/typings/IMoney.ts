export interface IMoney {
  value: number;
  currency: Currency;
}

export enum Currency {
  EUR,
  DOLLAR,
}
