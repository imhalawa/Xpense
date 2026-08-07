import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import dayjs from "dayjs";
import theme from "../../theme/theme";
import { tokens } from "../../theme/tokens";
import { Currency } from "../../typings/enums/Currency";
import { IBudgetPeriodResponse, IBudgetResponse, IMoneyResponse } from "../../clients/types";
import BudgetMeter from "./BudgetMeter";

const euros = (minorUnits: number): IMoneyResponse => ({ minorUnits, currency: Currency.EUR });

const augustPeriod = (
  spent: number,
  remaining: number,
  exceeded = false,
  uncounted: IMoneyResponse[] = []
): IBudgetPeriodResponse => ({
  name: "2026-08",
  from: "2026-08-01T00:00:00Z",
  toExclusive: "2026-09-01T00:00:00Z",
  spent: euros(spent),
  remaining: euros(remaining),
  exceeded,
  uncounted,
});

const budget = (period: IBudgetResponse["period"]): IBudgetResponse => ({
  id: 1,
  category: {
    id: 1,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 1, createdAt: "", updatedAt: null },
    createdAt: "",
    updatedAt: null,
  },
  amount: euros(100000),
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
  period,
  createdAt: "",
  updatedAt: null,
});

const renderMeter = (item: IBudgetResponse, now = dayjs("2026-08-10T00:00:00Z")) =>
  render(
    <ThemeProvider theme={theme}>
      <BudgetMeter budget={item} now={now} />
    </ThemeProvider>
  );

const collectStyleRules = (): string =>
  Array.from(document.querySelectorAll("style"))
    .map((styleElement) => styleElement.textContent ?? "")
    .join("");

describe("BudgetMeter", () => {
  it("names the category", () => {
    renderMeter(budget(augustPeriod(10000, 90000)));
    expect(screen.getByText("Groceries")).toBeDefined();
  });

  it("shows spent and remaining", () => {
    renderMeter(budget(augustPeriod(25000, 75000)));
    expect(screen.getByText(/250[.,]00/)).toBeDefined();
    expect(screen.getByText(/750[.,]00/)).toBeDefined();
  });

  it("shows how many days are left", () => {
    renderMeter(budget(augustPeriod(10000, 90000)));
    expect(screen.getByText(/21 days left/i)).toBeDefined();
  });

  it("exposes progress to assistive technology, not just as a colour", () => {
    renderMeter(budget(augustPeriod(25000, 75000)));
    const meter = screen.getByRole("progressbar");
    expect(meter.getAttribute("aria-valuenow")).toBe("25");
  });

  it("says the budget is not measuring when it has no period", () => {
    renderMeter(budget(null));
    expect(screen.getByText(/not measuring/i)).toBeDefined();
  });

  it("never hides uncounted spending in another currency", () => {
    renderMeter(
      budget(augustPeriod(10000, 90000, false, [{ minorUnits: 500, currency: Currency.USD }]))
    );
    expect(screen.getByText(/uncounted/i)).toBeDefined();
    expect(screen.getByText(/\$5[.,]00/)).toBeDefined();
  });

  it("warns when the burn rate projects an overspend", () => {
    renderMeter(budget(augustPeriod(20000, 80000)), dayjs("2026-08-05T00:00:00Z"));
    expect(screen.getByText(/projected/i)).toBeDefined();
  });

  it("repaints the bar for the dark colour scheme", () => {
    renderMeter(budget(augustPeriod(10000, 90000)));
    const darkBarRule = collectStyleRules().match(
      /\[data-theme='dark'] \.css-[^{]*MuiLinearProgress-bar[^{]*\{([^}]*)\}/
    );
    expect(darkBarRule).not.toBeNull();
    expect(darkBarRule?.[1] ?? "").toContain(tokens.brand[400]);
  });
});
