import { IOption } from "..";
import * as yup from "yup";

export const tagSchema: yup.ObjectSchema<ITag> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().nonNullable().required(),
  create: yup.boolean().required(),
  createdOn: yup.number().required().nullable(),
  lastUpdated: yup.number().required().nullable(),
  bgColorHex: yup.string().required().nullable(),
  fgColorHex: yup.string().required().nullable(),
});

export interface ITag extends IOption {
  create: boolean | null;
  bgColorHex: string | null;
  fgColorHex: string | null;
}
