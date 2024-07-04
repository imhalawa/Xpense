export interface ICategory {
    id: number;
    name: string;
    priority: IPriority;
}

export interface IPriority {
    id: number;
    name: string;
    weight: number;
}