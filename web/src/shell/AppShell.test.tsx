import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { LoadingContextProvider } from "../contexts/LoadingContext";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "../vault/fixtureProjection";
import type { FixtureSeed } from "../vault/fixtureProjection";
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

const renderShell = (path: string) => {
  const projection = fixtureProjection(seed);
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
};

const appearsBefore = (leading: Element, trailing: Element) =>
  Boolean(leading.compareDocumentPosition(trailing) & Node.DOCUMENT_POSITION_FOLLOWING);

describe("AppShell", () => {
  it("renders the fixed sidebar order", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");

    const identity = await screen.findByRole("button", { name: "Local user, Personal" });
    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    const overview = screen.getByRole("link", { name: "Overview" });
    const manage = screen.getByRole("link", { name: "Manage" });
    const categories = screen.getByRole("button", { name: "Categories" });
    const lock = screen.getByRole("button", { name: "Lock vault", hidden: true });

    expect(appearsBefore(identity, addTransaction)).toBe(true);
    expect(appearsBefore(addTransaction, overview)).toBe(true);
    expect(appearsBefore(manage, categories)).toBe(true);
    expect(appearsBefore(categories, lock)).toBe(true);
  });

  it("renders all destinations and marks exactly one active route", async () => {
    setWindowWidth(true);
    renderShell("/transactions?space=personal");
    await screen.findByRole("button", { name: "Local user, Personal" });

    expect(screen.getByRole("link", { name: "Overview" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Transactions" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Budgets" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Manage" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Transactions" }).getAttribute("aria-current")).toBe(
      "page",
    );
    expect(
      screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page"),
    ).toHaveLength(1);
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
