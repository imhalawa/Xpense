import * as yup from "yup";

export const prioritySchema: yup.ObjectSchema<IPriority> = yup.object().shape({
  id: yup.number().required(),
  name: yup.string().required(),
  weight: yup.number().required(),
});

export interface IPriority {
  id: number;
  name: string;
  weight: number;
}
