import { Currency } from "../typings/enums/Currency";
import { ICreateBudgetRequest, Recurrence } from "../clients/types";

export interface BudgetFormValues {
  categoryId: number | null;
  amountMajorUnits: string;
  currency: Currency;
  recurrence: Recurrence;
  startsOn: string;
  endsOn: string | null;
  alertThresholdPercent: number | null;
}

export type BudgetFormErrors = Partial<Record<keyof BudgetFormValues, string>>;

const minorUnitsPerMajor = 100;

const parseAmount = (amountMajorUnits: string): number => Number(amountMajorUnits.trim());

export const validateBudgetForm = (values: BudgetFormValues): BudgetFormErrors => {
  const errors: BudgetFormErrors = {};

  if (values.categoryId === null || values.categoryId <= 0)
    errors.categoryId = "The category must be a valid selection.";

  const amount = parseAmount(values.amountMajorUnits);
  if (values.amountMajorUnits.trim() === "" || Number.isNaN(amount))
    errors.amountMajorUnits = "The amount must be a number.";
  else if (amount <= 0) errors.amountMajorUnits = "The amount must be positive.";

  if (values.recurrence === "None" && values.endsOn === null)
    errors.endsOn = "A budget that does not repeat must state when it ends.";
  else if (values.endsOn !== null && values.endsOn < values.startsOn)
    errors.endsOn = "The end date cannot be before the start date.";

  if (
    values.alertThresholdPercent !== null &&
    (values.alertThresholdPercent < 1 || values.alertThresholdPercent > 100)
  )
    errors.alertThresholdPercent = "The alert threshold must be between 1 and 100 percent.";

  return errors;
};

export const toCreateRequest = (values: BudgetFormValues): ICreateBudgetRequest => ({
  categoryId: values.categoryId!,
  amount: {
    minorUnits: Math.round(parseAmount(values.amountMajorUnits) * minorUnitsPerMajor),
    currency: values.currency,
  },
  recurrence: values.recurrence,
  startsOn: values.startsOn,
  endsOn: values.endsOn,
  alertThresholdPercent: values.alertThresholdPercent,
});
