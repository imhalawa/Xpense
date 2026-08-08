import { FormEvent, MouseEvent, useState } from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import { DatePicker } from "@mui/x-date-pickers";
import dayjs from "dayjs";
import CurrencyOption from "../CurrencyOption/CurrencyOption";
import { DateIcon } from "../../icons/icons";
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

  const handleAlertChoice = (_changeEvent: MouseEvent<HTMLElement>, choice: string | null) => {
    if (choice === null) return;
    setAlertChoice(choice);
    if (choice === noAlertChoice) setValues({ ...values, alertThresholdPercent: null });
    else if (choice !== customAlertChoice)
      setValues({ ...values, alertThresholdPercent: Number(choice) });
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
          slotProps={{
            select: {
              renderValue: (selected) => <CurrencyOption currency={selected as Currency} />,
            },
          }}
          onChange={(changeEvent) =>
            setValues({ ...values, currency: changeEvent.target.value as Currency })
          }
        >
          {currencies.map((currency) => (
            <MenuItem key={currency} value={currency}>
              <CurrencyOption currency={currency} />
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

        <DatePicker
          label="Starts on"
          value={dayjs(values.startsOn)}
          slots={{ openPickerIcon: DateIcon }}
          slotProps={{
            textField: {
              error: errors.startsOn !== undefined,
              helperText: errors.startsOn,
            },
          }}
          onChange={(picked) =>
            setValues({ ...values, startsOn: picked === null ? "" : picked.format(dayFormat) })
          }
        />

        <DatePicker
          label="Ends on"
          value={values.endsOn === null ? null : dayjs(values.endsOn)}
          slots={{ openPickerIcon: DateIcon }}
          slotProps={{
            textField: {
              error: errors.endsOn !== undefined,
              helperText: errors.endsOn,
            },
          }}
          onChange={(picked) =>
            setValues({ ...values, endsOn: picked === null ? null : picked.format(dayFormat) })
          }
        />

        <Box>
          <Typography variant="body2" sx={{ color: "text.secondary", marginBottom: 1 }}>
            Alert threshold
          </Typography>
          <ToggleButtonGroup
            exclusive
            fullWidth
            size="small"
            value={alertChoice}
            onChange={handleAlertChoice}
            aria-label="Alert threshold">
            <ToggleButton value={noAlertChoice}>No alert</ToggleButton>
            {alertPresets.map((preset) => (
              <ToggleButton key={preset} value={String(preset)}>
                {preset}%
              </ToggleButton>
            ))}
            <ToggleButton value={customAlertChoice}>Custom</ToggleButton>
          </ToggleButtonGroup>

          {alertChoice === customAlertChoice && (
            <TextField
              type="number"
              fullWidth
              label="Alert threshold percent"
              sx={{ marginTop: 2 }}
              value={
                values.alertThresholdPercent === null ? "" : String(values.alertThresholdPercent)
              }
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
          )}
        </Box>

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
