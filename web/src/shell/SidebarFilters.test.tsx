import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import type { TaxonomyKind, TaxonomyValue, TransactionFilter } from "../vault/VaultProjection";
import SidebarFilters from "./SidebarFilters";

const filter: TransactionFilter = {
  space: "personal",
  category: "category-1",
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
};

const value = (kind: TaxonomyKind, id: string, label: string): TaxonomyValue => ({
  id,
  kind,
  label,
  foregroundHex: kind === "tag" ? "#111111" : null,
  backgroundHex: kind === "tag" ? "#ffffff" : null,
});

const values: TaxonomyValue[] = [
  ...Array.from({ length: 9 }, (_entry, index) =>
    value("category", `category-${index + 1}`, `Category ${index + 1}`),
  ),
  value("tag", "tag-1", "Family"),
  value("tag", "tag-2", "Holiday"),
  value("merchant", "merchant-1", "Bakery"),
];

const renderFilters = () => {
  const onToggleTaxonomy = vi.fn();

  render(
    <MemoryRouter>
      <SidebarFilters
        values={values}
        filter={filter}
        onToggleTaxonomy={onToggleTaxonomy}
      />
    </MemoryRouter>,
  );

  return onToggleTaxonomy;
};

describe("SidebarFilters", () => {
  beforeEach(() => window.localStorage.clear());

  it("keeps only one taxonomy section expanded", () => {
    renderFilters();

    fireEvent.click(screen.getByRole("button", { name: "Tags" }));

    expect(screen.getByRole("button", { name: "Categories" }).getAttribute("aria-expanded")).toBe(
      "false",
    );
    expect(screen.getByRole("button", { name: "Tags" }).getAttribute("aria-expanded")).toBe(
      "true",
    );
    expect(screen.getByRole("button", { name: "Merchants" }).getAttribute("aria-expanded")).toBe(
      "false",
    );
  });

  it("shows five values and a View all action for a long section", () => {
    renderFilters();

    const categoryRegion = screen.getByRole("region", { name: "Categories" });
    expect(within(categoryRegion).getAllByRole("link")).toHaveLength(5);
    expect(within(categoryRegion).getByRole("button", { name: "View all categories" })).toBeDefined();
  });

  it("searches the complete value list in the View all popover", async () => {
    renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "View all categories" }));

    const search = screen.getByRole("textbox", { name: "Search categories" });
    fireEvent.change(search, { target: { value: "Category 9" } });

    const popover = screen.getByLabelText("All categories");
    expect(within(popover).getByRole("link", { name: "Category 9" })).toBeDefined();
    expect(within(popover).queryByRole("link", { name: "Category 2" })).toBeNull();

    fireEvent.keyDown(search, { key: "Escape" });

    await waitFor(() =>
      expect(document.activeElement).toBe(
        screen.getByRole("button", { name: "View all categories" }),
      ),
    );
  });

  it("toggles a taxonomy value and remembers its use", () => {
    const onToggleTaxonomy = renderFilters();
    fireEvent.click(screen.getByRole("button", { name: "Tags" }));

    fireEvent.click(screen.getByRole("link", { name: "Family" }));

    expect(onToggleTaxonomy).toHaveBeenCalledWith("tag", "tag-1");
    expect(window.localStorage.getItem("xpense.sidebar.recent.tag")).toContain("tag-1");
  });

  it("exposes a non-colour selected indicator", () => {
    renderFilters();

    const selected = screen.getByRole("link", { name: "Category 1 Selected" });
    expect(within(selected).getByRole("img", { name: "Selected" })).toBeDefined();
  });
});
