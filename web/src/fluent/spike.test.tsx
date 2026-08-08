import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import {
  Button,
  Card,
  FluentProvider,
  ProgressBar,
  Title2,
  createDarkTheme,
  createLightTheme,
  webLightTheme,
} from "@fluentui/react-components";
import { LineChart } from "@fluentui/react-charts";
import { Alert24Regular } from "@fluentui/react-icons";
import { brandRamp } from "./brand";

describe("fluent toolchain", () => {
  it("renders a themed component under FluentProvider", () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <Button>Add transaction</Button>
      </FluentProvider>
    );
    expect(screen.getByRole("button", { name: "Add transaction" })).toBeDefined();
  });

  it("builds a light and a dark theme from the brand ramp", () => {
    const light = createLightTheme(brandRamp);
    const dark = createDarkTheme(brandRamp);

    expect(light.colorBrandBackground).toBeDefined();
    expect(dark.colorBrandBackground).toBeDefined();
    expect(light.colorNeutralBackground1).not.toBe(dark.colorNeutralBackground1);
  });

  it("renders the typography ramp, a card, a progress bar and an icon", () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <Card>
          <Title2>Groceries</Title2>
          <ProgressBar value={0.25} />
          <Alert24Regular />
        </Card>
      </FluentProvider>
    );
    expect(screen.getByText("Groceries")).toBeDefined();
  });

  it("renders a fluent chart", () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <LineChart
          data={{
            chartTitle: "Spending",
            lineChartData: [
              {
                legend: "Expense",
                data: [
                  { x: 1, y: 10 },
                  { x: 2, y: 30 },
                ],
                color: "#1565C0",
              },
            ],
          }}
          height={200}
          width={400}
        />
      </FluentProvider>
    );
    expect(document.querySelector("svg")).toBeDefined();
  });
});
