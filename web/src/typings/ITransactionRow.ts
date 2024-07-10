import { IAccount } from "./IAccount.ts";
import { ICategory } from "./ICategory.ts";
import { IMerchant } from "./IMerchant.ts";
import { IMoney } from "./IMoney.ts";
import { ITag } from "./ITag.ts";
import { TransactionType } from "./ITransactionType.ts";

export interface ITransactionRow {
  id: number;
  amount: IMoney;
  date: number;
  category: ICategory;
  merchant: IMerchant;
  account: IAccount;
  type: TransactionType;
  tags: ITag[];
}

const createTransactionRow = (
  id: number,
  amount: IMoney,
  date: number,
  category: ICategory,
  merchant: IMerchant,
  account: IAccount,
  type: TransactionType,
  tags: ITag[]
): ITransactionRow => ({
  id,
  date,
  merchant,
  category,
  amount,
  account,
  tags,
  type,
});

export default createTransactionRow;
