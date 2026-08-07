import { IOption } from "..";
import * as yup from "yup";

export interface IAccount extends IOption {
  accountNumber: string;
  balance: number;
  isDefault: boolean;
}

export const accountSchema: yup.ObjectSchema<IAccount> = yup.object().shape({
  id: yup.number().required().nullable(),
  createdOn: yup.number().required().nullable(),
  lastUpdated: yup.number().required().nullable(),
  label: yup.string().required(),
  accountNumber: yup.string().required(),
  balance: yup.number().required().positive("Account balance must be positive"),
  isDefault: yup.boolean().required(),
});
