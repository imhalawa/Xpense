import {ICategory, IPriority} from "./typings/ICategory.ts";

export const createCategoriesFixture = (): ICategory[] => {

    const priorities = createPriorityFixture();
    return [
        {id: 1, name: 'Groceries', priority: priorities[0]},
        {id: 2, name: 'Online Shopping', priority: priorities[1]},
        {id: 3, name: 'Luxury', priority: priorities[2]},
        {id: 4, name: 'Restaurants', priority: priorities[1]},
        {id: 5, name: 'Cafe', priority: priorities[2]},
    ];
}

export const createPriorityFixture = (): IPriority[] => [
    {id: 1, name: 'High', weight: 1},
    {id: 2, name: 'Medium', weight: 2},
    {id: 3, name: 'Low', weight: 3},
]