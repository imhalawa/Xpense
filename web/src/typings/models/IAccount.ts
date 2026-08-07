import * as yup from "yup";
import { Currency, IMoney } from "..";

export interface IAccount {
  accountNumber: string;
  label: string;
  balance: IMoney;
  isDefault: boolean;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export const accountSchema: yup.ObjectSchema<IAccount> = yup.object().shape({
  accountNumber: yup.string().required(),
  label: yup.string().required(),
  balance: yup
    .object()
    .shape({
      minorUnits: yup.number().required(),
      currency: yup.mixed<Currency>().oneOf(Object.values(Currency)).required(),
    })
    .required(),
  isDefault: yup.boolean().required(),
  createdAt: yup.string().nullable(),
  updatedAt: yup.string().nullable(),
});
