import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import StatTile from "./StatTile";

describe("StatTile", () => {
  it("renders the label and value", () => {
    render(<StatTile label="Available" value="€2,400.00" />);
    expect(screen.getByText("Available")).toBeDefined();
    expect(screen.getByText("€2,400.00")).toBeDefined();
  });

  it("renders an optional hint", () => {
    render(
      <StatTile
        label="Available"
        value="€2,400.00"
        hint="Last updated today"
      />
    );
    expect(screen.getByText("Last updated today")).toBeDefined();
  });

  it("renders an optional badge", () => {
    render(
      <StatTile
        label="Available"
        value="€2,400.00"
        badge={<span>Default</span>}
      />
    );
    expect(screen.getByText("Default")).toBeDefined();
  });
});
