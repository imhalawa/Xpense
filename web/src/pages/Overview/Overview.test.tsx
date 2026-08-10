import { render, screen } from "@testing-library/react";
import axios from "axios";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { Currency } from "../../typings/enums/Currency";
import { fixtureProjection } from "../../vault/fixtureProjection";
import type { FixtureSeed } from "../../vault/fixtureProjection";
import { plaintextProjection } from "../../vault/plaintextProjection";
import { VaultProvider } from "../../vault/VaultProvider";
import Overview from "./Overview";

vi.mock("../Transactions/TransactionsView", () => ({ default: () => <div>Transactions from vault</div> }));
vi.mock("axios");

const seed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [
      {
        id: "cash",
        label: "Cash",
        currency: Currency.EUR,
        balanceSource: "opening",
        openingBalanceMinorUnits: 1000,
        canEdit: true,
      },
      {
        id: "savings",
        label: "Savings",
        currency: Currency.EUR,
        balanceSource: "opening",
        openingBalanceMinorUnits: 500,
        canEdit: true,
      },
      {
        id: "dollars",
        label: "Dollars",
        currency: Currency.USD,
        balanceSource: "opening",
        openingBalanceMinorUnits: 2000,
        canEdit: true,
      },
    ],
  },
  taxonomy: {
    personal: [{ id: "1", kind: "category", label: "Food", foregroundHex: null, backgroundHex: null }],
  },
  transactions: {
    personal: [
      {
        id: "groceries",
        kind: "expense",
        amountMinorUnits: 200,
        currency: Currency.EUR,
        occurredAt: new Date().toISOString(),
        accountId: "cash",
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: "1",
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
      {
        id: "salary",
        kind: "income",
        amountMinorUnits: 100,
        currency: Currency.EUR,
        occurredAt: new Date().toISOString(),
        accountId: "cash",
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: null,
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
      {
        id: "move",
        kind: "transfer",
        amountMinorUnits: 300,
        currency: Currency.EUR,
        occurredAt: new Date().toISOString(),
        accountId: "cash",
        counterpartyAccountId: "savings",
        isCounterpartyPrivate: false,
        categoryId: null,
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
      {
        id: "usd-expense",
        kind: "expense",
        amountMinorUnits: 250,
        currency: Currency.USD,
        occurredAt: new Date().toISOString(),
        accountId: "dollars",
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: "1",
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
    ],
  },
  transactionHistoryComplete: { personal: true },
  budgets: {
    personal: [{
      id: "budget-1",
      category: { id: "1", label: "Food" },
      amount: { minorUnits: 1000, currency: Currency.EUR },
      recurrence: "Monthly",
      startsOn: new Date().toISOString().slice(0, 10),
      endsOn: null,
      alertThresholdPercent: null,
      period: null,
      createdAt: "",
      updatedAt: null,
      canEdit: true,
    }],
  },
};

describe("Overview", () => {
  it("renders balances and budget spending from the unlocked projection", async () => {
    render(
      <MemoryRouter>
        <VaultProvider projection={fixtureProjection(seed)}>
          <Overview />
        </VaultProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText("€14.00")).toBeDefined();
    expect(screen.getByText(/17[.,]50/)).toBeDefined();
    expect(screen.queryByText(/9[.,]999[.,]00/)).toBeNull();
    expect(screen.getByText("Food")).toBeDefined();
    expect(screen.getByText(/2[.,]00.*spent/i)).toBeDefined();
  });

  it("displays current API balances once through the plaintext projection", async () => {
    const createdAt = "2026-08-01T00:00:00.000Z";
    vi.mocked(axios.get).mockImplementation((async (url: string) => {
      if (url === "/api/v1/accounts") {
        return { data: [
          {
            accountNumber: "everyday",
            label: "Everyday",
            balance: { minorUnits: 800, currency: Currency.EUR },
            isDefault: true,
            createdAt,
            updatedAt: null,
          },
        ] };
      }
      if (url === "/api/v1/transactions") {
        return { data: {
          items: [
            {
              id: 1,
              kind: "expense",
              amount: { minorUnits: 200, currency: Currency.EUR },
              sourceAccountNumber: "everyday",
              destinationAccountNumber: null,
              categoryId: null,
              merchant: null,
              tags: [],
              reason: null,
              occurredAt: "2026-08-02T00:00:00.000Z",
              createdAt,
              updatedAt: null,
            },
            {
              id: 2,
              kind: "income",
              amount: { minorUnits: 100, currency: Currency.EUR },
              sourceAccountNumber: null,
              destinationAccountNumber: "everyday",
              categoryId: null,
              merchant: null,
              tags: [],
              reason: null,
              occurredAt: "2026-08-03T00:00:00.000Z",
              createdAt,
              updatedAt: null,
            },
            {
              id: 3,
              kind: "transfer",
              amount: { minorUnits: 100, currency: Currency.EUR },
              sourceAccountNumber: "everyday",
              destinationAccountNumber: "private-account",
              categoryId: null,
              merchant: null,
              tags: [],
              reason: null,
              occurredAt: "2026-08-04T00:00:00.000Z",
              createdAt,
              updatedAt: null,
            },
          ],
          page: 1,
          pageSize: 200,
          totalItems: 3,
          totalPages: 1,
        } };
      }
      if (url === "/api/v1/budgets") return { data: [] };
      if (["/api/v1/categories", "/api/v1/merchants", "/api/v1/tags", "/api/v1/priorities"].includes(url)) {
        return { data: [] };
      }
      throw new Error(`Unexpected request to ${url}`);
    }) as typeof axios.get);

    const projection = plaintextProjection();
    await projection.unlock();

    render(
      <MemoryRouter>
        <VaultProvider projection={projection}>
          <Overview />
        </VaultProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText("€8.00")).toBeDefined();
    expect(screen.queryByText("€6.00")).toBeNull();
  });
});
