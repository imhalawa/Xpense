import { FormEvent, useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
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

const recurrences: Recurrence[] = ["None", "Weekly", "Monthly", "Yearly"];

const currencies: Currency[] = Object.values(Currency);

const BudgetForm = ({
  categories,
  initialValues,
  onSubmit,
  onCancel,
  submitLabel,
  isEditing = false,
}: BudgetFormProps) => {
  const [values, setValues] = useState<BudgetFormValues>(initialValues);
  const [errors, setErrors] = useState<BudgetFormErrors>({});

  const handleSubmit = (submitEvent: FormEvent<HTMLFormElement>) => {
    submitEvent.preventDefault();
    const found = validateBudgetForm(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;
    onSubmit(values);
  };

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Stack spacing={2} sx={{ paddingTop: 1 }}>
        <TextField
          select
          label="Category"
          value={values.categoryId === null ? "" : String(values.categoryId)}
          disabled={isEditing}
          error={errors.categoryId !== undefined}
          helperText={errors.categoryId}
          onChange={(changeEvent) =>
            setValues({ ...values, categoryId: Number(changeEvent.target.value) })
          }
        >
          {categories.map((category) => (
            <MenuItem key={category.id} value={String(category.id)}>
              {category.label}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          label="Amount"
          value={values.amountMajorUnits}
          slotProps={{ htmlInput: { inputMode: "decimal" } }}
          error={errors.amountMajorUnits !== undefined}
          helperText={errors.amountMajorUnits}
          onChange={(changeEvent) =>
            setValues({ ...values, amountMajorUnits: changeEvent.target.value })
          }
        />

        <TextField
          select
          label="Currency"
          value={values.currency}
          error={errors.currency !== undefined}
          helperText={errors.currency}
          onChange={(changeEvent) =>
            setValues({ ...values, currency: changeEvent.target.value as Currency })
          }
        >
          {currencies.map((currency) => (
            <MenuItem key={currency} value={currency}>
              {currency}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          select
          label="Recurrence"
          value={values.recurrence}
          error={errors.recurrence !== undefined}
          helperText={errors.recurrence}
          onChange={(changeEvent) =>
            setValues({ ...values, recurrence: changeEvent.target.value as Recurrence })
          }
        >
          {recurrences.map((recurrence) => (
            <MenuItem key={recurrence} value={recurrence}>
              {recurrence}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          type="date"
          label="Starts on"
          value={values.startsOn}
          slotProps={{ inputLabel: { shrink: true } }}
          error={errors.startsOn !== undefined}
          helperText={errors.startsOn}
          onChange={(changeEvent) => setValues({ ...values, startsOn: changeEvent.target.value })}
        />

        <TextField
          type="date"
          label="Ends on"
          value={values.endsOn ?? ""}
          slotProps={{ inputLabel: { shrink: true } }}
          error={errors.endsOn !== undefined}
          helperText={errors.endsOn}
          onChange={(changeEvent) =>
            setValues({
              ...values,
              endsOn: changeEvent.target.value === "" ? null : changeEvent.target.value,
            })
          }
        />

        <TextField
          type="number"
          label="Alert threshold percent"
          value={values.alertThresholdPercent === null ? "" : String(values.alertThresholdPercent)}
          error={errors.alertThresholdPercent !== undefined}
          helperText={errors.alertThresholdPercent}
          onChange={(changeEvent) =>
            setValues({
              ...values,
              alertThresholdPercent:
                changeEvent.target.value === "" ? null : Number(changeEvent.target.value),
            })
          }
        />

        <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
          <Button type="button" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="contained">
            {submitLabel}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
};

export default BudgetForm;
