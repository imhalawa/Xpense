import { IAccount, IBaseEntity, ICategory, IMerchant, IMoney, ITag, TransactionType } from "..";

export interface ITransaction extends IBaseEntity {
  amount: IMoney;
  category: ICategory;
  merchant: IMerchant;
  account: IAccount;
  type: TransactionType;
  tags: ITag[] | null;
}

const buildTransaction = (
  id: number | null,
  amount: IMoney,
  createdAt: number | null,
  lastModifiedOn: number | null,
  category: ICategory,
  merchant: IMerchant,
  account: IAccount,
  type: TransactionType,
  tags: ITag[] | null
): ITransaction => ({
  id,
  createdOn: createdAt,
  lastUpdated: lastModifiedOn,
  merchant,
  category,
  amount,
  account,
  type,
  tags,
});

export default buildTransaction;
