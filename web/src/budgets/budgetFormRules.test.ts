import { describe, expect, it } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { BudgetFormValues, toCreateRequest, validateBudgetForm } from "./budgetFormRules";

const valid: BudgetFormValues = {
  categoryId: 1,
  amountMajorUnits: "250.00",
  currency: Currency.EUR,
  recurrence: "Monthly",
  startsOn: "2026-08-01",
  endsOn: null,
  alertThresholdPercent: 75,
};

describe("validateBudgetForm", () => {
  it("accepts a valid monthly budget", () => {
    expect(validateBudgetForm(valid)).toEqual({});
  });

  it("requires a category", () => {
    expect(validateBudgetForm({ ...valid, categoryId: null }).categoryId).toMatch(/category/i);
  });

  it("rejects a zero amount", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "0" }).amountMajorUnits).toMatch(
      /positive/i
    );
  });

  it("rejects a negative amount", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "-5" }).amountMajorUnits).toMatch(
      /positive/i
    );
  });

  it("rejects an amount that is not a number", () => {
    expect(validateBudgetForm({ ...valid, amountMajorUnits: "abc" }).amountMajorUnits).toBeDefined();
  });

  it("requires an end date when the budget does not repeat", () => {
    const errors = validateBudgetForm({ ...valid, recurrence: "None", endsOn: null });
    expect(errors.endsOn).toMatch(/does not repeat/i);
  });

  it("accepts a one-off budget that states its end", () => {
    const errors = validateBudgetForm({
      ...valid,
      recurrence: "None",
      endsOn: "2026-08-31",
    });
    expect(errors.endsOn).toBeUndefined();
  });

  it("rejects an end date before the start date", () => {
    const errors = validateBudgetForm({ ...valid, endsOn: "2026-07-01" });
    expect(errors.endsOn).toMatch(/before the start/i);
  });

  it("accepts an end date equal to the start date", () => {
    const errors = validateBudgetForm({ ...valid, endsOn: "2026-08-01" });
    expect(errors.endsOn).toBeUndefined();
  });

  it("rejects a threshold below one", () => {
    expect(validateBudgetForm({ ...valid, alertThresholdPercent: 0 }).alertThresholdPercent).toMatch(
      /between 1 and 100/i
    );
  });

  it("rejects a threshold above one hundred", () => {
    expect(
      validateBudgetForm({ ...valid, alertThresholdPercent: 101 }).alertThresholdPercent
    ).toMatch(/between 1 and 100/i);
  });

  it("accepts no threshold at all", () => {
    expect(
      validateBudgetForm({ ...valid, alertThresholdPercent: null }).alertThresholdPercent
    ).toBeUndefined();
  });
});

describe("toCreateRequest", () => {
  it("converts major units to minor units", () => {
    expect(toCreateRequest(valid).amount).toEqual({ minorUnits: 25000, currency: Currency.EUR });
  });

  it("rounds rather than truncating a fractional minor unit", () => {
    const request = toCreateRequest({ ...valid, amountMajorUnits: "10.005" });
    expect(request.amount.minorUnits).toBe(1001);
  });

  it("keeps dates as plain day strings", () => {
    const request = toCreateRequest({ ...valid, endsOn: "2026-12-31" });
    expect(request.startsOn).toBe("2026-08-01");
    expect(request.endsOn).toBe("2026-12-31");
  });

  it("passes the category through", () => {
    expect(toCreateRequest(valid).categoryId).toBe(1);
  });
});
