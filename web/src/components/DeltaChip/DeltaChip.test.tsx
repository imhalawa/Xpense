import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import DeltaChip from "./DeltaChip";

describe("DeltaChip", () => {
  it("shows its label", () => {
    render(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByText("8% over budget")).toBeDefined();
  });

  it("carries a direction arrow so colour is not the only signal", () => {
    render(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByLabelText("increase")).toBeDefined();
  });

  it("labels a downward delta as a decrease", () => {
    render(
      <DeltaChip direction="down" tone="comparison">
        12% vs last month
      </DeltaChip>
    );
    expect(screen.getByLabelText("decrease")).toBeDefined();
  });
});
