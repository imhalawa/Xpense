import { ICategory, IPriority, IAccount, ITag, IMerchant, ITransaction, TransactionType, Currency } from "./typings";
import { ToUnixTimeStamp } from "./utils/DateUtils.ts";

export const createCategoriesFixture = (): ICategory[] => {
  const priorities = createPriorityFixture();
  return [
    { id: 1, label: "Groceries", priority: priorities[0] },
    { id: 2, label: "Online Shopping", priority: priorities[1] },
    { id: 3, label: "Luxury", priority: priorities[2] },
    { id: 4, label: "Restaurants", priority: priorities[1] },
    { id: 5, label: "Cafe", priority: priorities[2] },
  ];
};

export const createPriorityFixture = (): IPriority[] => [
  { id: 1, name: "High", weight: 1 },
  { id: 2, name: "Medium", weight: 2 },
  { id: 3, name: "Low", weight: 3 },
];

export const createAccountFixture = (): IAccount[] => [
  {
    id: 1,
    accountNumber: "123456789",
    balance: 1000,
    isMainAccount: true,
    label: "Main Account",
  },
  {
    id: 2,
    accountNumber: "433456789",
    balance: 1000,
    isMainAccount: false,
    label: "Saving Account",
  },
  {
    id: 3,
    accountNumber: "545456789",
    balance: 1000,
    isMainAccount: false,
    label: "Extra Saving Account",
  },
];

export const createTagFixture = (): ITag[] => [
  {
    id: 1,
    label: "Tag 1",
    create: false,
  },
  {
    id: 2,
    label: "Tag 2",
    create: false,
  },
  {
    id: 3,
    label: "Tag 3",
    create: false,
  },
  {
    id: 4,
    label: "Tag 4",
    create: false,
  },
  {
    id: 5,
    label: "Tag 5",
    create: false,
  },
];

export const createMerchantFixture = (): IMerchant[] => [
  {
    id: 1,
    label: "Merchant 1",
    create: false,
  },
  {
    id: 2,
    label: "Merchant 2",
    create: false,
  },
  {
    id: 3,
    label: "Merchant 3",
    create: false,
  },
];

export const createTransactionFixture = (): ITransaction[] => [
  {
    id: 1,
    amount: {
      currency: Currency.DOLLAR,
      value: 1000,
    },
    date: ToUnixTimeStamp("2024-07-01"),
    category: {
      id: 1,
      label: "Category 1",
      priority: {
        id: 1,
        name: "Priority 1",
        weight: 1,
      },
    },
    merchant: {
      id: 1,
      label: "Merchant 1",
      create: false,
    },
    account: {
      id: 1,
      accountNumber: "123456789",
      balance: 1000,
      isMainAccount: false,
      label: "Main Account",
    },
    tags: [
      {
        id: 1,
        label: "Tag 1",
        create: false,
      },
      {
        id: 2,
        label: "Tag 2",
        create: false,
      },
      {
        id: 3,
        label: "Tag 3",
        create: false,
      },
      {
        id: 4,
        label: "Tag 4",
        create: false,
      },
      {
        id: 5,
        label: "Tag 5",
        create: false,
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
    date: ToUnixTimeStamp("2024-07-02"),
    category: {
      id: 2,
      label: "Category 2",
      priority: {
        id: 1,
        name: "Priority 1",
        weight: 1,
      },
    },
    merchant: {
      id: 2,
      label: "Merchant 2",
      create: false,
    },
    account: {
      id: 2,
      accountNumber: "123456789",
      balance: 1000,
      isMainAccount: false,
      label: "",
    },
    tags: [
      {
        id: 1,
        label: "Tag 1",
        create: false,
      },
      {
        id: 2,
        label: "Tag 2",
        create: false,
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
    date: ToUnixTimeStamp("2024-01-03"),
    category: {
      id: 3,
      label: "Category 3",
      priority: {
        id: 1,
        name: "Priority 1",
        weight: 1,
      },
    },
    merchant: {
      id: 3,
      label: "Merchant 3",
      create: false,
    },
    account: {
      id: 3,
      accountNumber: "123456789",
      balance: 1000,
      isMainAccount: false,
      label: "",
    },
    tags: [
      {
        id: 1,
        label: "Tag 1",
        create: false,
      },
      {
        id: 2,
        label: "Tag 2",
        create: false,
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
    date: ToUnixTimeStamp("2024-06-04"),
    category: {
      id: 4,
      label: "Category 4",
      priority: {
        id: 1,
        name: "Priority 1",
        weight: 1,
      },
    },
    merchant: {
      id: 4,
      label: "Merchant 4",
      create: false,
    },
    account: {
      id: 4,
      accountNumber: "123456789",
      balance: 1000,
      isMainAccount: false,
      label: "",
    },
    tags: [
      {
        id: 1,
        label: "Tag 1",
        create: false,
      },
      {
        id: 2,
        label: "Tag 2",
        create: false,
      },
    ],
    type: TransactionType.CREDIT,
  },
];
