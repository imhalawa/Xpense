import { Box, Grid, Typography, useMediaQuery } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import { useEffect, useState } from "react";
import { createTransactionFixture } from "../../fixtures";
import { ITransaction } from "../../typings";
import dayjs, { Dayjs } from "dayjs";
import { ITransactionFormData, mapToTransaction } from "../../typings/forms/ITransactionFormData";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import { useLoading } from "../../contexts/LoadingContext";
import { useCalendar } from "../../contexts/CalendarContext";
import { useTheme } from "@mui/material/styles";

const Transactions = () => {
  const theme = useTheme();
  const matchMD = useMediaQuery(theme.breakpoints.down("md"));

  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransaction[]>(createTransactionFixture());
  const [filteredTransactions, setFilteredTransactions] = useState<ITransaction[]>(transactions);
  const { selectedDate, setSelectedDate } = useCalendar();

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
    <Grid container spacing={2}>
      <Grid item xs={12}>
        <Typography variant="h4" my={2}>
          Transactions
        </Typography>
        <hr />
      </Grid>
      <Grid item xs={12} sm={12} md={12} lg={3} xl={2}>
        <TransactionsForm onSubmit={onSubmit} selectedDate={selectedDate} />
      </Grid>

      <Grid item xs={12} sm={12} md={12} lg={9} xl={10} mt={matchMD ? 4 : 0}>
        <Box sx={{ height: "calc(100vh - 256px)", width: "100%" }}>
          <TransactionsGrid dense transactions={filteredTransactions} />
        </Box>
      </Grid>
    </Grid>
  );
};

export default Transactions;
