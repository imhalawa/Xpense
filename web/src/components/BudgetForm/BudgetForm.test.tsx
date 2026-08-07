import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import theme from "../../theme/theme";
import { Currency } from "../../typings/enums/Currency";
import { ICategoryResponse } from "../../clients/types";
import { BudgetFormValues } from "../../budgets/budgetFormRules";
import BudgetForm from "./BudgetForm";

const categories: ICategoryResponse[] = [
  {
    id: 1,
    label: "Groceries",
    priority: { id: 1, label: "Essential", weight: 1, createdAt: "", updatedAt: null },
    createdAt: "",
    updatedAt: null,
  },
];

const initialValues: BudgetFormValues = {
  categoryId: 1,
  amountMajorUnits: "250",
  currency: Currency.EUR,
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
};

const renderForm = (onSubmit = vi.fn(), values = initialValues) => {
  render(
    <ThemeProvider theme={theme}>
      <BudgetForm
        categories={categories}
        initialValues={values}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        submitLabel="Save"
      />
    </ThemeProvider>
  );
  return onSubmit;
};

describe("BudgetForm", () => {
  it("submits valid values", () => {
    const onSubmit = renderForm();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it("does not submit when the amount is zero, and says why", () => {
    const onSubmit = renderForm(vi.fn(), { ...initialValues, amountMajorUnits: "0" });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/must be positive/i)).toBeDefined();
  });

  it("does not submit a one-off budget with no end date, and says why", () => {
    const onSubmit = renderForm(vi.fn(), {
      ...initialValues,
      recurrence: "None",
      endsOn: null,
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/does not repeat/i)).toBeDefined();
  });

  it("offers every recurrence the api accepts", () => {
    renderForm();
    expect(screen.getByLabelText(/recurrence/i)).toBeDefined();
  });
});
