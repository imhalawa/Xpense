import axios from "axios";
import { Dayjs } from "dayjs";
import { IBudgetResponse, ICreateBudgetRequest, IUpdateBudgetRequest } from "./types";

export const listBudgets = async (on?: Dayjs | null): Promise<IBudgetResponse[]> => {
  const response = await axios.get<IBudgetResponse[]>("/api/v1/budgets", {
    params: { on: on ? on.toISOString() : undefined },
  });
  return response.data;
};

export const createBudget = async (request: ICreateBudgetRequest): Promise<IBudgetResponse> => {
  const response = await axios.post<IBudgetResponse>("/api/v1/budgets", request);
  return response.data;
};

export const updateBudget = async (
  id: number,
  request: IUpdateBudgetRequest
): Promise<IBudgetResponse> => {
  const response = await axios.put<IBudgetResponse>(`/api/v1/budgets/${id}`, request);
  return response.data;
};

export const deleteBudget = async (id: number): Promise<void> => {
  await axios.delete(`/api/v1/budgets/${id}`);
};
