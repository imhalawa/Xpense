import axios from "axios";
import { Dayjs } from "dayjs";
import { ICreateTransactionRequest, ITransactionPageResponse, ITransactionResponse } from "./types";

export interface ITransactionQuery {
  page: number;
  pageSize: number;
  day?: Dayjs | null;
}

export const listTransactions = async ({
  page,
  pageSize,
  day,
}: ITransactionQuery): Promise<ITransactionPageResponse> => {
  const response = await axios.get<ITransactionPageResponse>("/api/v1/transactions", {
    params: {
      page,
      pageSize,
      from: day ? day.startOf("day").toISOString() : undefined,
      to: day ? day.startOf("day").add(1, "day").toISOString() : undefined,
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
