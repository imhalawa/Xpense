import { Box, Grid, useMediaQuery } from "@mui/material";
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
import Page from "../../components/Page/Page";

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
