import { IOption } from "..";
import * as yup from "yup";

export const tagSchema: yup.ObjectSchema<ITag> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().nonNullable().required(),
  create: yup.boolean().required(),
  createdAt: yup.string().nullable(),
  updatedAt: yup.string().nullable(),
  bgColorHex: yup.string().nullable(),
  fgColorHex: yup.string().nullable(),
});

export interface ITag extends IOption {
  create: boolean | null;
  bgColorHex?: string | null;
  fgColorHex?: string | null;
}
