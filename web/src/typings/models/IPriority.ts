import * as yup from "yup";
import { IBaseEntity } from "..";

export const prioritySchema: yup.ObjectSchema<IPriority> = yup.object().shape({
  id: yup.number().required().nullable(),
  createdOn: yup.number().nullable(),
  lastUpdated: yup.number().nullable(),
  label: yup.string().nonNullable().required(),
  weight: yup.number().required(),
});

export interface IPriority extends IBaseEntity {
  label: string;
  weight: number;
}
