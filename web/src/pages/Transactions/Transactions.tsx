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
import axios from "axios";
import { IResponse } from "../../clients/types/IResponse";

const Transactions = () => {
  const { submittedTransaction, setSubmittedTransaction } = useTransctionUtilities();

  const { setLoading } = useLoading();
  const [transactions, setTransactions] = useState<ITransaction[]>(createTransactionFixture());
  const [filteredTransactions, setFilteredTransactions] = useState<ITransaction[]>(transactions);
  const { selectedDate, setSelectedDate } = useCalendar();

  useEffect(() => {
    if (submittedTransaction != null) {
      setLoading(true);
      setTransactions([...transactions, submittedTransaction]);
      setSubmittedTransaction(null);
      setLoading(false);
    }
  }, [submittedTransaction]);

  useEffect(() => {
    axios.defaults.baseURL = "http://localhost:4000/";
    axios.get<IResponse<ITransaction[]>>("/api/transaction").then((response) => {
      setTransactions(response.data.data);
      setFilteredTransactions(response.data.data);
    });
  }, [transactions]);

  const filterTransactionsByDate = (date: Dayjs | null) => {
    if (date) {
      setSelectedDate(date);
      setFilteredTransactions(
        transactions.filter((transaction) => dayjs.unix(transaction.createdOn!).isSame(date, "day"))
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
