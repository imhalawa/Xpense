import * as yup from "yup";
import { accountSchema, IAccount } from "../models/IAccount";
import { Currency } from "../enums/Currency";
import { ICategory } from "../models/ICategory";
import { IMerchant, merchantSchema } from "../models/IMerchant";
import { ITag, tagSchema } from "../models/ITag";
import { TransactionType } from "../enums/TransactionType";
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
  merchant: merchantSchema.nullable().when("type", {
    is: (type: TransactionType) => type !== TransactionType.TRANSFER,
    then: (field) => field.required("Please select a merchant"),
  }),
  category: categorySchema.nullable().when("type", {
    is: (type: TransactionType) => type !== TransactionType.TRANSFER,
    then: (field) => field.required("Please select a category"),
  }),
  account: accountSchema.nullable().required("Please select an account"),
  counterpartyAccount: accountSchema.nullable().when("type", {
    is: TransactionType.TRANSFER,
    then: (field) => field
      .required("Please select the destination account")
      .test(
        "different-transfer-account",
        "Choose a different destination account",
        function (counterpartyAccount) {
          const form = this.parent as ITransactionFormData;
          return counterpartyAccount?.accountNumber !== form.account?.accountNumber;
        },
      )
      .test(
        "matching-transfer-currency",
        "Transfer accounts must use the same currency",
        function (counterpartyAccount) {
          const form = this.parent as ITransactionFormData;
          return counterpartyAccount?.balance.currency === form.account?.balance.currency;
        },
      ),
  }),
  type: yup
    .mixed<TransactionType>()
    .oneOf(Object.values(TransactionType) as TransactionType[])
    .required()
    .nonNullable(),
  tags: yup.array().of(tagSchema).nullable().required(),
  reason: yup.string().nullable().default(null),
});

export interface ITransactionFormData {
  amount: number;
  currency: Currency;
  type: TransactionType;
  dateOfTransaction: number;
  account: IAccount | null;
  counterpartyAccount: IAccount | null;
  category: ICategory | null;
  merchant: IMerchant | null;
  tags: ITag[] | null;
  reason: string | null;
}

export const fromTransactionFormData = (
  transaction: ITransactionFormData
): ICreateTransactionRequest => {
  const accountNumber = transaction.account!.accountNumber;
  const credited = transaction.type === TransactionType.CREDIT;
  const transferred = transaction.type === TransactionType.TRANSFER;

  return {
    amount: toMinorUnits(transaction.amount, transaction.currency),
    sourceAccountNumber: credited ? null : accountNumber,
    destinationAccountNumber: credited
      ? accountNumber
      : transferred
        ? transaction.counterpartyAccount!.accountNumber
        : null,
    categoryId: transaction.category?.id ?? null,
    merchant:
      transaction.merchant === null
        ? null
        : {
            id: transaction.merchant.id,
            label: transaction.merchant.label,
            create: transaction.merchant.create ?? false,
          },
    tags: (transaction.tags ?? []).map((tag) => ({
      id: tag.id ?? null,
      label: tag.label,
      create: tag.create ?? false,
    })),
    occurredAt: dayjs.unix(transaction.dateOfTransaction).toISOString(),
    reason: transaction.reason,
  };
};
