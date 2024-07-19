import { ICategory, IPriority, IAccount, ITag, IMerchant, ITransaction, TransactionType, Currency } from "./typings";

export const createCategoriesFixture = (): ICategory[] => {
  const priorities = createPriorityFixture();
  return [
    { id: 1, label: "Groceries", priority: priorities[0], createdOn: 0, lastUpdated: 0 },
    { id: 2, label: "Online Shopping", priority: priorities[1], createdOn: 0, lastUpdated: 0 },
    { id: 3, label: "Luxury", priority: priorities[2], createdOn: 0, lastUpdated: 0 },
    { id: 4, label: "Restaurants", priority: priorities[1], createdOn: 0, lastUpdated: 0 },
    { id: 5, label: "Cafe", priority: priorities[3], createdOn: 0, lastUpdated: 0 },
  ];
};

export const createPriorityFixture = (): IPriority[] => [
  { id: 1, label: "High", weight: 1, createdOn: 0, lastUpdated: 0 },
  { id: 2, label: "Medium", weight: 2, createdOn: 0, lastUpdated: 0 },
  { id: 3, label: "Low", weight: 3, createdOn: 0, lastUpdated: 0 },
  { id: 6, label: "Extreme", weight: 1, createdOn: 0, lastUpdated: 0 },
];

export const createAccountFixture = (): IAccount[] => [
  {
    id: 1,
    accountNumber: "1000000001",
    balance: 1000,
    isDefault: true,
    label: "Main Account",
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 2,
    accountNumber: "1000000002",
    balance: 1000,
    isDefault: false,
    label: "Saving Account",
    createdOn: 0,
    lastUpdated: 0,
  },
];

export const createTagFixture = (): ITag[] => [
  {
    id: 1,
    label: "Tag 1",
    create: false,
    bgColorHex: "#000000",
    fgColorHex: "#ffffff",
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 2,
    label: "Tag 2",
    create: false,
    bgColorHex: "#000000",
    fgColorHex: "#ffffff",
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 3,
    label: "Tag 3",
    create: false,
    bgColorHex: "#000000",
    fgColorHex: "#ffffff",
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 4,
    label: "Tag 4",
    create: false,
    bgColorHex: "#000000",
    fgColorHex: "#ffffff",
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 5,
    label: "Tag 5",
    create: false,
    bgColorHex: "#000000",
    fgColorHex: "#ffffff",
    createdOn: 0,
    lastUpdated: 0,
  },
];

export const createMerchantFixture = (): IMerchant[] => [
  {
    id: 3,
    label: "Merchant 1",
    create: false,
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 2,
    label: "Merchant 2",
    create: false,
    createdOn: 0,
    lastUpdated: 0,
  },
  {
    id: 3,
    label: "Merchant 3",
    create: false,
    createdOn: 0,
    lastUpdated: 0,
  },
];

export const createTransactionFixture = (): ITransaction[] => [
  {
    id: 1,
    createdOn: 0,
    lastUpdated: null,
    amount: {
      currency: Currency.DOLLAR,
      cents: 30,
    },
    category: {
      id: 1,
      label: "Category 1",
      createdOn: 0,
      lastUpdated: 0,
      priority: {
        id: 1,
        label: "Priority 1",
        weight: 1,
        createdOn: 0,
        lastUpdated: 0,
      },
    },
    merchant: {
      id: 1,
      label: "Merchant 1",
      create: false,
      createdOn: 0,
      lastUpdated: 0,
    },
    account: {
      id: 1,
      accountNumber: "123456789",
      balance: 1000,
      isDefault: false,
      label: "Main Account",
      createdOn: 0,
      lastUpdated: 0,
    },
    tags: [
      {
        id: 1,
        label: "Tag 1",
        create: false,
        bgColorHex: "#000000",
        fgColorHex: "#ffffff",
        createdOn: 0,
        lastUpdated: 0,
      },
      {
        id: 2,
        label: "Tag 2",
        create: false,
        bgColorHex: "#000000",
        fgColorHex: "#ffffff",
        createdOn: 0,
        lastUpdated: null,
      },
      {
        id: 3,
        label: "Tag 3",
        create: false,
        bgColorHex: "#000000",
        fgColorHex: "#ffffff",
        createdOn: 0,
        lastUpdated: null,
      },
      {
        id: 4,
        label: "Tag 4",
        create: false,
        bgColorHex: "#000000",
        fgColorHex: "#ffffff",
        createdOn: 0,
        lastUpdated: null,
      },
      {
        id: 5,
        label: "Tag 5",
        create: false,
        bgColorHex: "#000000",
        fgColorHex: "#ffffff",
        createdOn: 0,
        lastUpdated: null,
      },
    ],
    type: TransactionType.CREDIT,
  },
];
