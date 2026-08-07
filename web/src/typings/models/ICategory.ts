import * as yup from "yup";
import { prioritySchema, IPriority, IBaseEntity } from "..";

export const categorySchema: yup.ObjectSchema<ICategory> = yup.object().shape({
  id: yup.number().required().nullable(),
  label: yup.string().required(),
  priority: prioritySchema.required(),
  createdOn: yup.number().nullable(),
  lastUpdated: yup.number().nullable(),
});

export interface ICategory extends IBaseEntity {
  label: string;
  priority: IPriority;
}
