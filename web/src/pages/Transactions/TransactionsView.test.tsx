import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Currency } from "../../typings/enums/Currency";
import { fixtureProjection } from "../../vault/fixtureProjection";
import type { FixtureSeed } from "../../vault/fixtureProjection";
import type { TransactionFilter, TransactionView } from "../../vault/VaultProjection";
import { VaultProvider } from "../../vault/VaultProvider";
import TransactionsView from "./TransactionsView";

const filter: TransactionFilter = {
  space: "personal",
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
};

const transaction = (id: number): TransactionView => ({
  id: String(id),
  kind: "expense",
  amountMinorUnits: 1250,
  currency: Currency.EUR,
  occurredAt: "2026-08-08T12:00:00.000Z",
  accountId: "everyday",
  counterpartyAccountId: null,
  isCounterpartyPrivate: false,
  categoryId: "food",
  merchantId: "bakery",
  tagIds: ["family"],
  canEdit: true,
});

const seed = (transactions: TransactionView[]): FixtureSeed => ({
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [{ id: "everyday", label: "Everyday", currency: Currency.EUR, canEdit: true }],
  },
  taxonomy: {
    personal: [
      {
        id: "food",
        kind: "category",
        label: "Food",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "bakery",
        kind: "merchant",
        label: "Bakery",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "family",
        kind: "tag",
        label: "Family",
        foregroundHex: null,
        backgroundHex: null,
      },
    ],
  },
  transactions: { personal: transactions },
});

const setWidth = (width: number) => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: query === "(min-width: 768px)" ? width >= 768 : width >= 1024,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const renderView = (
  transactions: TransactionView[],
  options: {
    activeFilterCount?: number;
    refreshKey?: number;
    projection?: ReturnType<typeof fixtureProjection>;
    onEditTransaction?: (transaction: TransactionView) => void;
    onTransactionChanged?: () => void;
  } = {},
) => {
  const projection = options.projection ?? fixtureProjection(seed(transactions));
  const view = render(
    <VaultProvider projection={projection}>
      <TransactionsView
        filter={filter}
        activeFilterCount={options.activeFilterCount ?? 0}
        refreshKey={options.refreshKey}
        onAddTransaction={vi.fn()}
        onClearFilters={vi.fn()}
        onEditTransaction={options.onEditTransaction}
        onTransactionChanged={options.onTransactionChanged}
      />
    </VaultProvider>,
  );
  return { projection, ...view };
};

describe("TransactionsView", () => {
  it("renders the semantic columns in order on desktop", async () => {
    setWidth(1440);
    renderView([transaction(1)]);

    const table = await screen.findByRole("table");
    expect(within(table).getAllByRole("columnheader").map((header) => header.textContent)).toEqual([
      "Amount",
      "Date",
      "Category",
      "Merchant",
      "Account",
      "Tags",
    ]);
    expect(screen.queryByRole("list", { name: "Transactions" })).toBeNull();
  });

  it("renders cards instead of a table on mobile", async () => {
    setWidth(390);
    renderView([transaction(1)]);

    expect(await screen.findByRole("list", { name: "Transactions" })).toBeDefined();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("keeps the mobile action menu visible, touch-sized, and usable", async () => {
    setWidth(390);
    const onEditTransaction = vi.fn();
    renderView([transaction(1)], { onEditTransaction });

    const actions = await screen.findByRole("button", { name: "Actions for transaction 1" });
    expect(getComputedStyle(actions).opacity).toBe("1");
    expect(Number.parseFloat(getComputedStyle(actions).minWidth)).toBeGreaterThanOrEqual(44);
    expect(Number.parseFloat(getComputedStyle(actions).minHeight)).toBeGreaterThanOrEqual(44);
    fireEvent.click(actions);
    fireEvent.click(await screen.findByRole("menuitem", { name: "Edit" }));

    expect(onEditTransaction).toHaveBeenCalledWith(transaction(1));
  });

  it("uses Fluent's 44px medium row sizing", async () => {
    setWidth(1440);
    renderView([transaction(1)]);

    const cell = (await screen.findAllByRole("gridcell"))[0];
    expect(getComputedStyle(cell).height).toBe("44px");
  });

  it("distinguishes an empty ledger from an empty filtered result", async () => {
    setWidth(1440);
    const first = renderView([]);
    expect(await screen.findByText("Add your first transaction to start your ledger.")).toBeDefined();
    expect(screen.getByRole("button", { name: "Add transaction" })).toBeDefined();
    first.unmount();

    renderView([], { activeFilterCount: 1 });
    expect(await screen.findByText("No transactions match these filters.")).toBeDefined();
    expect(screen.getByRole("button", { name: "Clear filters" })).toBeDefined();
  });

  it("replaces rows with a locked-state message while locked", async () => {
    setWidth(1440);
    const { projection } = renderView([transaction(1)]);
    await screen.findByRole("table");

    act(() => projection.lock());

    expect(await screen.findByText("The vault is locked.")).toBeDefined();
    expect(screen.queryByRole("button", { name: "Unlock" })).toBeNull();
    expect(screen.queryByRole("row")).toBeNull();
  });

  it("keeps cached rows when a later refresh fails", async () => {
    setWidth(1440);
    const projection = fixtureProjection(seed([transaction(1)]));
    const originalQuery = projection.queryTransactions.bind(projection);
    const queryTransactions = vi
      .spyOn(projection, "queryTransactions")
      .mockImplementationOnce(originalQuery)
      .mockRejectedValueOnce(new Error("offline"));
    const first = renderView([], { projection, refreshKey: 1 });
    await screen.findByRole("table");

    first.rerender(
      <VaultProvider projection={projection}>
        <TransactionsView
          filter={filter}
          activeFilterCount={0}
          refreshKey={2}
          onAddTransaction={vi.fn()}
          onClearFilters={vi.fn()}
        />
      </VaultProvider>,
    );

    expect(await screen.findByRole("status")).toHaveTextContent("could not be refreshed");
    expect(screen.getByRole("table")).toBeDefined();
    expect(queryTransactions).toHaveBeenCalledTimes(2);
  });

  it("bounds rendered rows for a ledger above 200 entries", async () => {
    setWidth(1440);
    renderView(Array.from({ length: 220 }, (_value, index) => transaction(index + 1)));

    expect(await screen.findByLabelText("Transaction total")).toHaveTextContent("220");
    expect(screen.getAllByRole("row").length).toBeLessThan(221);
  });

  it("does not reveal an inaccessible transfer counterparty", async () => {
    setWidth(1440);
    renderView([
      {
        ...transaction(1),
        kind: "transfer",
        counterpartyAccountId: "private-account",
        isCounterpartyPrivate: true,
      },
    ]);

    expect(await screen.findByText("Private account")).toBeDefined();
    expect(screen.queryByText("private-account")).toBeNull();
  });

  it("opens editable rows by click, Enter, and the action menu", async () => {
    setWidth(1440);
    const onEditTransaction = vi.fn();
    renderView([transaction(1)], { onEditTransaction });

    const row = (await screen.findAllByRole("row"))[1];
    fireEvent.click(row);
    fireEvent.keyDown(row, { key: "Enter" });
    fireEvent.click(screen.getByRole("button", { name: "Actions for transaction 1" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Edit" }));

    expect(onEditTransaction).toHaveBeenCalledTimes(3);
    expect(onEditTransaction).toHaveBeenLastCalledWith(transaction(1));
  });

  it("has no row action for a transaction the viewer cannot edit", async () => {
    setWidth(1440);
    const onEditTransaction = vi.fn();
    renderView([{ ...transaction(1), canEdit: false }], { onEditTransaction });

    const row = (await screen.findAllByRole("row"))[1];
    fireEvent.click(row);
    fireEvent.keyDown(row, { key: " " });

    expect(screen.queryByRole("button", { name: "Actions for transaction 1" })).toBeNull();
    expect(onEditTransaction).not.toHaveBeenCalled();
  });

  it("confirms deletion and refreshes the transaction collection", async () => {
    setWidth(1440);
    const projection = fixtureProjection(seed([transaction(1)]));
    const onTransactionChanged = vi.fn();
    renderView([transaction(1)], { projection, onEditTransaction: vi.fn(), onTransactionChanged });

    fireEvent.click(await screen.findByRole("button", { name: "Actions for transaction 1" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Delete" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete transaction" }));

    await waitFor(() => expect(onTransactionChanged).toHaveBeenCalledTimes(1));
    expect(screen.queryByText("12.50 EUR")).toBeNull();
  });
});
