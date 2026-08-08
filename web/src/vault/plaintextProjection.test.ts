import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { Currency } from "../typings/enums/Currency";
import { plaintextProjection } from "./plaintextProjection";
import type {
  IAccountResponse,
  ICategoryResponse,
  ICreateTransactionRequest,
  IMerchantResponse,
  ITagResponse,
  ITransactionResponse,
} from "../clients/types";
import type { TransactionDraft, TransactionFilter } from "./VaultProjection";

vi.mock("axios");

const personalSpace = "personal";
const createdAt = "2026-08-01T00:00:00.000Z";
const everydayAccountNumber = "NL01XPNS0000000001";
const savingsAccountNumber = "NL01XPNS0000000002";

const accounts: IAccountResponse[] = [
  {
    accountNumber: everydayAccountNumber,
    label: "Everyday",
    balance: { minorUnits: 250000, currency: Currency.EUR },
    isDefault: true,
    createdAt,
    updatedAt: null,
  },
  {
    accountNumber: savingsAccountNumber,
    label: "Savings",
    balance: { minorUnits: 900000, currency: Currency.EUR },
    isDefault: false,
    createdAt,
    updatedAt: null,
  },
];

const categories: ICategoryResponse[] = [
  {
    id: 7,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 100, createdAt, updatedAt: null },
    createdAt,
    updatedAt: null,
  },
];

const merchants: IMerchantResponse[] = [
  { id: 3, label: "Albert", createdAt, updatedAt: null },
];

const tags: ITagResponse[] = [
  { id: 5, label: "Work", bgColorHex: "#102030", fgColorHex: "#ffffff", createdAt, updatedAt: null },
];

const buildTransaction = (
  overrides: Partial<ITransactionResponse> & { id: number },
): ITransactionResponse => ({
  kind: "expense",
  amount: { minorUnits: 1250, currency: Currency.EUR },
  sourceAccountNumber: everydayAccountNumber,
  destinationAccountNumber: null,
  categoryId: 7,
  merchant: { id: 3, label: "Albert" },
  tags: [{ id: 5, label: "Work" }],
  reason: null,
  occurredAt: "2026-08-05T10:00:00.000Z",
  createdAt,
  updatedAt: null,
  ...overrides,
});

const buildFilter = (overrides: Partial<TransactionFilter> = {}): TransactionFilter => ({
  space: personalSpace,
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
  ...overrides,
});

const buildDraft = (overrides: Partial<TransactionDraft> = {}): TransactionDraft => ({
  id: null,
  space: personalSpace,
  kind: "expense",
  amountMinorUnits: 4999,
  currency: Currency.EUR,
  occurredAt: "2026-08-06T09:30:00.000Z",
  accountId: everydayAccountNumber,
  categoryId: "7",
  merchantLabel: "Albert",
  tagLabels: ["Work"],
  reason: "Weekly shop",
  ...overrides,
});

interface TransactionPageStub {
  items: ITransactionResponse[];
  totalItems: number;
  totalPages: number;
}

interface RequestConfig {
  params?: { page?: number };
}

const stubTransactionPages = (pageFor: (page: number) => TransactionPageStub): void => {
  vi.mocked(axios.get).mockImplementation(((url: string, config?: RequestConfig) => {
    if (url === "/api/v1/accounts") return Promise.resolve({ data: accounts });
    if (url === "/api/v1/categories") return Promise.resolve({ data: categories });
    if (url === "/api/v1/merchants") return Promise.resolve({ data: merchants });
    if (url === "/api/v1/tags") return Promise.resolve({ data: tags });
    if (url === "/api/v1/transactions") {
      const page = config?.params?.page ?? 1;
      const stub = pageFor(page);
      return Promise.resolve({
        data: {
          items: stub.items,
          page,
          pageSize: 200,
          totalItems: stub.totalItems,
          totalPages: stub.totalPages,
        },
      });
    }
    return Promise.reject(new Error(`Unexpected request to ${url}`));
  }) as unknown as typeof axios.get);
};

const stubSinglePage = (items: ITransactionResponse[]): void =>
  stubTransactionPages(() => ({ items, totalItems: items.length, totalPages: 1 }));

const callsTo = (url: string): unknown[][] =>
  vi.mocked(axios.get).mock.calls.filter((call) => call[0] === url);

const requestedPages = (): number[] =>
  vi
    .mocked(axios.get)
    .mock.calls.filter((call) => call[0] === "/api/v1/transactions")
    .map((call) => (call[1] as RequestConfig | undefined)?.params?.page ?? 1);

describe("plaintextProjection", () => {
  beforeEach(() => {
    vi.mocked(axios.get).mockReset();
    vi.mocked(axios.post).mockReset();
  });

  it("asks each option endpoint exactly once and pages transactions until the last page", async () => {
    stubTransactionPages((page) => ({
      items: [buildTransaction({ id: page })],
      totalItems: 3,
      totalPages: 3,
    }));

    const projection = plaintextProjection();
    await projection.unlock();

    expect(callsTo("/api/v1/accounts")).toHaveLength(1);
    expect(callsTo("/api/v1/categories")).toHaveLength(1);
    expect(callsTo("/api/v1/merchants")).toHaveLength(1);
    expect(callsTo("/api/v1/tags")).toHaveLength(1);
    expect(requestedPages()).toEqual([1, 2, 3]);
    expect(projection.state).toBe("ready");
  });

  it("reports one personal space", async () => {
    stubSinglePage([]);

    const projection = plaintextProjection();
    await projection.unlock();

    expect(await projection.listSpaces()).toEqual([
      { id: personalSpace, name: "Personal", kind: "personal", canEdit: true },
    ]);
  });

  it("hands every identifier over as a string", async () => {
    stubSinglePage([buildTransaction({ id: 41 })]);

    const projection = plaintextProjection();
    await projection.unlock();

    const [category] = await projection.listTaxonomy(personalSpace, "category");
    const [merchant] = await projection.listTaxonomy(personalSpace, "merchant");
    const [tag] = await projection.listTaxonomy(personalSpace, "tag");
    const [account] = await projection.listAccounts(personalSpace);
    const { rows } = await projection.queryTransactions(buildFilter(), { offset: 0, limit: 50 });

    expect(category.id).toBe("7");
    expect(merchant.id).toBe("3");
    expect(tag.id).toBe("5");
    expect(account.id).toBe(everydayAccountNumber);
    expect(rows[0].id).toBe("41");
    expect(rows[0].categoryId).toBe("7");
    expect(rows[0].merchantId).toBe("3");
    expect(rows[0].tagIds).toEqual(["5"]);
  });

  it("carries a tag's stored colours over", async () => {
    stubSinglePage([]);

    const projection = plaintextProjection();
    await projection.unlock();

    const [tag] = await projection.listTaxonomy(personalSpace, "tag");

    expect(tag.foregroundHex).toBe("#ffffff");
    expect(tag.backgroundHex).toBe("#102030");
  });

  it("matches an account filter on the stringified account number", async () => {
    stubSinglePage([
      buildTransaction({ id: 1, sourceAccountNumber: everydayAccountNumber }),
      buildTransaction({ id: 2, sourceAccountNumber: savingsAccountNumber }),
    ]);

    const projection = plaintextProjection();
    await projection.unlock();

    const page = await projection.queryTransactions(
      buildFilter({ account: savingsAccountNumber }),
      { offset: 0, limit: 50 },
    );

    expect(page.rows.map((row) => row.id)).toEqual(["2"]);
  });

  it("puts a transfer's destination on the counterparty so either side matches", async () => {
    stubSinglePage([
      buildTransaction({
        id: 9,
        kind: "transfer",
        categoryId: null,
        merchant: null,
        destinationAccountNumber: savingsAccountNumber,
      }),
    ]);

    const projection = plaintextProjection();
    await projection.unlock();

    const { rows } = await projection.queryTransactions(buildFilter(), { offset: 0, limit: 50 });

    expect(rows[0].accountId).toBe(everydayAccountNumber);
    expect(rows[0].counterpartyAccountId).toBe(savingsAccountNumber);
  });

  it("puts an income's account on the credited side", async () => {
    stubSinglePage([
      buildTransaction({
        id: 11,
        kind: "income",
        sourceAccountNumber: null,
        destinationAccountNumber: everydayAccountNumber,
      }),
    ]);

    const projection = plaintextProjection();
    await projection.unlock();

    const { rows } = await projection.queryTransactions(buildFilter(), { offset: 0, limit: 50 });

    expect(rows[0].accountId).toBe(everydayAccountNumber);
    expect(rows[0].counterpartyAccountId).toBeNull();
  });

  it("stops at the row ceiling and says how much of the ledger it is showing", async () => {
    const fullPage = Array.from({ length: 200 }, (_unused, index) =>
      buildTransaction({ id: index + 1 }),
    );
    stubTransactionPages(() => ({ items: fullPage, totalItems: 4000, totalPages: 20 }));

    const projection = plaintextProjection();
    await projection.unlock();

    expect(requestedPages()).toHaveLength(10);
    expect(projection.rowCeiling).toEqual({
      loadedRowCount: 2000,
      availableRowCount: 4000,
      reachedCeiling: true,
    });
  });

  it("reports no ceiling when the whole ledger fits", async () => {
    stubSinglePage([buildTransaction({ id: 1 })]);

    const projection = plaintextProjection();
    await projection.unlock();

    expect(projection.rowCeiling).toEqual({
      loadedRowCount: 1,
      availableRowCount: 1,
      reachedCeiling: false,
    });
  });

  it("posts the mapped request body when a draft is saved", async () => {
    stubSinglePage([]);
    vi.mocked(axios.post).mockResolvedValue({
      data: buildTransaction({ id: 77, reason: "Weekly shop" }),
    });

    const projection = plaintextProjection();
    await projection.unlock();
    const saved = await projection.saveTransaction(buildDraft());

    const [url, body] = vi.mocked(axios.post).mock.calls[0];
    expect(url).toBe("/api/v1/transactions");
    expect(body as ICreateTransactionRequest).toEqual({
      amount: { minorUnits: 4999, currency: Currency.EUR },
      sourceAccountNumber: everydayAccountNumber,
      destinationAccountNumber: null,
      categoryId: 7,
      merchant: { id: 3, label: "Albert", create: false },
      tags: [{ id: 5, label: "Work", create: false }],
      reason: "Weekly shop",
      occurredAt: "2026-08-06T09:30:00.000Z",
    });
    expect(saved.id).toBe("77");
  });

  it("asks the server to create a merchant or tag it has never seen", async () => {
    stubSinglePage([]);
    vi.mocked(axios.post).mockResolvedValue({ data: buildTransaction({ id: 78 }) });

    const projection = plaintextProjection();
    await projection.unlock();
    await projection.saveTransaction(
      buildDraft({ merchantLabel: "Jumbo", tagLabels: ["Holiday"] }),
    );

    const body = vi.mocked(axios.post).mock.calls[0][1] as ICreateTransactionRequest;
    expect(body.merchant).toEqual({ id: null, label: "Jumbo", create: true });
    expect(body.tags).toEqual([{ id: null, label: "Holiday", create: true }]);
  });

  it("credits the account when an income draft is saved", async () => {
    stubSinglePage([]);
    vi.mocked(axios.post).mockResolvedValue({ data: buildTransaction({ id: 79 }) });

    const projection = plaintextProjection();
    await projection.unlock();
    await projection.saveTransaction(buildDraft({ kind: "income" }));

    const body = vi.mocked(axios.post).mock.calls[0][1] as ICreateTransactionRequest;
    expect(body.sourceAccountNumber).toBeNull();
    expect(body.destinationAccountNumber).toBe(everydayAccountNumber);
  });

  it("refetches after a save so the saved row is queryable", async () => {
    const created = buildTransaction({ id: 77 });
    let ledger: ITransactionResponse[] = [];
    stubTransactionPages(() => ({
      items: ledger,
      totalItems: ledger.length,
      totalPages: ledger.length === 0 ? 0 : 1,
    }));
    vi.mocked(axios.post).mockImplementation((() => {
      ledger = [created];
      return Promise.resolve({ data: created });
    }) as unknown as typeof axios.post);

    const projection = plaintextProjection();
    await projection.unlock();
    await projection.saveTransaction(buildDraft());

    const { rows } = await projection.queryTransactions(buildFilter(), { offset: 0, limit: 50 });
    expect(rows.map((row) => row.id)).toEqual(["77"]);
  });

  it("refuses a draft carrying an identifier because there is no update endpoint", async () => {
    stubSinglePage([]);

    const projection = plaintextProjection();
    await projection.unlock();

    await expect(projection.saveTransaction(buildDraft({ id: "77" }))).rejects.toThrow(
      "Editing a transaction is not supported yet",
    );
    expect(vi.mocked(axios.post)).not.toHaveBeenCalled();
  });

  it("refuses a transfer draft because it cannot name the second account", async () => {
    stubSinglePage([]);

    const projection = plaintextProjection();
    await projection.unlock();

    await expect(projection.saveTransaction(buildDraft({ kind: "transfer" }))).rejects.toThrow(
      "Saving a transfer is not supported yet",
    );
    expect(vi.mocked(axios.post)).not.toHaveBeenCalled();
  });

  it("starts locked and rejects every read before it is unlocked", async () => {
    stubSinglePage([]);

    const projection = plaintextProjection();

    expect(projection.state).toBe("locked");
    await expect(projection.listSpaces()).rejects.toThrow("The vault is locked");
    expect(vi.mocked(axios.get)).not.toHaveBeenCalled();
  });

  it("drops the cached rows and the ceiling report on lock", async () => {
    stubSinglePage([buildTransaction({ id: 1 })]);

    const projection = plaintextProjection();
    await projection.unlock();
    const states: string[] = [];
    projection.subscribe((state) => states.push(state));
    projection.lock();

    expect(projection.state).toBe("locked");
    expect(projection.rowCeiling).toBeNull();
    expect(states).toEqual(["locked"]);
    await expect(
      projection.queryTransactions(buildFilter(), { offset: 0, limit: 50 }),
    ).rejects.toThrow("The vault is locked");
  });

  it("moves to the error state when the fetch fails", async () => {
    vi.mocked(axios.get).mockRejectedValue(new Error("Network down"));

    const projection = plaintextProjection();

    await expect(projection.unlock()).rejects.toThrow("Network down");
    expect(projection.state).toBe("error");
  });
});
