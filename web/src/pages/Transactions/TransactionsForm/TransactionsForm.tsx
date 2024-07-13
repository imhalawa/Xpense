import {
  FormControl,
  TextField,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Grid,
} from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers";
import dayjs, { Dayjs } from "dayjs";
import { Euro, DollarSign } from "lucide-react";
import { useState, useEffect } from "react";
import AccountAutoComplete from "../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete";
import CategoryAutoComplete from "../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete";
import TagAutoComplete from "../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete";
import {
  createCategoriesFixture,
  createAccountFixture,
  createTagFixture,
} from "../../../fixtures";
import {
  TransactionType,
  ICategory,
  IAccount,
  ITag,
  Currency,
} from "../../../typings";
import buildTransaction, { ITransaction } from "../../../typings/ITransaction";

export interface ITransactionFormProps {
  onSubmit: (transaction: ITransaction) => void;
  selectedDate: Dayjs | null;
}

const TransactionsForm = ({
  onSubmit,
  selectedDate,
}: ITransactionFormProps) => {
  const categories = createCategoriesFixture();
  const accounts = createAccountFixture();
  const defaultAccount = accounts.find((a) => a.isMainAccount);
  const defaultCategory = categories.find((c) => c.priority.weight === 2);

  const [amount, setAmount] = useState(0);
  const [transactionType, setTransactionType] = useState(TransactionType.DEBIT);
  const [currency, setCurrency] = useState(Currency.EUR);
  const [transactionDateTime, setTransactionDateTime] = useState<Dayjs>(
    selectedDate ?? dayjs()
  );
  const [categoryOptions, setCategoryOptions] = useState(categories);
  const [category, setCategory] = useState<ICategory | null>(
    defaultCategory || null
  );
  const [accountOptions, setAccountOptions] = useState(createAccountFixture());
  const [account, setAccount] = useState<IAccount | null>(
    defaultAccount || null
  );

  const [tagOptions, setTagOptions] = useState(createTagFixture());
  const [tags, setTags] = useState<ITag[] | null>(null);

  useEffect(() => {
    setTransactionDateTime(selectedDate ?? dayjs());
  }, [selectedDate]);

  const handleSubmit = (e: React.MouseEvent<HTMLButtonElement, MouseEvent>) => {
    e.preventDefault();

    if (category === null) {
      alert("Please select a category");
      return;
    }

    if (account === null) {
      alert("Please select an account");
      return;
    }

    if ((selectedDate ?? dayjs()).isSame(dayjs(), "day")) {
      setTransactionDateTime(dayjs());
    }

    const transaction: ITransaction = buildTransaction(
      0,
      {
        currency: currency,
        value: amount,
      },
      transactionDateTime.unix(),
      category,
      { id: 1, label: "Merchant X" },
      account,
      transactionType,
      tags
    );

    onSubmit(transaction);
  };

  return (
    <>
      <Grid container spacing={1}>
        <Grid item xs={8}>
          <FormControl fullWidth>
            <TextField
              id="outlined-number"
              label="Amount"
              type="number"
              value={amount}
              required
              InputLabelProps={{
                shrink: true,
              }}
              onChange={(e) => setAmount(Number(e.target.value))}
            />
          </FormControl>
        </Grid>

        <Grid item xs={4}>
          <FormControl fullWidth>
            <InputLabel id="demo-simple-select-label">Currency</InputLabel>
            <Select
              labelId="demo-simple-select-label"
              id="demo-simple-select"
              value={currency}
              required
              label="Currency"
              onChange={(e) => setCurrency(e.target.value as Currency)}
            >
              <MenuItem value={Currency.EUR}>
                <Euro size={16} />
              </MenuItem>
              <MenuItem value={Currency.DOLLAR}>
                <DollarSign size={16} />
              </MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <FormControl fullWidth>
            <CategoryAutoComplete
              defaultValue={category}
              options={categoryOptions}
              label="Category"
              onChange={(category) => setCategory(category)}
            />
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <FormControl fullWidth>
            <DatePicker
              value={transactionDateTime}
              onChange={(e) => setTransactionDateTime(e as Dayjs)}
              label="Date of Transaction"
              disableFuture
            />
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <FormControl fullWidth>
            <InputLabel id="demo-simple-select-label">
              Transaction Type
            </InputLabel>
            <Select
              labelId="demo-simple-select-label"
              id="demo-simple-select"
              value={transactionType}
              label="Transaction Type"
              onChange={(e) =>
                setTransactionType(e.target.value as TransactionType)
              }
            >
              <MenuItem value={TransactionType.CREDIT}>Income</MenuItem>
              <MenuItem value={TransactionType.DEBIT}>Expense</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <FormControl fullWidth>
            <AccountAutoComplete
              defaultValue={account}
              options={accountOptions}
              label="Account"
              onChange={(account) => setAccount(account)}
            />
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <FormControl fullWidth>
            <TagAutoComplete
              options={tagOptions}
              label="Tags"
              onChange={(value) => setTags(value)}
            />
          </FormControl>
        </Grid>
        <Grid item xs={12}>
          <Button
            variant="contained"
            fullWidth
            onClick={(e) => handleSubmit(e)}
          >
            Add Transaction
          </Button>
        </Grid>
      </Grid>
    </>
  );
};

export default TransactionsForm;
