import { Box, Grid } from "@mui/material";
import TransactionsGrid from "./TransactionsGrid/TransactionsGrid";
import Page from "../../components/Page/Page";

const Transactions = () => {
  return (
    <Page title="Transactions" headerColor={"primary.dark"} headerBackgroundColor={"white"}>
      <Grid item xs={12}>
        <Box sx={{ height: "calc(100vh - 256px)", width: "100%" }}>
          <TransactionsGrid size={10} />
        </Box>
      </Grid>
    </Page>
  );
};

export default Transactions;
