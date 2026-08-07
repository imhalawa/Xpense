import axios from "axios";
import { ITransaction } from "../typings";
import { IResponse } from "./types/IResponse";
import { ITransactionRequest } from "./types/ITransactionRequest";
import { IPaginatedResponse } from "./types/IPaginatedResponse";

export const filter = async (
  page: number,
  size: number,
  date?: number
): Promise<IPaginatedResponse<ITransaction>> => {
  var response = await axios.get<IPaginatedResponse<ITransaction>>("/api/transaction/filter", {
    params: {
      page: page,
      size: size,
      date: date,
    },
  });
  return response.data;
};

export const deposit = async (data: ITransactionRequest): Promise<IResponse<ITransaction>> => {
  var response = await axios.post<IResponse<ITransaction>>("/api/transaction/deposit", data);
  return response.data;
};

export const withdraw = async (data: ITransactionRequest): Promise<IResponse<ITransaction>> => {
  var response = await axios.post<IResponse<ITransaction>>("/api/transaction/withdraw", data);
  return response.data;
};
