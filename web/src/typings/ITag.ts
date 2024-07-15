import { IOption } from "./IOption";

export interface ITag extends IOption {
  id: number | null;
  create: boolean | null;
}
