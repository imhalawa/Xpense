import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
import type { AccountView, TransactionDraft } from "../vault/VaultProjection";
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
  default: ({
    accounts,
    activeSpace,
    onCancel,
    onSubmit,
  }: {
    accounts: AccountView[];
    activeSpace: string;
    onCancel: () => void;
    onSubmit: (draft: TransactionDraft) => Promise<void>;
  }) => (
    <div>
      <label>
        Account
        <select aria-label="Account">
          {accounts.map((account) => (
            <option key={account.id}>{account.label}</option>
          ))}
        </select>
      </label>
      <button
        type="button"
        onClick={() =>
          void onSubmit({
            id: null,
            space: activeSpace,
            kind: "expense",
            amountMinorUnits: 1250,
            currency: Currency.EUR,
            occurredAt: "2026-08-08T12:00:00.000Z",
            accountId: accounts[0]?.id ?? "",
            categoryId: null,
            merchantLabel: null,
            tagLabels: [],
            reason: null,
          })
        }>
        Create
      </button>
      <button type="button" onClick={onCancel}>
        Cancel
      </button>
    </div>
  ),
}));

const baseSeed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [
      { id: "editable", label: "Everyday", currency: Currency.EUR, canEdit: true },
      { id: "readonly", label: "Shared view", currency: Currency.EUR, canEdit: false },
    ],
  },
  taxonomy: { personal: [] },
  transactions: { personal: [] },
};

const setWideScreen = () => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: query === "(min-width: 1024px)",
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
  return <output aria-label="Current location">{location.pathname}</output>;
};

const renderShell = (seed: FixtureSeed, initialPath = "/budgets") => {
  const projection = fixtureProjection(seed);
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <VaultProvider projection={projection}>
        <LoadingContextProvider>
          <AppShell>
            <LocationProbe />
          </AppShell>
        </LoadingContextProvider>
      </VaultProvider>
    </MemoryRouter>,
  );
  return projection;
};

describe("TransactionDialog", () => {
  it("opens from another page and lists only editable accounts", async () => {
    setWideScreen();
    renderShell(baseSeed);

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeEnabled());
    fireEvent.click(addTransaction);

    const dialog = await screen.findByRole("dialog");
    expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent(
      "/transactions/new",
    );
    expect(within(dialog).getByRole("option", { name: "Everyday" })).toBeDefined();
    expect(within(dialog).queryByRole("option", { name: "Shared view" })).toBeNull();
  });

  it("disables creation and explains when no account can be edited", async () => {
    setWideScreen();
    renderShell({ ...baseSeed, accounts: { personal: [] } }, "/transactions/new");

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeDisabled());
    expect(addTransaction).toHaveAccessibleDescription(
      "No account in this space can be edited.",
    );
  });

  it("closes with Escape and returns focus to Add transaction", async () => {
    setWideScreen();
    renderShell(baseSeed);

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    await waitFor(() => expect(addTransaction).toBeEnabled());
    fireEvent.click(addTransaction);
    const dialog = await screen.findByRole("dialog");
    await waitFor(() =>
      expect(dialog).toContainElement(document.activeElement as HTMLElement | null),
    );

    fireEvent.keyDown(dialog, { key: "Escape" });

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent("/budgets");
    await waitFor(() => expect(addTransaction).toHaveFocus());
  });

  it("saves through the projection and leaves the creation route", async () => {
    setWideScreen();
    const projection = renderShell(baseSeed, "/transactions/new");
    const saveTransaction = vi.spyOn(projection, "saveTransaction");

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));

    await waitFor(() => expect(saveTransaction).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.getByRole("status", { name: "Current location" })).toHaveTextContent(
        "/transactions",
      ),
    );
  });
});
