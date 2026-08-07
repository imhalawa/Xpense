import axios from "axios";
import { ISpendingByCategoryResponse } from "./types";

export const getSpendingByCategory = async (): Promise<ISpendingByCategoryResponse> => {
  const response = await axios.get<ISpendingByCategoryResponse>(
    "/api/v1/analytics/spending/by-category"
  );
  return response.data;
};
