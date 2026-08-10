import axios from "axios";
import type {
  IAccountResponse,
  ICreateAccountRequest,
  IUpdateAccountRequest,
} from "./types";

export const listAccounts = async (): Promise<IAccountResponse[]> =>
  (await axios.get<IAccountResponse[]>("/api/v1/accounts")).data;

export const getAccount = async (accountNumber: string): Promise<IAccountResponse> =>
  (await axios.get<IAccountResponse>(`/api/v1/accounts/${encodeURIComponent(accountNumber)}`)).data;

export const createAccount = async (request: ICreateAccountRequest): Promise<IAccountResponse> =>
  (await axios.post<IAccountResponse>("/api/v1/accounts", request)).data;

export const updateAccount = async (
  accountNumber: string,
  request: IUpdateAccountRequest,
): Promise<IAccountResponse> =>
  (await axios.put<IAccountResponse>(`/api/v1/accounts/${encodeURIComponent(accountNumber)}`, request)).data;

export const deleteAccount = async (accountNumber: string): Promise<void> => {
  await axios.delete(`/api/v1/accounts/${encodeURIComponent(accountNumber)}`);
};
