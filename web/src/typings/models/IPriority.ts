import * as yup from "yup";
import { IBaseEntity } from "..";

export const prioritySchema: yup.ObjectSchema<IPriority> = yup.object().shape({
  id: yup.number().required().nullable(),
  createdAt: yup.string().nullable(),
  updatedAt: yup.string().nullable(),
  label: yup.string().nonNullable().required(),
  weight: yup.number().required(),
});

export interface IPriority extends IBaseEntity {
  label: string;
  weight: number;
}
