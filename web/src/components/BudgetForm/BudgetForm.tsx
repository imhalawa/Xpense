import { FormEvent, useState } from "react";
import {
  Button,
  Dropdown,
  Field,
  Input,
  Option,
  ToggleButton,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { DatePicker } from "@fluentui/react-datepicker-compat";
import dayjs from "dayjs";
import CurrencyOption from "../CurrencyOption/CurrencyOption";
import { ICategoryResponse, Recurrence } from "../../clients/types";
import { Currency } from "../../typings/enums/Currency";
import {
  BudgetFormErrors,
  BudgetFormValues,
  validateBudgetForm,
} from "../../budgets/budgetFormRules";

interface BudgetFormProps {
  categories: ICategoryResponse[];
  initialValues: BudgetFormValues;
  onSubmit: (values: BudgetFormValues) => void;
  onCancel: () => void;
  submitLabel: string;
  isEditing?: boolean;
}

const dayFormat = "YYYY-MM-DD";
const recurrences: Recurrence[] = ["None", "Weekly", "Monthly", "Yearly"];
const currencies: Currency[] = Object.values(Currency);
const alertPresets: number[] = [25, 50, 75];
const noAlertChoice = "none";
const customAlertChoice = "custom";

const useStyles = makeStyles({
  form: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalL,
    paddingTop: tokens.spacingVerticalS,
  },
  thresholdGroup: {
    display: "grid",
    gridTemplateColumns: "repeat(5, minmax(0, 1fr))",
    gap: tokens.spacingHorizontalXS,
    "@media (max-width: 479px)": {
      gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
    },
  },
  threshold: {
    transitionProperty: "background-color, color, border-color",
    transitionDuration: tokens.durationFast,
    transitionTimingFunction: tokens.curveEasyEase,
  },
  control: {
    minWidth: 0,
    width: "100%",
  },
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    gap: tokens.spacingHorizontalS,
  },
});

const choiceForThreshold = (thresholdPercent: number | null): string => {
  if (thresholdPercent === null) return noAlertChoice;
  if (alertPresets.includes(thresholdPercent)) return String(thresholdPercent);
  return customAlertChoice;
};

const BudgetForm = ({
  categories,
  initialValues,
  onSubmit,
  onCancel,
  submitLabel,
  isEditing = false,
}: BudgetFormProps) => {
  const styles = useStyles();
  const [values, setValues] = useState<BudgetFormValues>(initialValues);
  const [errors, setErrors] = useState<BudgetFormErrors>({});
  const [alertChoice, setAlertChoice] = useState<string>(
    choiceForThreshold(initialValues.alertThresholdPercent)
  );

  const handleSubmit = (submitEvent: FormEvent<HTMLFormElement>) => {
    submitEvent.preventDefault();
    const found = validateBudgetForm(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;
    onSubmit(values);
  };

  const selectAlertChoice = (choice: string) => {
    setAlertChoice(choice);
    if (choice === noAlertChoice) {
      setValues({ ...values, alertThresholdPercent: null });
    } else if (choice !== customAlertChoice) {
      setValues({ ...values, alertThresholdPercent: Number(choice) });
    }
  };

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <Field label="Category" required validationMessage={errors.categoryId}>
        <Dropdown
          className={styles.control}
          disabled={isEditing}
          value={categories.find((category) => category.id === values.categoryId)?.label ?? ""}
          selectedOptions={values.categoryId === null ? [] : [String(values.categoryId)]}
          onOptionSelect={(_event, data) =>
            setValues({ ...values, categoryId: Number(data.optionValue) })
          }>
          {categories.map((category) => (
            <Option key={category.id} value={String(category.id)}>
              {category.label}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Amount" required validationMessage={errors.amountMajorUnits}>
        <Input
          className={styles.control}
          inputMode="decimal"
          value={values.amountMajorUnits}
          onChange={(_event, data) => setValues({ ...values, amountMajorUnits: data.value })}
        />
      </Field>

      <Field label="Currency" required validationMessage={errors.currency}>
        <Dropdown
          className={styles.control}
          value={values.currency}
          selectedOptions={[values.currency]}
          onOptionSelect={(_event, data) =>
            setValues({ ...values, currency: data.optionValue as Currency })
          }>
          {currencies.map((currency) => (
            <Option key={currency} value={currency} text={currency}>
              <CurrencyOption currency={currency} />
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Recurrence" required validationMessage={errors.recurrence}>
        <Dropdown
          className={styles.control}
          value={values.recurrence}
          selectedOptions={[values.recurrence]}
          onOptionSelect={(_event, data) =>
            setValues({ ...values, recurrence: data.optionValue as Recurrence })
          }>
          {recurrences.map((recurrence) => (
            <Option key={recurrence} value={recurrence}>
              {recurrence}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Starts on" required validationMessage={errors.startsOn}>
        <DatePicker
          className={styles.control}
          value={dayjs(values.startsOn).toDate()}
          formatDate={(date) => (date === undefined ? "" : dayjs(date).format(dayFormat))}
          onSelectDate={(date) =>
            setValues({ ...values, startsOn: date == null ? "" : dayjs(date).format(dayFormat) })
          }
        />
      </Field>

      <Field label="Ends on" validationMessage={errors.endsOn}>
        <DatePicker
          className={styles.control}
          value={values.endsOn === null ? null : dayjs(values.endsOn).toDate()}
          formatDate={(date) => (date === undefined ? "" : dayjs(date).format(dayFormat))}
          onSelectDate={(date) =>
            setValues({
              ...values,
              endsOn: date === null || date === undefined ? null : dayjs(date).format(dayFormat),
            })
          }
        />
      </Field>

      <Field label="Alert threshold" validationMessage={errors.alertThresholdPercent}>
        <div className={styles.thresholdGroup} role="group" aria-label="Alert threshold">
          <ToggleButton
            className={styles.threshold}
            checked={alertChoice === noAlertChoice}
            onClick={() => selectAlertChoice(noAlertChoice)}>
            No alert
          </ToggleButton>
          {alertPresets.map((preset) => (
            <ToggleButton
              key={preset}
              className={styles.threshold}
              checked={alertChoice === String(preset)}
              onClick={() => selectAlertChoice(String(preset))}>
              {preset}%
            </ToggleButton>
          ))}
          <ToggleButton
            className={styles.threshold}
            checked={alertChoice === customAlertChoice}
            onClick={() => selectAlertChoice(customAlertChoice)}>
            Custom
          </ToggleButton>
        </div>
      </Field>

      {alertChoice === customAlertChoice && (
        <Field label="Alert threshold percent" validationMessage={errors.alertThresholdPercent}>
          <Input
            className={styles.control}
            type="number"
            value={
              values.alertThresholdPercent === null ? "" : String(values.alertThresholdPercent)
            }
            onChange={(_event, data) =>
              setValues({
                ...values,
                alertThresholdPercent: data.value === "" ? null : Number(data.value),
              })
            }
          />
        </Field>
      )}

      <div className={styles.actions}>
        <Button type="button" appearance="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" appearance="primary">
          {submitLabel}
        </Button>
      </div>
    </form>
  );
};

export default BudgetForm;
