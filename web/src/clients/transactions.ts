import axios from "axios";
import { ITransaction } from "../typings";
import { IResponse } from "./types/IResponse";
import { ITransactionRequest } from "./types/ITransactionRequest";

export const getAllTransactions = async (): Promise<IResponse<ITransaction[]>> => {
  var response = await axios.get<IResponse<ITransaction[]>>("/api/transaction");
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
