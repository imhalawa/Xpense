import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router";
import { FluentProvider } from "@fluentui/react-components";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import { darkTheme, lightTheme } from "../fluent/theme";
import TransactionsToolbar from "../pages/Transactions/TransactionsToolbar";
import TransactionsView from "../pages/Transactions/TransactionsView";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
import type { TransactionFilter } from "../vault/VaultProjection";
import { VaultProvider } from "../vault/VaultProvider";
import AppShell from "./AppShell";

vi.mock("../clients/notifications", () => ({
  listNotifications: vi.fn().mockResolvedValue({
    notifications: [],
    page: 1,
    pageSize: 10,
    totalItems: 0,
    totalPages: 1,
    unreadItems: 0,
  }),
  getUnreadCount: vi.fn().mockResolvedValue(0),
  markNotificationRead: vi.fn().mockResolvedValue(undefined),
  markAllNotificationsRead: vi.fn().mockResolvedValue(undefined),
}));

vi.mock("../pages/Transactions/TransactionsForm/TransactionsForm", () => ({
  default: () => <button type="button">Cancel</button>,
}));

const account = {
  id: "everyday",
  label: "Everyday",
  currency: Currency.EUR,
  canEdit: true,
};

const seed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: { personal: [account] },
  taxonomy: { personal: [] },
  transactions: {
    personal: [
      {
        id: "transaction-1",
        kind: "expense",
        amountMinorUnits: 1250,
        currency: Currency.EUR,
        occurredAt: "2026-08-08T12:00:00.000Z",
        accountId: account.id,
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: null,
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
    ],
  },
};

const filter: TransactionFilter = {
  space: "personal",
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
};

const setWidth = (width: number) => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches:
      query === "(min-width: 1024px)"
        ? width >= 1024
        : query === "(min-width: 768px)"
          ? width >= 768
          : false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const renderResponsiveShell = (width: number, mode: "light" | "dark") => {
  setWidth(width);
  const projection = fixtureProjection(seed);
  return render(
    <FluentProvider
      data-theme={mode}
      theme={mode === "dark" ? darkTheme : lightTheme}>
      <MemoryRouter initialEntries={["/transactions?space=personal"]}>
        <VaultProvider projection={projection}>
          <LoadingContextProvider>
            <AppShell>
              <TransactionsToolbar
                filter={filter}
                accounts={[account]}
                onFilterChange={vi.fn()}
                onClearFilters={vi.fn()}
              />
              <TransactionsView
                filter={filter}
                activeFilterCount={0}
                onAddTransaction={vi.fn()}
                onClearFilters={vi.fn()}
              />
            </AppShell>
          </LoadingContextProvider>
        </VaultProvider>
      </MemoryRouter>
    </FluentProvider>,
  );
};

interface Structure {
  hasNavigationButton: boolean;
  headingPosition: string;
  transactionPresentation: "cards" | "table";
  toolbarOverflow: string;
}

const readStructure = (): Structure => ({
  hasNavigationButton: screen.queryByRole("button", { name: "Open navigation" }) !== null,
  headingPosition: getComputedStyle(screen.getByRole("heading", { level: 1 })).position,
  transactionPresentation:
    screen.queryByRole("table") === null ? "cards" : "table",
  toolbarOverflow: getComputedStyle(
    screen.getByRole("toolbar", { name: "Transaction filters" }),
  ).overflowX,
});

describe("responsive shell", () => {
  it.each(["light", "dark"] as const)("selects the expected branches in %s mode", async (mode) => {
    const expected = new Map<number, Structure>([
      [
        390,
        {
          hasNavigationButton: true,
          headingPosition: "",
          transactionPresentation: "cards",
          toolbarOverflow: "auto",
        },
      ],
      [
        768,
        {
          hasNavigationButton: true,
          headingPosition: "",
          transactionPresentation: "table",
          toolbarOverflow: "auto",
        },
      ],
      [
        1024,
        {
          hasNavigationButton: false,
          headingPosition: "absolute",
          transactionPresentation: "table",
          toolbarOverflow: "auto",
        },
      ],
      [
        1440,
        {
          hasNavigationButton: false,
          headingPosition: "absolute",
          transactionPresentation: "table",
          toolbarOverflow: "auto",
        },
      ],
    ]);

    for (const [width, expectedStructure] of expected) {
      const view = renderResponsiveShell(width, mode);
      await screen.findByLabelText("Transaction total");
      expect(readStructure()).toEqual(expectedStructure);
      expect(document.querySelector(`[data-theme="${mode}"]`)).not.toBeNull();
      view.unmount();
    }
  });

  it("changes only theme attributes, not layout structure", async () => {
    const light = renderResponsiveShell(1024, "light");
    await waitFor(() => expect(screen.queryByRole("table")).not.toBeNull());
    const lightStructure = readStructure();
    light.unmount();

    const dark = renderResponsiveShell(1024, "dark");
    await waitFor(() => expect(screen.queryByRole("table")).not.toBeNull());
    expect(readStructure()).toEqual(lightStructure);
    dark.unmount();
  });
});
