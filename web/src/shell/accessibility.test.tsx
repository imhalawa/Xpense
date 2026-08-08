import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router";
import { appGlobalStyles } from "../fluent/GlobalStyles";
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

vi.mock("../pages/Transactions/TransactionsForm/TransactionsForm", () => ({
  default: ({ onCancel }: { onCancel: () => void }) => (
    <button type="button" onClick={onCancel}>
      Cancel
    </button>
  ),
}));

const seed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    personal: [{ id: "everyday", label: "Everyday", currency: Currency.EUR, canEdit: true }],
  },
  taxonomy: {
    personal: [
      ...Array.from({ length: 6 }, (_value, index) => ({
        id: `category-${index}`,
        kind: "category" as const,
        label: `Category ${index}`,
        foregroundHex: null,
        backgroundHex: null,
      })),
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

const renderShell = (width = 1440) => {
  setWidth(width);
  return render(
    <MemoryRouter initialEntries={["/transactions?space=personal"]}>
      <VaultProvider projection={fixtureProjection(seed)}>
        <LoadingContextProvider>
          <AppShell>
            <div>Transactions content</div>
          </AppShell>
        </LoadingContextProvider>
      </VaultProvider>
    </MemoryRouter>,
  );
};

const appearsBefore = (leading: Element, trailing: Element) =>
  Boolean(leading.compareDocumentPosition(trailing) & Node.DOCUMENT_POSITION_FOLLOWING);

describe("app shell accessibility", () => {
  it("uses navigation and main landmarks with one page heading", async () => {
    renderShell();
    await screen.findByRole("button", { name: "Local user, Personal" });

    expect(screen.queryByRole("banner")).toBeNull();
    expect(screen.getAllByRole("navigation")).toHaveLength(1);
    expect(screen.getAllByRole("main")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("keeps the h1 clipped on desktop and visible on mobile", async () => {
    const desktop = renderShell();
    const desktopHeading = await screen.findByRole("heading", { level: 1 });
    expect(getComputedStyle(desktopHeading).position).toBe("absolute");
    expect(getComputedStyle(desktopHeading).width).toBe("1px");
    desktop.unmount();

    renderShell(390);
    const mobileHeading = screen.getByRole("heading", { level: 1 });
    expect(getComputedStyle(mobileHeading).position).not.toBe("absolute");
  });

  it("gives every button and link an accessible name", async () => {
    renderShell();
    await screen.findByRole("button", { name: "Local user, Personal" });

    for (const control of [...screen.getAllByRole("button"), ...screen.getAllByRole("link")]) {
      expect(control).toHaveAccessibleName();
    }
  });

  it("keeps the primary controls in a reachable tab order", async () => {
    renderShell();
    const identity = await screen.findByRole("button", { name: "Local user, Personal" });
    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    const destinations = ["Overview", "Transactions", "Budgets", "Manage"].map((name) =>
      screen.getByRole("link", { name }),
    );
    const sections = ["Categories", "Tags", "Merchants"].map((name) =>
      screen.getByRole("button", { name }),
    );
    const ordered = [identity, addTransaction, ...destinations, ...sections];

    for (const control of ordered) expect(control.tabIndex).toBeGreaterThanOrEqual(0);
    for (let index = 0; index < ordered.length - 1; index += 1) {
      expect(appearsBefore(ordered[index], ordered[index + 1])).toBe(true);
    }
    for (const section of sections) expect(section).toHaveAttribute("aria-expanded");
  });

  it("returns focus from the View all popover and transaction dialog", async () => {
    renderShell();
    const viewAll = await screen.findByRole("button", { name: "View all categories" });
    fireEvent.click(viewAll);
    const search = await screen.findByRole("textbox", { name: "Search categories" });
    search.focus();
    fireEvent.keyDown(search, { key: "Escape" });
    await waitFor(() => expect(viewAll).toHaveFocus());

    const addTransaction = screen.getByRole("button", { name: "Add transaction" });
    fireEvent.click(addTransaction);
    const dialog = await screen.findByRole("dialog");
    fireEvent.keyDown(dialog, { key: "Escape" });
    await waitFor(() => expect(addTransaction).toHaveFocus());
  });

  it("defines a reduced-motion override globally", () => {
    const globalCss = JSON.stringify(appGlobalStyles);
    expect(globalCss).toContain("prefers-reduced-motion: reduce");
    expect(globalCss).toContain("transition-duration: 0.01ms");
  });
});
