import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router";
import { render, screen } from "@testing-library/react";
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

describe("AppShell", () => {
  it("renders all shell destinations and marks active route", () => {
    setWindowWidth(true);

    render(
      <MemoryRouter initialEntries={["/transactions"]}>
        <AppShell>
          <div>Content</div>
        </AppShell>
      </MemoryRouter>
    );

    expect(screen.getByRole("link", { name: "Overview" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Transactions" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Budgets" })).toBeDefined();
    expect(screen.getByRole("link", { name: "Manage" })).toBeDefined();

    const active = screen.getByRole("link", { name: "Transactions" });
    expect(active.getAttribute("aria-current")).toBe("page");
    expect(screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page")).toHaveLength(1);
  });

  it("shows a menu control on narrow screens", () => {
    setWindowWidth(false);

    render(
      <MemoryRouter>
        <AppShell>
          <div>Content</div>
        </AppShell>
      </MemoryRouter>
    );

    expect(screen.getByRole("button", { name: "Open navigation" })).toBeDefined();
  });
});
