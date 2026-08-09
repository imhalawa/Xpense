import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { BudgetFormValues } from "../../budgets/budgetFormRules";
import { Currency } from "../../typings/enums/Currency";
import { fixtureProjection, type FixtureSeed } from "../../vault/fixtureProjection";
import { VaultProvider } from "../../vault/VaultProvider";
import Budgets from "./Budgets";

vi.mock("../../components/BudgetForm/BudgetForm", () => ({
  default: ({
    initialValues,
    onSubmit,
    submitLabel,
  }: {
    initialValues: BudgetFormValues;
    onSubmit(values: BudgetFormValues): void;
    submitLabel: string;
  }) => (
    <button
      type="button"
      onClick={() =>
        onSubmit({
          ...initialValues,
          categoryId: initialValues.categoryId ?? 1,
          amountMajorUnits: "12",
        })
      }>
      {submitLabel}
    </button>
  ),
}));

const categoryId = "11111111-1111-4111-8111-111111111111";
const budgetId = "22222222-2222-4222-8222-222222222222";

const seed: FixtureSeed = {
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: { personal: [] },
  taxonomy: {
    personal: [
      {
        id: categoryId,
        kind: "category",
        label: "Food",
        foregroundHex: null,
        backgroundHex: null,
      },
    ],
  },
  transactions: { personal: [] },
  budgets: {
    personal: [
      {
        id: budgetId,
        category: { id: categoryId, label: "Food" },
        amount: { minorUnits: 1000, currency: Currency.EUR },
        recurrence: "Monthly",
        startsOn: "2026-08-01",
        endsOn: null,
        alertThresholdPercent: null,
        period: null,
        createdAt: "2026-08-01T00:00:00Z",
        updatedAt: null,
        canEdit: true,
      },
    ],
  },
};

describe("Budgets", () => {
  it("round-trips UUID category and budget identifiers through create and edit", async () => {
    const projection = fixtureProjection(seed);
    const saveBudget = vi.spyOn(projection, "saveBudget");
    render(
      <VaultProvider projection={projection}>
        <Budgets />
      </VaultProvider>,
    );

    await screen.findByText("Food");
    fireEvent.click(screen.getByRole("button", { name: "New budget" }));
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(saveBudget).toHaveBeenCalledTimes(1));
    expect(saveBudget.mock.calls[0][1]).toEqual(
      expect.objectContaining({ id: null, categoryId }),
    );

    fireEvent.click(screen.getAllByRole("button", { name: /edit the food budget/i })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(saveBudget).toHaveBeenCalledTimes(2));
    expect(saveBudget.mock.calls[1][1]).toEqual(
      expect.objectContaining({ id: budgetId, categoryId }),
    );
    for (const call of saveBudget.mock.calls) {
      expect(call[1].categoryId).not.toBeNull();
      expect(Number.isNaN(call[1].categoryId)).toBe(false);
    }
  });
});
