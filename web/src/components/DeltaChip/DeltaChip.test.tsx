import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import DeltaChip from "./DeltaChip";

const renderChip = (element: React.ReactElement) =>
  render(<ThemeProvider theme={theme}>{element}</ThemeProvider>);

describe("DeltaChip", () => {
  it("shows its label", () => {
    renderChip(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByText("8% over budget")).toBeDefined();
  });

  it("carries a direction arrow so colour is never the only signal", () => {
    renderChip(
      <DeltaChip direction="up" tone="overBudget">
        8% over budget
      </DeltaChip>
    );
    expect(screen.getByLabelText("increase")).toBeDefined();
  });

  it("labels a downward delta as a decrease", () => {
    renderChip(
      <DeltaChip direction="down" tone="comparison">
        12% vs last month
      </DeltaChip>
    );
    expect(screen.getByLabelText("decrease")).toBeDefined();
  });
});
