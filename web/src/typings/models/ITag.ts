import { IOption } from "..";
import * as yup from "yup";

export const tagSchema: yup.ObjectSchema<ITag> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().nonNullable().required(),
  create: yup.boolean().required(),
});

export interface ITag extends IOption {
  id: number | null;
  create: boolean | null;
}
