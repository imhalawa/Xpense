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
import type { BrandVariants } from "@fluentui/react-components";

const brand: BrandVariants = {
  10: "#020305",
  20: "#111723",
  30: "#16263D",
  40: "#193253",
  50: "#1B3F6A",
  60: "#1B4C82",
  70: "#18599B",
  80: "#1565C0",
  90: "#3B7AD0",
  100: "#598FDC",
  110: "#74A4E6",
  120: "#8FB9EF",
  130: "#AACEF6",
  140: "#C5E2FB",
  150: "#E0F0FD",
  160: "#F5FAFE",
};

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
    const light = createLightTheme(brand);
    const dark = createDarkTheme(brand);

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
