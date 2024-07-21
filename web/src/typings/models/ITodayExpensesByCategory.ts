import { ICategory } from "./ICategory";
import { IMoney } from "./IMoney";

export interface ITodayExpensesByCategory{
    expenses: ICategoryExpenses[];
    total: IMoney;
}

export interface ICategoryExpenses{
    id: number;
    category: ICategory;
    amount: IMoney;
}