import { Currency } from "../typings";

export interface IMoneyResponse {
  minorUnits: number;
  currency: Currency;
}

export interface IPriorityResponse {
  id: number;
  label: string;
  weight: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ICategoryResponse {
  id: number;
  label: string;
  priority: IPriorityResponse;
  createdAt: string;
  updatedAt: string | null;
}

export interface IAccountResponse {
  accountNumber: string;
  label: string;
  balance: IMoneyResponse;
  isDefault: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface IMerchantResponse {
  id: number;
  label: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ITagResponse {
  id: number;
  label: string;
  bgColorHex: string;
  fgColorHex: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ITransactionOptionResponse {
  id: number;
  label: string;
}

export type TransactionKind = "income" | "expense" | "transfer";

export interface ITransactionResponse {
  id: number;
  kind: TransactionKind;
  amount: IMoneyResponse;
  sourceAccountNumber: string | null;
  destinationAccountNumber: string | null;
  categoryId: number | null;
  merchant: ITransactionOptionResponse | null;
  tags: ITransactionOptionResponse[];
  reason: string | null;
  occurredAt: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ITransactionPageResponse {
  items: ITransactionResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ICategorySpendingResponse {
  id: number;
  category: ICategoryResponse;
  amount: IMoneyResponse;
}

export interface ISpendingByCategoryResponse {
  expenses: ICategorySpendingResponse[];
  totals: IMoneyResponse[];
}

export interface IOptionRequest {
  id: number | null;
  label: string;
  create: boolean;
}

export interface IMoneyRequest {
  minorUnits: number;
  currency: Currency;
}

export interface ICreateTransactionRequest {
  amount: IMoneyRequest;
  sourceAccountNumber?: string | null;
  destinationAccountNumber?: string | null;
  categoryId?: number | null;
  merchant?: IOptionRequest | null;
  tags?: IOptionRequest[] | null;
  reason?: string | null;
  occurredAt?: string | null;
}
