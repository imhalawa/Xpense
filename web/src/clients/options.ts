import axios from "axios";
import { IAccountResponse, ICategoryResponse, IMerchantResponse, ITagResponse } from "./types";

export const listAccounts = async (): Promise<IAccountResponse[]> => {
  const response = await axios.get<IAccountResponse[]>("/api/v1/accounts");
  return response.data;
};

export const listCategories = async (): Promise<ICategoryResponse[]> => {
  const response = await axios.get<ICategoryResponse[]>("/api/v1/categories");
  return response.data;
};

export interface IOptionQuery {
  search?: string;
  limit?: number;
}

const optionParams = ({ search, limit }: IOptionQuery) => ({
  search: search?.trim() ? search.trim() : undefined,
  limit,
});

export const listMerchants = async (query: IOptionQuery = {}): Promise<IMerchantResponse[]> => {
  const response = await axios.get<IMerchantResponse[]>("/api/v1/merchants", {
    params: optionParams(query),
  });
  return response.data;
};

export const listTags = async (query: IOptionQuery = {}): Promise<ITagResponse[]> => {
  const response = await axios.get<ITagResponse[]>("/api/v1/tags", {
    params: optionParams(query),
  });
  return response.data;
};
