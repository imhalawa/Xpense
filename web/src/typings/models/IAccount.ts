import { IOption } from "..";
import * as yup from "yup";

export interface IAccount extends IOption {
  id: number;
  accountNumber: string;
  balance: number;
  isMainAccount: boolean;
}

export const accountSchema: yup.ObjectSchema<IAccount> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  accountNumber: yup.string().required(),
  balance: yup.number().required().positive("Account balance must be positive"),
  isMainAccount: yup.boolean().required(),
});
