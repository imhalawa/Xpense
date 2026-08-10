import axios from "axios";
import type { ITagRequest, ITagResponse } from "./types";
import type { IOptionQuery } from "./options";

const optionParams = ({ search, limit }: IOptionQuery) => ({
  search: search?.trim() ? search.trim() : undefined,
  limit,
});

export const listTags = async (query: IOptionQuery = {}): Promise<ITagResponse[]> =>
  (await axios.get<ITagResponse[]>("/api/v1/tags", { params: optionParams(query) })).data;

export const getTag = async (id: string): Promise<ITagResponse> =>
  (await axios.get<ITagResponse>(`/api/v1/tags/${encodeURIComponent(id)}`)).data;

export const createTag = async (request: ITagRequest): Promise<ITagResponse> =>
  (await axios.post<ITagResponse>("/api/v1/tags", request)).data;

export const updateTag = async (id: string, request: ITagRequest): Promise<ITagResponse> =>
  (await axios.put<ITagResponse>(`/api/v1/tags/${encodeURIComponent(id)}`, request)).data;

export const deleteTag = async (id: string): Promise<void> => {
  await axios.delete(`/api/v1/tags/${encodeURIComponent(id)}`);
};
