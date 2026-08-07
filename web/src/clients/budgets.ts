import axios from "axios";
import { Dayjs } from "dayjs";
import { IBudgetResponse } from "./types";

export const listBudgets = async (on?: Dayjs | null): Promise<IBudgetResponse[]> => {
  const response = await axios.get<IBudgetResponse[]>("/api/v1/budgets", {
    params: { on: on ? on.toISOString() : undefined },
  });
  return response.data;
};
