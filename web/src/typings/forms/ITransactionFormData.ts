import * as yup from "yup";
import {
  accountSchema,
  Currency,
  IAccount,
  ICategory,
  IMerchant,
  ITag,
  merchantSchema,
  tagSchema,
  TransactionType,
} from "..";
import buildTransaction from "../models/ITransaction";
import { categorySchema } from "../models/ICategory";

export const schema: yup.ObjectSchema<ITransactionFormData> = yup.object().shape({
  amount: yup.number().required("Amount is required").positive("Amount must be positive"),
  currency: yup
    .mixed<Currency>()
    .oneOf(Object.values(Currency) as number[])
    .nonNullable()
    .required("Currency is required"),
  dateOfTransaction: yup.number().required().nonNullable(),
  merchant: merchantSchema.nullable().required("Please select a merchant"),
  category: categorySchema.nullable().required("Please select a category"),
  account: accountSchema.nullable().required("Please select an account"),
  type: yup
    .mixed<TransactionType>()
    .oneOf(Object.values(TransactionType) as number[])
    .required()
    .nonNullable(),
  tags: yup.array().of(tagSchema).nullable().required(),
});

export interface ITransactionFormData {
  amount: number;
  currency: Currency;
  type: TransactionType;
  dateOfTransaction: number;
  account: IAccount | null;
  category: ICategory | null;
  merchant: IMerchant | null;
  tags: ITag[] | null;
}

export const mapToTransaction = (data: ITransactionFormData) => {
  return buildTransaction(
    0,
    {
      currency: data.currency,
      cents: data.amount,
    },
    data.dateOfTransaction,
    null,
    data.category!,
    data.merchant!,
    data.account!,
    data.type,
    data.tags
  );
};
