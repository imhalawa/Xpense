import axios from "axios";
import { Dayjs } from "dayjs";
import {
  ICreateTransactionRequest,
  ITransactionPageResponse,
  ITransactionResponse,
  IUpdateTransactionRequest,
} from "./types";

export interface ITransactionQuery {
  page: number;
  pageSize: number;
  day?: Dayjs | null;
  from?: Dayjs | null;
  to?: Dayjs | null;
}

interface IHalfOpenRange {
  from: string | undefined;
  to: string | undefined;
}

const halfOpenRange = ({ day, from, to }: ITransactionQuery): IHalfOpenRange => {
  if (from != null || to != null)
    return {
      from: from?.startOf("day").toISOString(),
      to: to?.startOf("day").add(1, "day").toISOString(),
    };

  if (day != null)
    return {
      from: day.startOf("day").toISOString(),
      to: day.startOf("day").add(1, "day").toISOString(),
    };

  return { from: undefined, to: undefined };
};

export const listTransactions = async (
  query: ITransactionQuery
): Promise<ITransactionPageResponse> => {
  const response = await axios.get<ITransactionPageResponse>("/api/v1/transactions", {
    params: {
      page: query.page,
      pageSize: query.pageSize,
      ...halfOpenRange(query),
    },
  });
  return response.data;
};

export const createTransaction = async (
  request: ICreateTransactionRequest
): Promise<ITransactionResponse> => {
  const response = await axios.post<ITransactionResponse>("/api/v1/transactions", request);
  return response.data;
};

export const updateTransaction = async (
  id: string,
  request: IUpdateTransactionRequest,
): Promise<ITransactionResponse> => {
  const response = await axios.put<ITransactionResponse>(`/api/v1/transactions/${id}`, request);
  return response.data;
};

export const deleteTransaction = async (id: string): Promise<void> => {
  await axios.delete(`/api/v1/transactions/${id}`);
};
