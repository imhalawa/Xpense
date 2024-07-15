import { IOption } from "./IOption";

export interface IMerchant extends IOption {
  id: number;
  create?: boolean;
}
