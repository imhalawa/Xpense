import {
  IAccount,
  ICategory,
  IMerchant,
  IMoney,
  ITag,
  TransactionType,
} from ".";

export interface ITransaction {
  id: number;
  amount: IMoney;
  date: number;
  category: ICategory;
  merchant: IMerchant;
  account: IAccount;
  type: TransactionType;
  tags: ITag[] | null;
}

const buildTransaction = (
  id: number,
  amount: IMoney,
  date: number,
  category: ICategory,
  merchant: IMerchant,
  account: IAccount,
  type: TransactionType,
  tags: ITag[] | null
): ITransaction => ({
  id,
  date,
  merchant,
  category,
  amount,
  account,
  type,
  tags,
});

export default buildTransaction;
