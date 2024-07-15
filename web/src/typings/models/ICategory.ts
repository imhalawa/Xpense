import * as yup from "yup";
import { prioritySchema, IPriority } from "..";

export const categorySchema: yup.ObjectSchema<ICategory> = yup.object().shape({
  id: yup.number().required(),
  label: yup.string().required(),
  priority: prioritySchema.required(),
});

export interface ICategory {
  id: number;
  label: string;
  priority: IPriority;
}
