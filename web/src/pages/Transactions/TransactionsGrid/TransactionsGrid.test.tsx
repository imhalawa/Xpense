import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Currency } from "../../../typings/enums/Currency";
import TransactionsGrid from "./TransactionsGrid";

vi.mock("../../../clients/transactions", () => ({
  listTransactions: vi.fn().mockResolvedValue({
    items: [
      {
        id: 1,
        kind: "expense",
        amount: { minorUnits: 900, currency: Currency.EUR },
        sourceAccountNumber: "1000000003",
        destinationAccountNumber: null,
        categoryId: 3,
        merchant: { id: 2, label: "Coffee Co" },
        tags: [{ id: 4, label: "morning" }],
        reason: null,
        occurredAt: "2026-08-07T00:00:00Z",
        createdAt: "2026-08-07T00:00:00Z",
        updatedAt: null,
      },
    ],
    page: 1,
    pageSize: 10,
    totalItems: 1,
    totalPages: 1,
  }),
}));

vi.mock("../../../clients/options", () => ({
  listCategories: vi.fn().mockResolvedValue([
    {
      id: 3,
      label: "Coffee",
      priority: { id: 1, label: "Essential", weight: 1 },
      createdAt: "2026-08-01T00:00:00Z",
      updatedAt: null,
    },
  ]),
}));

vi.mock("../../../contexts/LoadingContext", () => ({
  useLoading: () => ({ setLoading: vi.fn() }),
}));

vi.mock("../../../contexts/TransactionUtilitiesContext", () => ({
  useTransctionUtilities: () => ({
    submittedTransaction: null,
    setSubmittedTransaction: vi.fn(),
  }),
}));

describe("TransactionsGrid", () => {
  it("provides a readable transaction summary for narrow screens", async () => {
    render(<TransactionsGrid />);

    const summary = await screen.findByRole("article", {
      name: "9 EUR on 07 Aug 2026",
      hidden: true,
    });

    expect(within(summary).getByText("Coffee")).toBeTruthy();
    expect(within(summary).getByText("Coffee Co")).toBeTruthy();
    expect(within(summary).getByText("1000000003")).toBeTruthy();
    expect(within(summary).getByText("#morning")).toBeTruthy();
  });
});
