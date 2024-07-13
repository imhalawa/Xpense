import { Box, Button, FormControl, Grid, Typography } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import { DateCalendar } from "@mui/x-date-pickers";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import { useEffect, useState } from "react";
import { createTransactionFixture } from "../../fixtures";
import { ITransaction } from "../../typings";
import dayjs, { Dayjs } from "dayjs";

const Transactions = () => {
  const [transactions, setTransactions] = useState<ITransaction[]>(
    createTransactionFixture()
  );

  const [filteredTransactions, setFilteredTransactions] =
    useState<ITransaction[]>(transactions);

  const [selectedDate, setSelectedDate] = useState<Dayjs | null>(null);

  const onSubmit = (transaction: ITransaction) => {
    setTransactions([...transactions, transaction]);
  };

  const filterTransactionsByDate = (date: Dayjs | null) => {
    if (date) {
      setSelectedDate(date);
      setFilteredTransactions(
        transactions.filter((transaction) =>
          dayjs.unix(transaction.date).isSame(date, "day")
        )
      );
    } else {
      setSelectedDate(null);
      setFilteredTransactions(transactions);
    }
  };

  useEffect(() => {
    filterTransactionsByDate(selectedDate);
  }, [transactions, selectedDate]);

  return (
    <Grid container spacing={2} mt={2}>
      <Grid item xs={12}>
        <Typography variant="h4">Transactions</Typography>
        <hr />
      </Grid>
      <Grid item xs={12} md={2}>
        <TransactionsForm onSubmit={onSubmit} selectedDate={selectedDate} />
        <hr />
        <FormControl fullWidth>
          <DateCalendar
            value={selectedDate}
            onChange={(date) => setSelectedDate(date)}
            disableFuture
          />
          <Button onClick={() => setSelectedDate(null)}>Clear</Button>
        </FormControl>
      </Grid>
      <Grid item xs={12} md={10}>
        <Box sx={{ height: "80vh", width: "100%" }}>
          <TransactionsGrid transactions={filteredTransactions} />
        </Box>
      </Grid>
    </Grid>
  );
};

export default Transactions;
