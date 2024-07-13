export interface ICategory {
  id: number;
  label: string;
  priority: IPriority;
}

export interface IPriority {
  id: number;
  name: string;
  weight: number;
}
