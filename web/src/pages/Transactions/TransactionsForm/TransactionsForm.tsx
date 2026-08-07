import {
  FormControl,
  TextField,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Grid,
  Box,
  FormHelperText,
  Card,
  CardActions,
  CardContent,
} from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers";
import dayjs, { Dayjs } from "dayjs";
import { DateIcon, DollarIcon, EuroIcon, ExpenseIcon, IncomeIcon } from "../../../icons/icons";
import AccountAutoComplete from "../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete";
import CategoryAutoComplete from "../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete";
import TagAutoComplete from "../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete";
import { TransactionType, Currency } from "../../../typings";
import { Controller, useForm } from "react-hook-form";
import MerchantAutoComplete from "../../../components/Forms/AutoComplete/MerchantAutoComplete/MerchantAutoComplete";
import { useEffect } from "react";
import { fromTransactionFormData, ITransactionFormData, schema } from "../../../typings/forms/ITransactionFormData";
import { yupResolver } from "@hookform/resolvers/yup";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";
import { useLoading } from "../../../contexts/LoadingContext";
import { createTransaction } from "../../../clients/transactions";
export interface ITransactionFormProps {
  selectedDate: Dayjs | null;
}

const TransactionsForm = ({ selectedDate }: ITransactionFormProps) => {
  const { setSubmittedTransaction } = useTransctionUtilities();
  const { setLoading } = useLoading();

  const { handleSubmit, control, setValue, watch } = useForm<ITransactionFormData>({
    resolver: yupResolver(schema),
    defaultValues: {
      amount: 0,
      currency: Currency.EUR,
      type: TransactionType.DEBIT,
      dateOfTransaction: selectedDate?.unix() ?? dayjs().unix(),
      account: null,
      category: null,
      merchant: null,
      tags: [],
    },
  });

  watch("dateOfTransaction");

  const submit = (data: ITransactionFormData) => {
    setLoading(true);

    if (dayjs.unix(data.dateOfTransaction).isSame(dayjs(), "day")) {
      setValue("dateOfTransaction", dayjs().unix());
    }

    createTransaction(fromTransactionFormData(data))
      .then((transaction) => {
        setSubmittedTransaction(transaction);
        setLoading(false);
      })
      .catch((error) => {
        console.error(error);
        setLoading(false);
      });
  };

  useEffect(() => {
    setValue("dateOfTransaction", selectedDate?.unix() ?? dayjs().unix());
  }, [selectedDate]);

  return (
    <form onSubmit={handleSubmit(submit)}>
      <Card>
        <CardContent>
          <Grid container spacing={1}>
            {/* Amount */}
            <Grid size={8}>
              <FormControl fullWidth>
                <Controller
                  name="amount"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <TextField
                      id="outlined-number"
                      label="Amount"
                      type="number"
                      value={value}
                      variant="standard"
                      onChange={onChange}
                      error={!!error}
                      helperText={error?.message}
                      slotProps={{
                        inputLabel: { shrink: true }
                      }}
                    />
                  )}
                />
              </FormControl>
            </Grid>

            {/* Currency */}
            <Grid size={4}>
              <FormControl fullWidth>
                <Controller
                  name="currency"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <>
                      <InputLabel id="lbl-select-currency">Currency</InputLabel>
                      <Select
                        labelId="lbl-select-currency"
                        id="select-currency"
                        value={value}
                        label="Currency"
                        variant="standard"
                        onChange={onChange}
                        error={!!error}
                      >
                        <FormHelperText>{error?.message}</FormHelperText>
                        <MenuItem value={Currency.EUR}>
                          <EuroIcon size={16} />
                        </MenuItem>
                        <MenuItem value={Currency.USD}>
                          <DollarIcon size={16} />
                        </MenuItem>
                      </Select>
                    </>
                  )}
                />
              </FormControl>
            </Grid>
            {/* Merchant */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="merchant"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <MerchantAutoComplete
                      label="Merchant"
                      onChange={onChange}
                      value={value}
                      error={!!error}
                      helperText={error?.message}
                    />
                  )}
                />
              </FormControl>
            </Grid>
            {/* Category */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="category"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <CategoryAutoComplete
                      label="Category"
                      onChange={onChange}
                      value={value}
                      error={!!error}
                      helperText={error?.message}
                    />
                  )}
                />
              </FormControl>
            </Grid>
            {/* Date of Transaction */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="dateOfTransaction"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <DatePicker
                      value={dayjs.unix(value)}
                      onChange={onChange}
                      label="Date of Transaction"
                      slots={{
                        openPickerIcon: DateIcon,
                      }}
                      slotProps={{
                        textField: {
                          variant: "standard",
                          error: !!error,
                          helperText: error?.message,
                        },
                      }}
                      disableFuture
                    />
                  )}
                />
              </FormControl>
            </Grid>

            {/* Transaction Type */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="type"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <>
                      <InputLabel id="lbl-select-transaction-type">Transaction Type</InputLabel>
                      <Select
                        id="lbl-select-transaction-type"
                        labelId="lbl-select-transaction-type"
                        value={value}
                        label="Transaction Type"
                        variant="standard"
                        error={!!error}
                        onChange={onChange}
                      >
                        <FormHelperText>{error?.message}</FormHelperText>
                        <MenuItem value={TransactionType.CREDIT}>
                          <Grid container>
                            <Grid size={10}>
                              Income
                            </Grid>
                            <Grid size={2}>
                              <Box
                                sx={{
                                  display: "flex",
                                  justifyContent: "right",
                                  color: "success.main"
                                }}>
                                <IncomeIcon size={20} />
                              </Box>
                            </Grid>
                          </Grid>
                        </MenuItem>
                        <MenuItem value={TransactionType.DEBIT}>
                          <Grid container>
                            <Grid size={10}>
                              Expense
                            </Grid>
                            <Grid size={2}>
                              <Box
                                sx={{
                                  display: "flex",
                                  justifyContent: "right",
                                  color: "error.main"
                                }}>
                                <ExpenseIcon size={20} />
                              </Box>
                            </Grid>
                          </Grid>
                        </MenuItem>
                      </Select>
                    </>
                  )}
                />
              </FormControl>
            </Grid>

            {/* Account */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="account"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <AccountAutoComplete
                      label="Account"
                      error={!!error}
                      value={value}
                      helperText={error?.message}
                      onChange={onChange}
                    />
                  )}
                />
              </FormControl>
            </Grid>

            {/* Tags */}
            <Grid size={12}>
              <FormControl fullWidth>
                <Controller
                  name="tags"
                  control={control}
                  render={({ field: { onChange, value }, fieldState: { error } }) => (
                    <TagAutoComplete
                      label="Tags"
                      value={value}
                      error={!!error}
                      helperText={error?.message}
                      onChange={onChange}
                    />
                  )}
                />
              </FormControl>
            </Grid>
          </Grid>
        </CardContent>
        <CardActions>
          <Grid size={12}>
            <Button variant="contained" fullWidth type="submit">
              Add Transaction
            </Button>
          </Grid>
        </CardActions>
      </Card>
    </form>
  );
};

export default TransactionsForm;
