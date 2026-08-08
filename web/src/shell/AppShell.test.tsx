import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router";
import { render, screen } from "@testing-library/react";
import AppShell from "./AppShell";

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
    expect(active).toHaveAttribute("aria-current", "page");
    expect(screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page")).toHaveLength(1);
  });
});
