import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
import { VaultProvider } from "../vault/VaultProvider";
import type { VaultProjection } from "../vault/VaultProjection";
import { getUnreadCount, listNotifications } from "../clients/notifications";
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

const seed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [{ id: "account-1", label: "Current", currency: Currency.EUR, canEdit: true }],
  },
  taxonomy: {
    personal: [
      {
        id: "category-1",
        kind: "category",
        label: "Food",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "tag-1",
        kind: "tag",
        label: "Family",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "merchant-1",
        kind: "merchant",
        label: "Bakery",
        foregroundHex: null,
        backgroundHex: null,
      },
    ],
  },
  transactions: { personal: [] },
};

const exceededBudgetSeed: FixtureSeed = {
  ...seed,
  transactions: {
    personal: [
      {
        id: "transaction-1",
        kind: "expense",
        amountMinorUnits: 15750,
        currency: Currency.EUR,
        occurredAt: new Date().toISOString(),
        accountId: "account-1",
        counterpartyAccountId: null,
        isCounterpartyPrivate: false,
        categoryId: "category-1",
        merchantId: null,
        tagIds: [],
        canEdit: true,
      },
    ],
  },
  budgets: {
    personal: [
      {
        id: "budget-1",
        category: { id: "category-1", label: "Food" },
        amount: { minorUnits: 10000, currency: Currency.EUR },
        recurrence: "Monthly",
        startsOn: "2020-01-01",
        endsOn: null,
        alertThresholdPercent: 75,
        period: null,
        createdAt: "2020-01-01T00:00:00.000Z",
        updatedAt: null,
        canEdit: true,
      },
    ],
  },
};

const setWindowWidth = (isWide: boolean) => {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: isWide && query === "(min-width: 1024px)",
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
};

const renderShell = (path: string, projection: VaultProjection = fixtureProjection(seed)) => {
  render(
    <MemoryRouter initialEntries={[path]}>
      <VaultProvider projection={projection}>
        <LoadingContextProvider>
          <AppShell>
            <div>Content</div>
          </AppShell>
        </LoadingContextProvider>
      </VaultProvider>
    </MemoryRouter>,
  );
  return projection;
};

const appearsBefore = (leading: Element, trailing: Element) =>
  Boolean(leading.compareDocumentPosition(trailing) & Node.DOCUMENT_POSITION_FOLLOWING);

describe("AppShell", () => {
  afterEach(() => {
    localStorage.clear();
  });

  it("renders the fixed sidebar order", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");

    const identity = await screen.findByRole("button", { name: "Local user, Personal" });
    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    const overview = screen.getByRole("link", { name: "Overview" });
    const accounts = screen.getByRole("button", { name: "Accounts" });
    const categories = screen.getByRole("button", { name: "Categories" });
    const notifications = screen.getByRole("button", { name: "No unread notifications" });

    expect(appearsBefore(identity, notifications)).toBe(true);
    expect(appearsBefore(notifications, addTransaction)).toBe(true);
    expect(appearsBefore(identity, addTransaction)).toBe(true);
    expect(appearsBefore(addTransaction, overview)).toBe(true);
    expect(appearsBefore(accounts, categories)).toBe(true);
    expect(screen.queryByRole("link", { name: "Manage" })).toBeNull();
  });

  it("derives budget alerts on the client in encrypted mode and calls no notification route", async () => {
    setWindowWidth(true);
    vi.mocked(listNotifications).mockClear();
    vi.mocked(getUnreadCount).mockClear();
    const encrypted = fixtureProjection(exceededBudgetSeed);
    Object.defineProperty(encrypted, "dataMode", { value: "encrypted" });

    renderShell("/transactions?space=personal", encrypted);
    const bell = await screen.findByRole("button", { name: "1 unread notification" });

    expect(listNotifications).not.toHaveBeenCalled();
    expect(getUnreadCount).not.toHaveBeenCalled();

    fireEvent.click(bell);
    expect(await screen.findByText("Food is over budget")).toBeDefined();

    fireEvent.click(screen.getByRole("button", { name: "Mark all read" }));
    expect(await screen.findByRole("button", { name: "No unread notifications" })).toBeDefined();
  });

  it("locks and unlocks the vault from the identity menu on any route", async () => {
    setWindowWidth(true);
    const projection = renderShell("/budgets?space=personal");
    const unlock = vi.spyOn(projection, "unlock");

    fireEvent.click(await screen.findByRole("button", { name: "Local user, Personal" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Lock vault" }));
    fireEvent.click(await screen.findByRole("button", { name: "local" }));

    expect(screen.queryByRole("menuitem", { name: "Lock vault" })).toBeNull();
    fireEvent.click(screen.getByRole("menuitem", { name: "Unlock vault" }));

    await waitFor(() => expect(unlock).toHaveBeenCalledTimes(1));
    expect(await screen.findByRole("button", { name: "Local user, Personal" })).toBeDefined();
  });

  it("reports a failed manual unlock without exposing its error or rejecting globally", async () => {
    setWindowWidth(true);
    const projection = fixtureProjection(seed);
    vi.spyOn(projection, "unlock").mockRejectedValue(new Error("private unwrap detail"));
    const unhandledRejection = vi.fn();
    window.addEventListener("unhandledrejection", unhandledRejection);
    renderShell("/budgets?space=personal", projection);

    fireEvent.click(await screen.findByRole("button", { name: "Local user, Personal" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Lock vault" }));
    fireEvent.click(await screen.findByRole("button", { name: "local" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Unlock vault" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("The vault could not be unlocked. Try again.");
    expect(alert.textContent).not.toContain("private unwrap detail");
    expect(unhandledRejection).not.toHaveBeenCalled();
    window.removeEventListener("unhandledrejection", unhandledRejection);
  });

  it("changes the color scheme through the identity submenu", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");

    fireEvent.click(await screen.findByRole("button", { name: "Local user, Personal" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Theme" }));
    fireEvent.click(screen.getByRole("menuitemradio", { name: "Dark" }));

    expect(localStorage.getItem("xpense-color-scheme")).toBe("dark");
  });

  it("has no bottom theme or vault control shelf", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");
    await screen.findByRole("button", { name: "Local user, Personal" });

    expect(screen.queryByRole("group", { name: "Color scheme" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Lock vault" })).toBeNull();
  });

  it("renders all destinations and marks exactly one active route", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");
    await screen.findByRole("button", { name: "Local user, Personal" });

    expect(screen.getByRole("link", { name: "Overview" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Transactions" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Budgets" })).toBeDefined();
    expect(screen.queryByRole("link", { name: "Manage" })).toBeNull();
    expect(screen.getByRole("link", { name: "Transactions" }).getAttribute("aria-current")).toBe(
      "page",
    );
    expect(
      screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page"),
    ).toHaveLength(1);
  });

  it("clears a deleted active filter, restores stable focus, and dismisses the notice", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal&account=account-1");

    fireEvent.click(await screen.findByRole("button", { name: "Accounts" }));
    const account = await screen.findByRole("link", { name: /Current/ });
    fireEvent.mouseEnter(account.parentElement!);
    fireEvent.click(screen.getByRole("button", { name: "Actions for Current" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Delete" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    expect(await screen.findByText(/account filter was cleared/)).toBeDefined();
    await waitFor(() => expect(screen.getByRole("button", { name: "Accounts" })).toHaveFocus());
    fireEvent.click(screen.getByRole("button", { name: "Dismiss resource message" }));
    expect(screen.queryByText(/account filter was cleared/)).toBeNull();
  });

  it("uses an inline drawer, 1200px content, and one matching heading on desktop", async () => {
    setWindowWidth(true);
    renderShell("/budgets");
    await screen.findByRole("button", { name: "Local user, Personal" });

    expect(screen.queryByRole("button", { name: "Open navigation" })).toBeNull();
    expect(getComputedStyle(screen.getByTestId("app-content")).maxWidth).toBe("1200px");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Budgets");
  });

  it("opens and closes the overlay drawer after navigation on narrow screens", async () => {
    setWindowWidth(false);
    renderShell("/transactions?space=personal");

    fireEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    const overview = await screen.findByRole("link", { name: "Overview" });
    fireEvent.click(overview);

    await waitFor(() => expect(screen.queryByRole("link", { name: "Overview" })).toBeNull());
  });
});
