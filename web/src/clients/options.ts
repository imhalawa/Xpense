import axios from "axios";
import { IPriorityResponse } from "./types";
export { createCategory, listCategories } from "./categories";
export { listAccounts } from "./accounts";
export { listMerchants } from "./merchants";
export { listTags } from "./tags";

export const listPriorities = async (): Promise<IPriorityResponse[]> => {
  const response = await axios.get<IPriorityResponse[]>("/api/v1/priorities");
  return response.data;
};

export interface IOptionQuery {
  search?: string;
  limit?: number;
}
