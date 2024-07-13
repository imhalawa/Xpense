import { IOption } from "./IOption";

export interface IAccount extends IOption {
  id: number;
  accountNumber: string;
  balance: number;
  isMainAccount: boolean;
}
