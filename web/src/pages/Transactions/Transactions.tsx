import { Box, Grid } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import TransactionsForm from "./TransactionsForm/TransactionsForm";
import Calendar from "../../components/Calendary/Calendar";
import Page from "../../components/Page/Page";
import { useCalendar } from "../../contexts/CalendarContext";

const Transactions = () => {
  const { selectedDate } = useCalendar();

  return (
    <Page title="Transactions" headerColor={"primary.dark"} headerBackgroundColor={"background.paper"}>
      <Grid size={{ xs: 12, md: 8 }}>
        <Box sx={{ height: "calc(100vh - 256px)", width: "100%" }}>
          <TransactionsGrid size={10} />
        </Box>
      </Grid>
      <Grid size={{ xs: 12, md: 4 }}>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <TransactionsForm selectedDate={selectedDate} />
          <Calendar />
        </Box>
      </Grid>
    </Page>
  );
};

export default Transactions;
