import { render, screen } from "@testing-library/react";
import { MemoryRouter, Outlet } from "react-router";
import { describe, expect, it, vi } from "vitest";
import App from "./App";

vi.mock("./pages/Claim/Claim", () => ({
  default: () => <div>Standalone claim page</div>,
}));

vi.mock("./pages/Layout", () => ({
  default: () => <div>Application shell<Outlet /></div>,
}));

vi.mock("./vault/plaintextProjection", () => ({
  plaintextProjection: () => ({
    state: "ready",
    subscribe: () => () => undefined,
    unlock: vi.fn(),
    lock: vi.fn(),
  }),
}));

describe("App routing", () => {
  it("renders the maintenance claim outside the normal application layout", () => {
    render(
      <MemoryRouter initialEntries={["/claim"]}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByText("Standalone claim page")).toBeVisible();
    expect(screen.queryByText("Application shell")).not.toBeInTheDocument();
  });
});
