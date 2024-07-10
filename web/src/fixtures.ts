import {ICategory, IPriority} from "./typings/ICategory.ts";
import { Currency } from "./typings/IMoney.ts";
import { ITransactionRow } from "./typings/ITransactionRow.ts";
import { TransactionType } from "./typings/ITransactionType.ts";
import { ToUnixTimeStamp } from "./utils/DateUtils.ts";

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


export const createTransactionFixture = (): ITransactionRow[] => [
    {
      id: 1,
      amount: {
        currency: Currency.DOLLAR,
        value: 1000,
      },
      date: ToUnixTimeStamp("2022-01-01"),
      category: {
        id: 1,
        name: "Category 1",
        priority: {
          id: 1,
          name: "Priority 1",
          weight: 1,
        },
      },
      merchant: {
        id: 1,
        name: "Merchant 1",
      },
      account: {
        id: 1,
        accountNumber: "123456789",
        balance: 1000,
        link: "https://example.com",
      },
      tags: [
        {
          id: 1,
          name: "Tag 1",
        },
        {
          id: 2,
          name: "Tag 2",
        },
        {
          id: 3,
          name: "Tag 3",
        },
        {
          id: 4,
          name: "Tag 4",
        },
        {
          id: 5,
          name: "Tag 5",
        },
      ],
      type: TransactionType.CREDIT,
    },
    {
      id: 2,
      amount: {
        currency: Currency.EUR,
        value: 2000,
      },
      date: ToUnixTimeStamp("2022-01-02"),
      category: {
        id: 2,
        name: "Category 2",
        priority: {
          id: 1,
          name: "Priority 1",
          weight: 1,
        },
      },
      merchant: {
        id: 2,
        name: "Merchant 2",
      },
      account: {
        id: 2,
        accountNumber: "123456789",
        balance: 1000,
        link: "https://example.com",
      },
      tags: [
        {
          id: 1,
          name: "Tag 1",
        },
        {
          id: 2,
          name: "Tag 2",
        },
      ],
      type: TransactionType.DEBIT,
    },
    {
      id: 3,
      amount: {
        currency: Currency.EUR,
        value: 3000,
      },
      date: ToUnixTimeStamp("2022-01-03"),
      category: {
        id: 3,
        name: "Category 3",
        priority: {
          id: 1,
          name: "Priority 1",
          weight: 1,
        },
      },
      merchant: {
        id: 3,
        name: "Merchant 3",
      },
      account: {
        id: 3,
        accountNumber: "123456789",
        balance: 1000,
        link: "https://example.com",
      },
      tags: [
        {
          id: 1,
          name: "Tag 1",
        },
        {
          id: 2,
          name: "Tag 2",
        },
      ],
      type: TransactionType.DEBIT,
    },
    {
      id: 4,
      amount: {
        currency: Currency.EUR,
        value: 4000,
      },
      date: ToUnixTimeStamp("2022-01-04"),
      category: {
        id: 4,
        name: "Category 4",
        priority: {
          id: 1,
          name: "Priority 1",
          weight: 1,
        },
      },
      merchant: {
        id: 4,
        name: "Merchant 4",
      },    
      account: {
        id: 4,  
        accountNumber: "123456789",
        balance: 1000,  
        link: "https://example.com",
      },    
      tags: [
        {
          id: 1,
          name: "Tag 1",
        },
        {
          id: 2,
          name: "Tag 2",
        },
      ],
      type: TransactionType.CREDIT,
    },
  ];