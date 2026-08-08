import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import ThemeModeToggle from "./ThemeModeToggle";

describe("ThemeModeToggle", () => {
  it("offers light, dark and system", () => {
    render(<ThemeModeToggle />);
    expect(screen.getByRole("button", { name: "Light" })).toBeDefined();
    expect(screen.getByRole("button", { name: "Dark" })).toBeDefined();
    expect(screen.getByRole("button", { name: "System" })).toBeDefined();
  });
});
