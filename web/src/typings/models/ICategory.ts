import * as yup from "yup";
import { prioritySchema, IPriority } from "./IPriority";
import { IBaseEntity } from "./IBaseEntity";

export const categorySchema: yup.ObjectSchema<ICategory> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().required(),
  priority: prioritySchema.required(),
  createdAt: yup.string().nullable(),
  updatedAt: yup.string().nullable(),
});

export interface ICategory extends IBaseEntity {
  label: string;
  priority: IPriority;
}
