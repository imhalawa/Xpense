import { IOption } from "..";
import * as yup from "yup";

export interface IMerchant extends IOption {
  id: number;
  create?: boolean;
}

export const merchantSchema: yup.ObjectSchema<IMerchant> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  create: yup.boolean().optional(),
});
