import { Box, Grid } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import { useEffect, useState } from "react";
import { createTransactionFixture } from "../../fixtures";
import { ITransaction } from "../../typings";
import dayjs, { Dayjs } from "dayjs";
import { useLoading } from "../../contexts/LoadingContext";
import { useCalendar } from "../../contexts/CalendarContext";
import Page from "../../components/Page/Page";
import { useTransctionUtilities } from "../../contexts/TransactionUtilitiesContext";

const Transactions = () => {
  const { submittedTransaction } = useTransctionUtilities();

  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransaction[]>(createTransactionFixture());
  const [filteredTransactions, setFilteredTransactions] = useState<ITransaction[]>(transactions);
  const { selectedDate, setSelectedDate } = useCalendar();

  useEffect(() => {
    console.log("received submitted transaction");

    setLoading(true);
    setTimeout(() => {
      if (submittedTransaction != null) {
        setTransactions([...transactions, submittedTransaction]);
      }
      setLoading(false);
    }, 1000);
  }, [submittedTransaction]);

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
    <Page title="Transactions" headerColor={"primary.dark"} headerBackgroundColor={"white"}>
      <Grid item xs={12}>
        <Box sx={{ height: "calc(100vh - 256px)", width: "100%" }}>
          <TransactionsGrid dense transactions={filteredTransactions} />
        </Box>
      </Grid>
    </Page>
  );
};

export default Transactions;
