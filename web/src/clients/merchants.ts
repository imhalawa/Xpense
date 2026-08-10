import axios from "axios";
import type { IMerchantRequest, IMerchantResponse } from "./types";
import type { IOptionQuery } from "./options";

const optionParams = ({ search, limit }: IOptionQuery) => ({
  search: search?.trim() ? search.trim() : undefined,
  limit,
});

export const listMerchants = async (
  query: IOptionQuery = {},
): Promise<IMerchantResponse[]> =>
  (await axios.get<IMerchantResponse[]>("/api/v1/merchants", { params: optionParams(query) })).data;

export const getMerchant = async (id: string): Promise<IMerchantResponse> =>
  (await axios.get<IMerchantResponse>(`/api/v1/merchants/${encodeURIComponent(id)}`)).data;

export const createMerchant = async (
  request: IMerchantRequest,
): Promise<IMerchantResponse> => (await axios.post<IMerchantResponse>("/api/v1/merchants", request)).data;

export const updateMerchant = async (
  id: string,
  request: IMerchantRequest,
): Promise<IMerchantResponse> =>
  (await axios.put<IMerchantResponse>(`/api/v1/merchants/${encodeURIComponent(id)}`, request)).data;

export const deleteMerchant = async (id: string): Promise<void> => {
  await axios.delete(`/api/v1/merchants/${encodeURIComponent(id)}`);
};
