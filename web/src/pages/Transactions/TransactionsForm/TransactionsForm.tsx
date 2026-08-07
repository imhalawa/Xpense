import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import { DatePicker } from "@mui/x-date-pickers";
import dayjs from "dayjs";
import { Controller, useForm } from "react-hook-form";
import { yupResolver } from "@hookform/resolvers/yup";
import CurrencyOption from "../../../components/CurrencyOption/CurrencyOption";
import { DateIcon, ExpenseIcon, IncomeIcon } from "../../../icons/icons";
import AccountAutoComplete from "../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete";
import CategoryAutoComplete from "../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete";
import MerchantAutoComplete from "../../../components/Forms/AutoComplete/MerchantAutoComplete/MerchantAutoComplete";
import TagAutoComplete from "../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete";
import { Currency } from "../../../typings/enums/Currency";
import { TransactionType } from "../../../typings/enums/TransactionType";
import {
  fromTransactionFormData,
  ITransactionFormData,
  schema,
} from "../../../typings/forms/ITransactionFormData";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";
import { useLoading } from "../../../contexts/LoadingContext";
import { createTransaction } from "../../../clients/transactions";

export interface ITransactionFormProps {
  onCancel: () => void;
  onSubmitted: () => void;
  submitLabel?: string;
}

const currencies: Currency[] = Object.values(Currency);

const TransactionsForm = ({
  onCancel,
  onSubmitted,
  submitLabel = "Create",
}: ITransactionFormProps) => {
  const { setSubmittedTransaction } = useTransctionUtilities();
  const { setLoading } = useLoading();

  const { handleSubmit, control } = useForm<ITransactionFormData>({
    resolver: yupResolver(schema),
    defaultValues: {
      amount: 0,
      currency: Currency.EUR,
      type: TransactionType.DEBIT,
      dateOfTransaction: dayjs().unix(),
      account: null,
      category: null,
      merchant: null,
      tags: [],
    },
  });

  const submit = (data: ITransactionFormData) => {
    setLoading(true);

    createTransaction(fromTransactionFormData(data))
      .then((transaction) => {
        setSubmittedTransaction(transaction);
        setLoading(false);
        onSubmitted();
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  };

  return (
    <Box component="form" onSubmit={handleSubmit(submit)} noValidate>
      <Stack spacing={2} sx={{ paddingTop: 1 }}>
        <Stack direction="row" spacing={2}>
          <Controller
            name="amount"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <TextField
                fullWidth
                type="number"
                label="Amount"
                value={value}
                error={error !== undefined}
                helperText={error?.message}
                onChange={onChange}
              />
            )}
          />

          <Controller
            name="currency"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <TextField
                select
                label="Currency"
                value={value}
                error={error !== undefined}
                helperText={error?.message}
                sx={{ minWidth: 140 }}
                slotProps={{
                  select: {
                    renderValue: (selected) => <CurrencyOption currency={selected as Currency} />,
                  },
                }}
                onChange={onChange}
              >
                {currencies.map((currency) => (
                  <MenuItem key={currency} value={currency}>
                    <CurrencyOption currency={currency} />
                  </MenuItem>
                ))}
              </TextField>
            )}
          />
        </Stack>

        <Controller
          name="merchant"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <MerchantAutoComplete
              label="Merchant"
              value={value}
              error={error !== undefined}
              helperText={error?.message}
              onChange={onChange}
            />
          )}
        />

        <Controller
          name="category"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <CategoryAutoComplete
              label="Category"
              value={value}
              error={error !== undefined}
              helperText={error?.message}
              onChange={onChange}
            />
          )}
        />

        <Controller
          name="dateOfTransaction"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <DatePicker
              label="Date of Transaction"
              value={dayjs.unix(value)}
              disableFuture
              slots={{ openPickerIcon: DateIcon }}
              slotProps={{
                textField: {
                  error: error !== undefined,
                  helperText: error?.message,
                },
              }}
              onChange={(picked) => onChange(picked === null ? null : picked.unix())}
            />
          )}
        />

        <Controller
          name="type"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <TextField
              select
              label="Transaction Type"
              value={value}
              error={error !== undefined}
              helperText={error?.message}
              onChange={onChange}
            >
              <MenuItem value={TransactionType.CREDIT}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <Box sx={{ display: "flex", color: "success.main" }}>
                    <IncomeIcon size={18} />
                  </Box>
                  Income
                </Box>
              </MenuItem>
              <MenuItem value={TransactionType.DEBIT}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <Box sx={{ display: "flex", color: "error.main" }}>
                    <ExpenseIcon size={18} />
                  </Box>
                  Expense
                </Box>
              </MenuItem>
            </TextField>
          )}
        />

        <Controller
          name="account"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <AccountAutoComplete
              label="Account"
              value={value}
              error={error !== undefined}
              helperText={error?.message}
              onChange={onChange}
            />
          )}
        />

        <Controller
          name="tags"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <TagAutoComplete
              label="Tags"
              value={value}
              error={error !== undefined}
              helperText={error?.message}
              onChange={onChange}
            />
          )}
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

export default TransactionsForm;
