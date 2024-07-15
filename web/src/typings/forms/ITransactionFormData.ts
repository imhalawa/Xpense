import * as yup from "yup";
import { Currency, IAccount, ICategory, IMerchant, ITag, TransactionType } from "..";
import { IPriority } from "../ICategory";
import buildTransaction from "../ITransaction";


// TODO move each schema to a separate file and import them here

export const accountSchema: yup.ObjectSchema<IAccount> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  accountNumber: yup.string().required(),
  balance: yup.number().required().positive("Account balance must be positive"),
  isMainAccount: yup.boolean().required(),
});

export const prioritySchema: yup.ObjectSchema<IPriority> = yup.object().shape({
  id: yup.number().required(),
  name: yup.string().required(),
  weight: yup.number().required(),
});

export const categorySchema: yup.ObjectSchema<ICategory> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  priority: prioritySchema.required(),
});

export const merchantSchema: yup.ObjectSchema<IMerchant> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  create: yup.boolean().optional(),
});

export const tagSchema: yup.ObjectSchema<ITag> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().nonNullable().required(),
  create: yup.boolean().required(),
});

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
      value: data.amount,
    },
    data.dateOfTransaction,
    data.category!,
    data.merchant!,
    data.account!,
    data.type,
    data.tags
  );
};
