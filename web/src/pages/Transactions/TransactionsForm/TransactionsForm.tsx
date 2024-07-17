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
import { Euro, DollarSign, CalendarIcon, ArrowUpRight, ArrowDownRight } from "lucide-react";
import AccountAutoComplete from "../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete";
import CategoryAutoComplete from "../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete";
import TagAutoComplete from "../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete";
import { TransactionType, Currency } from "../../../typings";
import { Controller, useForm } from "react-hook-form";
import MerchantAutoComplete from "../../../components/Forms/AutoComplete/MerchantAutoComplete/MerchantAutoComplete";
import { useEffect } from "react";
import { ITransactionFormData, mapToTransaction, schema } from "../../../typings/forms/ITransactionFormData";
import { yupResolver } from "@hookform/resolvers/yup";
import { useTransctionUtilities } from "../../../contexts/TransactionUtilitiesContext";
export interface ITransactionFormProps {
  selectedDate: Dayjs | null;
}

const TransactionsForm = ({ selectedDate }: ITransactionFormProps) => {
  const { setSubmittedTransaction } = useTransctionUtilities();
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
    if (dayjs.unix(data.dateOfTransaction).isSame(dayjs(), "day")) {
      setValue("dateOfTransaction", dayjs().unix());
    }
    console.log('submitting transaction');
    setSubmittedTransaction(mapToTransaction(data));
  };

  useEffect(() => {
    setValue("dateOfTransaction", selectedDate?.unix() ?? dayjs().unix());
  }, [selectedDate, setValue]);

  return (
    <form onSubmit={handleSubmit(submit)}>
      <Card>
        <CardContent>
          <Grid container spacing={1}>
            {/* Amount */}
            <Grid item xs={8}>
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
                      InputLabelProps={{ shrink: true }}
                      variant="standard"
                      onChange={onChange}
                      error={!!error}
                      helperText={error?.message}
                    />
                  )}
                />
              </FormControl>
            </Grid>

            {/* Currency */}
            <Grid item xs={4}>
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
                          <Euro size={16} />
                        </MenuItem>
                        <MenuItem value={Currency.DOLLAR}>
                          <DollarSign size={16} />
                        </MenuItem>
                      </Select>
                    </>
                  )}
                />
              </FormControl>
            </Grid>
            {/* Merchant */}
            <Grid item xs={12}>
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
            <Grid item xs={12}>
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
            <Grid item xs={12}>
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
                        openPickerIcon: CalendarIcon,
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
            <Grid item xs={12}>
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
                            <Grid item xs={10}>
                              Income
                            </Grid>
                            <Grid item xs={2}>
                              <Box display="flex" justifyContent="right">
                                <ArrowUpRight color="green" size={20} />
                              </Box>
                            </Grid>
                          </Grid>
                        </MenuItem>
                        <MenuItem value={TransactionType.DEBIT}>
                          <Grid container>
                            <Grid item xs={10}>
                              Expense
                            </Grid>
                            <Grid item xs={2}>
                              <Box display="flex" justifyContent="right">
                                <ArrowDownRight color="red" size={20} />
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
            <Grid item xs={12}>
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
            <Grid item xs={12}>
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
          <Grid item xs={12}>
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
