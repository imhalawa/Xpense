import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { Currency } from "../../typings/enums/Currency";
import type { AccountView, TransactionFilter } from "../../vault/VaultProjection";
import TransactionsToolbar from "./TransactionsToolbar";

const accounts: AccountView[] = [
  { id: "everyday", label: "Everyday", currency: Currency.EUR, canEdit: true },
  { id: "savings", label: "Savings", currency: Currency.EUR, canEdit: true },
];

const emptyFilter: TransactionFilter = {
  space: "personal",
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
};

const renderToolbar = (
  filter: TransactionFilter = emptyFilter,
  onFilterChange = vi.fn(),
  onClearFilters = vi.fn(),
) => {
  render(
    <TransactionsToolbar
      filter={filter}
      accounts={accounts}
      onFilterChange={onFilterChange}
      onClearFilters={onClearFilters}
    />,
  );
  return { onFilterChange, onClearFilters };
};

describe("TransactionsToolbar", () => {
  it("labels the toolbar and reveals both date fields from the range summary", async () => {
    renderToolbar({ ...emptyFilter, from: "2026-08-01", to: "2026-08-31" });

    expect(screen.getByRole("toolbar", { name: "Transaction filters" })).toBeDefined();
    fireEvent.click(screen.getByRole("button", { name: "2026-08-01 – 2026-08-31" }));

    expect(await screen.findByLabelText("From")).toBeDefined();
    expect(screen.getByLabelText("To")).toBeDefined();
  });

  it("lists accounts and changes only the account facet", async () => {
    const { onFilterChange } = renderToolbar({
      ...emptyFilter,
      category: "food",
      from: "2026-08-01",
    });

    fireEvent.click(screen.getByRole("combobox", { name: "Account" }));
    fireEvent.click(await screen.findByRole("option", { name: "Savings" }));

    expect(onFilterChange).toHaveBeenCalledWith({
      ...emptyFilter,
      category: "food",
      from: "2026-08-01",
      account: "savings",
    });
  });

  it("counts a date range once alongside taxonomy filters", () => {
    renderToolbar({
      ...emptyFilter,
      category: "food",
      from: "2026-08-01",
      to: "2026-08-31",
    });

    expect(screen.getByText("2", { selector: ".fui-Badge" })).toBeDefined();
  });

  it("disables clearing at zero and clears every facet except space", async () => {
    const { rerender } = render(
      <TransactionsToolbar
        filter={emptyFilter}
        accounts={accounts}
        onFilterChange={vi.fn()}
        onClearFilters={vi.fn()}
      />,
    );

    expect(screen.getByRole("button", { name: "Clear filters" })).toBeDisabled();

    const onClearFilters = vi.fn();
    rerender(
      <TransactionsToolbar
        filter={{ ...emptyFilter, tag: "family", account: "everyday" }}
        accounts={accounts}
        onFilterChange={vi.fn()}
        onClearFilters={onClearFilters}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));

    await waitFor(() => expect(onClearFilters).toHaveBeenCalledTimes(1));
  });
});
