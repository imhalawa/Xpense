import { Box, Button, FormControl, Grid, LinearProgress, Typography } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import { DateCalendar } from "@mui/x-date-pickers";
import { useEffect, useState } from "react";
import { createTransactionFixture } from "../../fixtures";
import { ITransaction } from "../../typings";
import dayjs, { Dayjs } from "dayjs";
import { ITransactionFormData, mapToTransaction } from "../../typings/forms/ITransactionFormData";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import { useLoading } from "../../contexts/Loading";

const Transactions = () => {
  const {setLoading} =useLoading();
  const [transactions, setTransactions] = useState<ITransaction[]>(createTransactionFixture());
  const [filteredTransactions, setFilteredTransactions] = useState<ITransaction[]>(transactions);
  const [selectedDate, setSelectedDate] = useState<Dayjs | null>(null);

  const onSubmit = (transaction: ITransactionFormData) => {
    setLoading(true);
    setTimeout(() => {
      console.log(transaction);
      setTransactions([...transactions, mapToTransaction(transaction)]);
      setLoading(false);
    }, 1000);
  };

  const filterTransactionsByDate = (date: Dayjs | null) => {
    if (date) {
      setSelectedDate(date);
      setFilteredTransactions(transactions.filter((transaction) => dayjs.unix(transaction.date).isSame(date, "day")));
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
            sx={{ width: "100%" }}
            onChange={(date) => setSelectedDate(date)}
            disableFuture
          />
          <Button onClick={() => setSelectedDate(null)}>Clear</Button>
        </FormControl>
      </Grid>
      <Grid item xs={12} md={10}>
        <Box sx={{ height: "80vh", width: "100%" }}>
          <TransactionsGrid dense transactions={filteredTransactions} />
        </Box>
      </Grid>
    </Grid>
  );
};

export default Transactions;
