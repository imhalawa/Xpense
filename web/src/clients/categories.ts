import axios from "axios";
import type {
  ICategoryResponse,
  ICreateCategoryRequest,
  IUpdateCategoryRequest,
} from "./types";

export const listCategories = async (): Promise<ICategoryResponse[]> =>
  (await axios.get<ICategoryResponse[]>("/api/v1/categories")).data;

export const getCategory = async (id: string): Promise<ICategoryResponse> =>
  (await axios.get<ICategoryResponse>(`/api/v1/categories/${encodeURIComponent(id)}`)).data;

export const createCategory = async (
  request: ICreateCategoryRequest,
): Promise<ICategoryResponse> => (await axios.post<ICategoryResponse>("/api/v1/categories", request)).data;

export const updateCategory = async (
  id: string,
  request: IUpdateCategoryRequest,
): Promise<ICategoryResponse> =>
  (await axios.put<ICategoryResponse>(`/api/v1/categories/${encodeURIComponent(id)}`, request)).data;

export const deleteCategory = async (id: string): Promise<void> => {
  await axios.delete(`/api/v1/categories/${encodeURIComponent(id)}`);
};
