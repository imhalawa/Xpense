import { toCents } from "./../../typings/models/IMoney";
import { IMerchant, ITag } from "../../typings";
import { ITransactionFormData } from "../../typings/forms/ITransactionFormData";
import { IMoney, createMoney } from "../../typings/models/IMoney";

export interface ITransactionRequest {
  amount: IMoney;
  accountNumber: string;
  categoryId: number;
  merchant: IMerchant;
  tags: ITag[];
  createdOn?: number | null;
}

export const fromTransactionFormData = (transaction: ITransactionFormData): ITransactionRequest => {
  return {
    amount: toCents(createMoney(transaction.amount, transaction.currency)),
    accountNumber: transaction.account!.accountNumber,
    categoryId: transaction.category!.id!,
    merchant: transaction.merchant!,
    tags: transaction.tags!,
    createdOn: transaction.dateOfTransaction,
  };
};
