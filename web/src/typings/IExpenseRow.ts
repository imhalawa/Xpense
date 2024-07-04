import {ICategory} from "./ICategory.ts";

export interface IExpenseRow {
    id: number;
    date: number;
    merchant: string;
    category: ICategory;
    amount: number;
}

const createExpenseRow = (id: number, date: number, merchant: string, category: ICategory, amount: number): IExpenseRow => ({
    id: id,
    date: date,
    merchant: merchant,
    category: category,
    amount: amount,
})

export default createExpenseRow;