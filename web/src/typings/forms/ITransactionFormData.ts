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
import { categorySchema } from "../models/ICategory";
import { toMinorUnits } from "../models/IMoney";
import { ICreateTransactionRequest } from "../../clients/types";
import dayjs from "dayjs";

export const schema: yup.ObjectSchema<ITransactionFormData> = yup.object().shape({
  amount: yup.number().required("Amount is required").positive("Amount must be positive"),
  currency: yup
    .mixed<Currency>()
    .oneOf(Object.values(Currency))
    .nonNullable()
    .required("Currency is required"),
  dateOfTransaction: yup.number().required().nonNullable(),
  merchant: merchantSchema.nullable().required("Please select a merchant"),
  category: categorySchema.nullable().required("Please select a category"),
  account: accountSchema.nullable().required("Please select an account"),
  type: yup
    .mixed<TransactionType>()
    .oneOf(Object.values(TransactionType) as TransactionType[])
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

export const fromTransactionFormData = (
  transaction: ITransactionFormData
): ICreateTransactionRequest => {
  const accountNumber = transaction.account!.accountNumber;
  const credited = transaction.type === TransactionType.CREDIT;

  return {
    amount: toMinorUnits(transaction.amount, transaction.currency),
    sourceAccountNumber: credited ? null : accountNumber,
    destinationAccountNumber: credited ? accountNumber : null,
    categoryId: transaction.category!.id,
    merchant: {
      id: transaction.merchant!.id,
      label: transaction.merchant!.label,
      create: transaction.merchant!.create ?? false,
    },
    tags: (transaction.tags ?? []).map((tag) => ({
      id: tag.id ?? null,
      label: tag.label,
      create: tag.create ?? false,
    })),
    occurredAt: dayjs.unix(transaction.dateOfTransaction).toISOString(),
  };
};
