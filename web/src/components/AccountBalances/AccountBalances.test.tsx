import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Currency } from "../../typings/enums/Currency";
import { IAccountResponse } from "../../clients/types";
import AccountBalances from "./AccountBalances";

const account = (
  label: string,
  minorUnits: number,
  currency: Currency,
  isDefault = false
): IAccountResponse => ({
  accountNumber: label.toUpperCase(),
  label,
  balance: { minorUnits, currency },
  isDefault,
  createdAt: "",
  updatedAt: null,
});

const renderBalances = (accounts: IAccountResponse[]) => render(<AccountBalances accounts={accounts} />);

describe("AccountBalances", () => {
  it("shows one tile per account", () => {
    renderBalances([account("ING", 240000, Currency.EUR), account("Revolut", 5000, Currency.USD)]);
    expect(screen.getByText("ING")).toBeDefined();
    expect(screen.getByText("Revolut")).toBeDefined();
  });

  it("formats each balance in its own currency", () => {
    renderBalances([account("ING", 240000, Currency.EUR)]);
    expect(screen.getByText(/2[.,]400[.,]00/)).toBeDefined();
  });

  it("never shows a combined total across currencies", () => {
    renderBalances([account("ING", 100000, Currency.EUR), account("Revolut", 100000, Currency.USD)]);
    expect(screen.queryByText(/2[.,]000[.,]00/)).toBeNull();
  });

  it("marks the default account", () => {
    renderBalances([account("ING", 1000, Currency.EUR, true)]);
    expect(screen.getByText("Default")).toBeDefined();
  });

  it("says so when there are no accounts", () => {
    renderBalances([]);
    expect(screen.getByText(/no accounts/i)).toBeDefined();
  });
});
