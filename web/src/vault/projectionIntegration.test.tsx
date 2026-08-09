import axios from "axios";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter, useLocation } from "react-router";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import Transactions from "../pages/Transactions/Transactions";
import AppShell from "../shell/AppShell";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "./fixtureProjection";
import type { FixtureSeed } from "./fixtureProjection";
import type { TransactionView } from "./VaultProjection";
import { VaultProvider } from "./VaultProvider";

vi.mock("axios", () => ({
  default: {
    get: vi.fn(() => Promise.reject(new Error("Network access is forbidden"))),
    post: vi.fn(() => Promise.reject(new Error("Network access is forbidden"))),
    put: vi.fn(() => Promise.reject(new Error("Network access is forbidden"))),
    delete: vi.fn(() => Promise.reject(new Error("Network access is forbidden"))),
  },
}));

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

const transaction = (
  id: string,
  amountMinorUnits: number,
  occurredAt: string,
  categoryId: string,
  tagIds: string[],
): TransactionView => ({
  id,
  kind: "expense",
  amountMinorUnits,
  currency: Currency.EUR,
  occurredAt,
  accountId: "personal-account",
  counterpartyAccountId: null,
  isCounterpartyPrivate: false,
  categoryId,
  merchantId: "market",
  tagIds,
  canEdit: true,
});

const seed: FixtureSeed = {
  spaces: [
    { id: "personal", name: "Personal", kind: "personal", canEdit: true },
    { id: "family-space", name: "Family", kind: "group", canEdit: true },
  ],
  accounts: {
    personal: [
      { id: "personal-account", label: "Everyday", currency: Currency.EUR, canEdit: true },
    ],
    "family-space": [
      { id: "family-account", label: "Household", currency: Currency.EUR, canEdit: true },
    ],
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
        id: "travel",
        kind: "category",
        label: "Travel",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "family-tag",
        kind: "tag",
        label: "Family",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "market",
        kind: "merchant",
        label: "Market",
        foregroundHex: null,
        backgroundHex: null,
      },
    ],
    "family-space": [
      {
        id: "family-groceries",
        kind: "category",
        label: "Family groceries",
        foregroundHex: null,
        backgroundHex: null,
      },
    ],
  },
  transactions: {
    personal: [
      transaction("august-start", 1100, "2026-08-01T12:00:00.000Z", "food", ["family-tag"]),
      transaction("august-end", 2200, "2026-08-31T12:00:00.000Z", "food", ["family-tag"]),
      transaction("travel", 3300, "2026-08-15T12:00:00.000Z", "travel", ["family-tag"]),
      transaction("outside", 4400, "2026-07-31T12:00:00.000Z", "food", ["family-tag"]),
      transaction("untagged", 5500, "2026-08-10T12:00:00.000Z", "food", []),
    ],
    "family-space": [],
  },
};

const setDesktop = () => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: query === "(min-width: 1024px)" || query === "(min-width: 768px)",
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const LocationProbe = () => {
  const location = useLocation();
  return <output aria-label="Current search">{location.search}</output>;
};

const currentSearch = () =>
  new URLSearchParams(screen.getByRole("status", { name: "Current search" }).textContent ?? "");

const dataRowText = () =>
  screen
    .getAllByRole("row")
    .slice(1)
    .map((row) => row.textContent ?? "");

const renderProjection = () => {
  setDesktop();
  render(
    <MemoryRouter initialEntries={["/transactions?space=personal"]}>
      <VaultProvider projection={fixtureProjection(seed)}>
        <LoadingContextProvider>
          <AppShell>
            <LocationProbe />
            <Transactions />
          </AppShell>
        </LoadingContextProvider>
      </VaultProvider>
    </MemoryRouter>,
  );
};

describe("projection filtering integration", () => {
  it("filters locally across taxonomy, dates and spaces without API queries", async () => {
    renderProjection();
    await screen.findByLabelText("Transaction total");

    fireEvent.click(screen.getByRole("button", { name: "Categories" }));
    fireEvent.click(await screen.findByRole("link", { name: "Food" }));
    await waitFor(() => expect(currentSearch().get("category")).toBe("food"));
    await waitFor(() => expect(dataRowText()).toHaveLength(4));
    expect(dataRowText().some((row) => row.includes("33.00 EUR"))).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "Tags" }));
    fireEvent.click(await screen.findByRole("link", { name: "Family" }));
    await waitFor(() => expect(currentSearch().get("tag")).toBe("family-tag"));
    expect(currentSearch().get("category")).toBe("food");
    await waitFor(() => expect(dataRowText()).toHaveLength(3));
    expect(dataRowText().some((row) => row.includes("55.00 EUR"))).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Current search" })).toHaveTextContent(
        "?space=personal",
      ),
    );
    fireEvent.click(await screen.findByRole("link", { name: "Family" }));
    await waitFor(() => expect(currentSearch().get("tag")).toBe("family-tag"));
    expect(currentSearch().get("category")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "All dates" }));
    const from = await screen.findByLabelText("From");
    const to = screen.getByLabelText("To");
    fireEvent.change(from, { target: { value: "2026-08-01" } });
    fireEvent.blur(from);
    fireEvent.change(to, { target: { value: "2026-08-31" } });
    fireEvent.blur(to);

    await waitFor(() => expect(currentSearch().get("from")).toBe("2026-08-01"));
    await waitFor(() => expect(currentSearch().get("to")).toBe("2026-08-31"));
    await waitFor(() => expect(dataRowText()).toHaveLength(3));
    expect(dataRowText().some((row) => row.includes("44.00 EUR"))).toBe(false);
    expect(dataRowText().some((row) => row.includes("11.00 EUR"))).toBe(true);
    expect(dataRowText().some((row) => row.includes("22.00 EUR"))).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() => expect(screen.getByRole("status", { name: "Current search" })).toHaveTextContent("?space=personal"));

    fireEvent.click(screen.getByRole("button", { name: "Local user, Personal" }));
    fireEvent.click(await screen.findByRole("menuitemradio", { name: "Family" }));
    await waitFor(() => expect(currentSearch().get("space")).toBe("family-space"));
    const familyCategories = screen.getByRole("button", { name: "Categories" });
    if (familyCategories.getAttribute("aria-expanded") === "false") {
      fireEvent.click(familyCategories);
    }
    await waitFor(() =>
      expect(screen.getByRole("link", { name: "Family groceries" })).toBeDefined(),
    );

    expect(vi.mocked(axios.get)).not.toHaveBeenCalled();
    expect(vi.mocked(axios.post)).not.toHaveBeenCalled();
    expect(vi.mocked(axios.put)).not.toHaveBeenCalled();
    expect(vi.mocked(axios.delete)).not.toHaveBeenCalled();
  }, 10_000);
});
