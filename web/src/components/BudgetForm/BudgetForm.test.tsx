import { describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
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

const renderForm = (onSubmit = vi.fn(), values = initialValues, isEditing = false) => {
  render(
    <BudgetForm
      categories={categories}
      initialValues={values}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
      submitLabel="Save"
      isEditing={isEditing}
    />
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

  it("locks the category while editing because the update endpoint cannot change it", () => {
    renderForm(vi.fn(), initialValues, true);
    expect(screen.getByRole("combobox", { name: "Category" }).hasAttribute("disabled")).toBe(true);
    cleanup();
    renderForm(vi.fn(), initialValues, false);
    expect(screen.getByRole("combobox", { name: "Category" }).hasAttribute("disabled")).toBe(
      false
    );
  });

  it("names the currency by symbol and iso code so a dollar is never ambiguous", () => {
    renderForm();
    expect(screen.getByText("EUR")).toBeDefined();
  });

  it("submits the percent behind an alert threshold quick pick", () => {
    const onSubmit = renderForm();
    fireEvent.click(screen.getByRole("button", { name: "50%" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit.mock.calls[0][0].alertThresholdPercent).toBe(50);
  });

  it("submits no threshold at all when the budget should stay quiet", () => {
    const onSubmit = renderForm();
    fireEvent.click(screen.getByRole("button", { name: "No alert" }));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit.mock.calls[0][0].alertThresholdPercent).toBeNull();
  });

  it("reveals a number field only once the threshold is custom", () => {
    renderForm();
    expect(screen.queryByLabelText("Alert threshold percent")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Custom" }));
    expect(screen.getByLabelText("Alert threshold percent")).toBeDefined();
  });
});
