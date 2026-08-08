import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import PageTitle from "./PageTitle";

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

describe("PageTitle", () => {
  it("renders exactly one level one heading at 1440px", () => {
    setWindowWidth(true);

    render(<PageTitle title="Transactions" />);

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Transactions" })).toBeDefined();
  });

  it("renders exactly one level one heading at 390px", () => {
    setWindowWidth(false);

    render(<PageTitle title="Transactions" />);

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Transactions" })).toBeDefined();
  });

  it("clips the heading out of sight at 1440px while keeping it in the accessibility tree", () => {
    setWindowWidth(true);

    render(<PageTitle title="Budgets" />);

    const heading = screen.getByRole("heading", { level: 1, name: "Budgets" });
    const computed = window.getComputedStyle(heading);

    expect(computed.position).toBe("absolute");
    expect(computed.width).toBe("1px");
    expect(computed.height).toBe("1px");
    expect(computed.clip).toBe("rect(0px, 0px, 0px, 0px)");
    expect(computed.overflow).toBe("hidden");
    expect(computed.display).not.toBe("none");
    expect(computed.visibility).not.toBe("hidden");
  });

  it("shows the heading at 390px", () => {
    setWindowWidth(false);

    render(<PageTitle title="Budgets" />);

    const heading = screen.getByRole("heading", { level: 1, name: "Budgets" });
    const computed = window.getComputedStyle(heading);

    expect(computed.position).not.toBe("absolute");
    expect(computed.width).not.toBe("1px");
    expect(computed.clip).toBe("");
  });
});
