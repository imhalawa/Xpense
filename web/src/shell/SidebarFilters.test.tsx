import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { Currency } from "../typings/enums/Currency";
import type { AccountView, TaxonomyKind, TaxonomyValue, TransactionFilter } from "../vault/VaultProjection";
import SidebarFilters from "./SidebarFilters";

const filter: TransactionFilter = { space: "personal", category: null, merchant: null, tag: null, account: null, from: null, to: null };
const category = (id: string, label: string): TaxonomyValue => ({ id, kind: "category", label, foregroundHex: null, backgroundHex: null });
const account: AccountView = { id: "account-1", label: "Everyday", currency: Currency.EUR, canEdit: true };
const values = [category("category-2", "Second"), category("category-1", "First"), { id: "tag-1", kind: "tag" as TaxonomyKind, label: "Family", foregroundHex: "#242424", backgroundHex: "#EDEDED" }, { id: "merchant-1", kind: "merchant" as TaxonomyKind, label: "Bakery", foregroundHex: null, backgroundHex: null }];

const setCoarsePointer = (matches: boolean) => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: query === "(hover: none), (pointer: coarse)" && matches,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const renderFilters = (canEdit = true) => {
  const onToggleTaxonomy = vi.fn();
  const onToggleAccount = vi.fn();
  const onResourceAction = vi.fn();
  const view = render(<MemoryRouter><SidebarFilters accounts={[account]} values={values} filter={filter} canEdit={canEdit} onToggleTaxonomy={onToggleTaxonomy} onToggleAccount={onToggleAccount} onResourceAction={onResourceAction} /></MemoryRouter>);
  return { onToggleTaxonomy, onToggleAccount, onResourceAction, ...view };
};

describe("SidebarFilters", () => {
  beforeEach(() => {
    window.localStorage.clear();
    setCoarsePointer(false);
  });

  it("places expandable Accounts before the stable server taxonomy order", () => {
    renderFilters();
    expect(screen.getByRole("button", { name: "Accounts" }).getAttribute("aria-expanded")).toBe("true");
    fireEvent.click(screen.getByRole("button", { name: "Categories" }));
    const labels = within(screen.getByRole("region", { name: "Categories" })).getAllByRole("link").map((link) => link.textContent);
    expect(labels).toEqual(["Second", "First"]);
  });

  it("keeps the configured order after selecting a filter", () => {
    const { onToggleTaxonomy } = renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "Categories" }));
    fireEvent.click(screen.getByRole("link", { name: "Second" }));
    expect(onToggleTaxonomy).toHaveBeenCalledWith("category", "category-2");
    expect(within(screen.getByRole("region", { name: "Categories" })).getAllByRole("link").map((link) => link.textContent)).toEqual(["Second", "First"]);
  });

  it("uses the mounted overflow trigger after mouse and right-click actions", () => {
    const { onResourceAction } = renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "Categories" }));
    const row = screen.getByRole("link", { name: "Second" }).parentElement!;
    fireEvent.mouseEnter(row);
    const overflow = screen.getByRole("button", { name: "Actions for Second" });

    fireEvent.click(overflow);
    fireEvent.click(screen.getByRole("menuitem", { name: "Edit" }));
    expect(onResourceAction).toHaveBeenLastCalledWith(
      "edit",
      expect.objectContaining({ kind: "category" }),
      overflow,
    );

    fireEvent.contextMenu(row);
    fireEvent.click(screen.getByRole("menuitem", { name: "Edit" }));
    expect(onResourceAction).toHaveBeenLastCalledWith(
      "edit",
      expect.objectContaining({ kind: "category" }),
      overflow,
    );
  });

  it("uses a mounted section trigger after Shift+F10 delete", () => {
    const { onResourceAction } = renderFilters();
    const section = screen.getByRole("button", { name: "Accounts" });
    const row = screen.getByRole("link", { name: "Everyday" }).parentElement!;

    fireEvent.keyDown(row, { key: "F10", shiftKey: true });
    fireEvent.click(screen.getByRole("menuitem", { name: "Delete" }));
    expect(onResourceAction).toHaveBeenCalledWith(
      "delete",
      expect.objectContaining({ kind: "account" }),
      section,
    );
  });

  it("hides overflow until row hover or focus and keeps it visible for coarse pointers", () => {
    const desktop = renderFilters();
    const row = screen.getByRole("link", { name: "Everyday" }).parentElement!;
    const overflow = screen.getByLabelText("Actions for Everyday");
    expect(getComputedStyle(overflow).visibility).toBe("hidden");

    fireEvent.mouseEnter(row);
    expect(getComputedStyle(overflow).visibility).toBe("visible");
    fireEvent.mouseLeave(row);
    fireEvent.focus(screen.getByRole("link", { name: "Everyday" }));
    expect(getComputedStyle(overflow).visibility).toBe("visible");

    desktop.unmount();
    setCoarsePointer(true);
    renderFilters();
    expect(getComputedStyle(screen.getByRole("button", { name: "Actions for Everyday" })).visibility).toBe("visible");
  });

  it("provides section create actions and hides all mutating controls for viewers", () => {
    const { onResourceAction, unmount } = renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "Add account" }));
    expect(onResourceAction).toHaveBeenCalledWith("create", expect.objectContaining({ kind: "account" }), expect.any(HTMLElement));

    unmount();
    renderFilters(false);
    expect(screen.queryByRole("button", { name: "Add account" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Actions for Everyday" })).toBeNull();
  });
});
