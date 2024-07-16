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
  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransaction[]>(createTransactionFixture());
  const [filteredTransactions, setFilteredTransactions] = useState<ITransaction[]>(transactions);
  const [selectedDate, setSelectedDate] = useState<Dayjs | null>(null);

  const onSubmit = (transaction: ITransactionFormData) => {
    setLoading(true);
    setTimeout(() => {
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
    <>
      <Grid item xs={12}>
        <Typography variant="h4" my={2}>
          Transactions
        </Typography>
        <hr />
      </Grid>
      <Grid item xs={12} md={4} lg={3} xl={2}>
        <TransactionsForm onSubmit={onSubmit} selectedDate={selectedDate} />
        <hr />
        <Box display={"flex"} flexDirection={"column"} gap={2}>
          <DateCalendar
            value={selectedDate}
            sx={{ width: "100%" }}
            onChange={(date) => setSelectedDate(date)}
            disableFuture
          />
          <Button onClick={() => setSelectedDate(null)}>Clear</Button>
        </Box>
      </Grid>
      <Grid item xs={12} md={8} lg={9} xl={10}>
        <Box sx={{ height: "80vh", width: "100%" }}>
          <TransactionsGrid dense transactions={filteredTransactions} />
        </Box>
      </Grid>
    </>
  );
};

export default Transactions;
